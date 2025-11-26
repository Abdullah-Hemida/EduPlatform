
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.StaticFiles;
using Edu.Infrastructure.Data;
using Edu.Domain.Entities;
using Edu.Application.IServices;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Edu.Web.Areas.Teacher.Controllers
{
    [Area("Teacher")]
    [Authorize(Roles = "Teacher")]
    public class FileResourcesController : TeacherBaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileStorageService _fileStorage;
        private readonly ILogger<FileResourcesController> _logger;
        private readonly FileExtensionContentTypeProvider _contentTypeProvider = new FileExtensionContentTypeProvider();

        // allowed extensions & max size (adjust as needed)
        private static readonly string[] AllowedExtensions = new[] { ".pdf", ".doc", ".docx", ".zip", ".jpg", ".jpeg", ".png", ".mp4", ".mkv" };
        private const long MaxFileBytes = 200L * 1024 * 1024; // 200 MB

        public FileResourcesController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IFileStorageService fileStorage,
            ILogger<FileResourcesController> logger)
        {
            _db = db;
            _userManager = userManager;
            _fileStorage = fileStorage;
            _logger = logger;
        }

        // Utility: sanitize file name for storage/display
        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "file";
            var safe = Path.GetFileName(name);
            // remove any invalid characters (keep letters, numbers, dot, dash, underscore, space)
            safe = Regex.Replace(safe, @"[^\w\-. ]+", "_");
            // trim length to reasonable value
            return safe.Length > 200 ? safe.Substring(0, 200) : safe;
        }

        /// <summary>
        /// POST: Teacher/FileResources/Upload
        /// Upload multiple files for a private lesson. Saves storage key and a resolved FileUrl (if available).
        /// Returns redirect (same behavior as your original) and sets TempData with results/errors.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(int lessonId, IFormFile[]? files, CancellationToken ct = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var lesson = await _db.PrivateLessons.Include(l => l.PrivateCourse).FirstOrDefaultAsync(l => l.Id == lessonId, ct);
            if (lesson == null) return NotFound();
            if (lesson.PrivateCourse?.TeacherId != user.Id) return Forbid();

            if (files == null || files.Length == 0)
            {
                TempData["Error"] = "No files uploaded.";
                return RedirectToAction("Details", "Courses", new { area = "Teacher", id = lesson.PrivateCourseId });
            }

            var errors = new List<string>();
            var uploaded = new List<(int fileResourceId, string storageKey, string? fileUrl)>();

            foreach (var file in files)
            {
                if (file == null || file.Length == 0)
                {
                    errors.Add("Empty file skipped.");
                    continue;
                }

                if (file.Length > MaxFileBytes)
                {
                    errors.Add($"'{file.FileName}' exceeds maximum size ({MaxFileBytes / (1024 * 1024)} MB).");
                    continue;
                }

                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!AllowedExtensions.Contains(ext))
                {
                    errors.Add($"'{file.FileName}' unsupported extension '{ext}'.");
                    continue;
                }

                try
                {
                    var folder = $"private-lessons/{lesson.Id}";
                    // SaveFileAsync should return a provider-agnostic storage key (e.g. "private-lessons/123/uuid.ext")
                    var storageKey = await _fileStorage.SaveFileAsync(file, folder);

                    // Try to obtain a public URL (SAS/CDN/local) to store in FileUrl for quick preview
                    string? publicUrl = null;
                    try
                    {
                        publicUrl = await _fileStorage.GetPublicUrlAsync(storageKey, TimeSpan.FromMinutes(30));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "GetPublicUrlAsync failed for {Key}", storageKey);
                        publicUrl = null;
                    }

                    var fr = new FileResource
                    {
                        PrivateLessonId = lesson.Id,
                        StorageKey = storageKey,
                        FileUrl = publicUrl, // keep null if not resolvable
                        Name = SanitizeFileName(file.FileName),
                        FileType = file.ContentType,
                        CreatedAtUtc = DateTime.UtcNow
                    };

                    _db.FileResources.Add(fr);
                    await _db.SaveChangesAsync(ct); // save per file to obtain Id and to avoid large transactions

                    uploaded.Add((fr.Id, fr.StorageKey!, fr.FileUrl));
                }
                catch (OperationCanceledException)
                {
                    errors.Add($"Upload of '{file.FileName}' was canceled.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed storing file '{FileName}' for lesson {LessonId}", file.FileName, lesson.Id);
                    errors.Add($"Failed to save '{file.FileName}'.");
                }
            }

            if (uploaded.Any())
            {
                TempData["Success"] = uploaded.Count == 1 ? "File uploaded." : $"{uploaded.Count} files uploaded.";
            }

            if (errors.Any())
            {
                TempData["FileUploadErrors"] = string.Join(" | ", errors);
            }

            return RedirectToAction("Details", "Courses", new { area = "Teacher", id = lesson.PrivateCourseId });
        }

        /// <summary>
        /// GET: Teacher/FileResources/Download/5
        /// - permission: only owner teacher or admin can download
        /// - prefers public url (SAS/CDN) via GetPublicUrlAsync, falls back to streaming via OpenReadAsync.
        /// - supports range processing when provider/stream supports it.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Download(int id)
        {
            var fr = await _db.FileResources
                              .Include(f => f.PrivateLesson)
                                .ThenInclude(l => l.PrivateCourse)
                              .FirstOrDefaultAsync(f => f.Id == id);
            if (fr == null) return NotFound();

            var course = fr.PrivateLesson?.PrivateCourse;
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (course == null) return NotFound();
            if (course.TeacherId != user.Id && !User.IsInRole("Admin")) return Forbid();

            // prefer StorageKey, fallback to FileUrl (older records)
            var key = fr.StorageKey ?? fr.FileUrl;
            if (string.IsNullOrWhiteSpace(key)) return NotFound();

            // If key looks like an absolute URL, redirect immediately (fast path)
            if (Uri.TryCreate(key, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https"))
            {
                return Redirect(key);
            }

            // Check existence (best-effort)
            var exists = await _fileStorage.ExistsAsync(key);
            if (!exists)
            {
                _logger.LogWarning("File storage missing key {Key} for FileResource {Id}", key, id);
                return NotFound();
            }

            // Prefer to resolve provider public URL (SAS/CDN) so client downloads directly
            try
            {
                var pub = await _fileStorage.GetPublicUrlAsync(key, TimeSpan.FromMinutes(30));
                if (!string.IsNullOrEmpty(pub) && Uri.TryCreate(pub, UriKind.Absolute, out var pubUri))
                {
                    return Redirect(pub);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "GetPublicUrlAsync failed for {Key}", key);
            }

            // Otherwise open stream from provider
            var stream = await _fileStorage.OpenReadAsync(key);
            if (stream == null)
            {
                _logger.LogWarning("OpenReadAsync returned null for key {Key} FileResource {Id}", key, id);
                return NotFound();
            }

            // Determine content type
            var contentType = fr.FileType;
            if (string.IsNullOrEmpty(contentType))
            {
                if (!_contentTypeProvider.TryGetContentType(fr.Name ?? string.Empty, out var ct)) ct = "application/octet-stream";
                contentType = ct;
            }

            var fileName = string.IsNullOrEmpty(fr.Name) ? Path.GetFileName(key) : fr.Name;

            // If stream supports seeking/range, enable range processing
            try
            {
                if (stream.CanSeek)
                {
                    return File(stream, contentType, fileName, enableRangeProcessing: true);
                }
                else
                {
                    // non-seekable: return File without rangeProcessing
                    return File(stream, contentType, fileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Returning file stream failed for FileResource {Id}; attempting fallback", id);
                try
                {
                    stream.Position = 0;
                }
                catch { /* ignore */ }
                return File(stream, contentType, fileName);
            }
        }

        // POST: Teacher/FileResources/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var fr = await _db.FileResources
                              .Include(f => f.PrivateLesson)
                                .ThenInclude(l => l.PrivateCourse)
                              .FirstOrDefaultAsync(f => f.Id == id);

            if (fr == null) return NotFound();

            var course = fr.PrivateLesson?.PrivateCourse;
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (course == null) return NotFound();
            if (course.TeacherId != user.Id && !User.IsInRole("Admin")) return Forbid();

            // best-effort delete from storage
            try
            {
                var keyToDelete = !string.IsNullOrEmpty(fr.StorageKey) ? fr.StorageKey : fr.FileUrl;
                if (!string.IsNullOrEmpty(keyToDelete))
                {
                    await _fileStorage.DeleteFileAsync(keyToDelete);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete storage for FileResource {Id}", id);
            }

            _db.FileResources.Remove(fr);
            await _db.SaveChangesAsync();

            TempData["Success"] = "File removed.";
            return RedirectToAction("Details", "Courses", new { area = "Teacher", id = course.Id });
        }
    }
}


