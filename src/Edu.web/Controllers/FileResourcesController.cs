using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Edu.Infrastructure.Data;
using Edu.Application.IServices;
using Edu.Domain.Entities;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Edu.Web.Controllers
{
    [Authorize(Roles = "Teacher,Admin")]
    public class FileResourcesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fileStorage;
        private readonly ILogger<FileResourcesController> _logger;

        // simple policy defaults
        private readonly string[] _allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".zip", ".png", ".jpg", ".jpeg", ".mp4", ".mov" };
        private const long _maxFileBytes = 20 * 1024 * 1024; // 20 MB

        public FileResourcesController(ApplicationDbContext db, IFileStorageService fileStorage, ILogger<FileResourcesController> logger)
        {
            _db = db;
            _fileStorage = fileStorage;
            _logger = logger;
        }

        // Utility: sanitize file name for storage/display
        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "file";
            // remove directory separators and control chars
            var safe = Path.GetFileName(name);
            // optionally remove any invalid chars
            safe = Regex.Replace(safe, @"[^\w\-. ]+", "_");
            return safe;
        }

        /// <summary>
        /// Upload a file for a lesson. Returns JSON { success, id, storageKey, fileUrl }.
        /// - Stores provider-agnostic StorageKey in DB.
        /// - Sets FileUrl to resolved public url (if available) for quick preview.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadForLesson([FromForm] IFormFile file, [FromForm] int? privateLessonId, [FromForm] int? schoolLessonId)
        {
            if (file == null) return BadRequest(new { success = false, error = "NoFile" });
            if (privateLessonId == null && schoolLessonId == null) return BadRequest(new { success = false, error = "NoLesson" });

            // Validate size
            if (file.Length <= 0 || file.Length > _maxFileBytes)
            {
                return BadRequest(new { success = false, error = "InvalidSize" });
            }

            // Validate extension
            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? string.Empty;
            if (!_allowedExtensions.Contains(ext))
            {
                return BadRequest(new { success = false, error = "InvalidType", type = ext });
            }

            try
            {
                var folder = privateLessonId.HasValue ? $"private-lessons/{privateLessonId.Value}" : $"school-lessons/{schoolLessonId.Value}";

                // Save file (returns provider-agnostic storage key, e.g. "private-lessons/123/uuid.ext")
                var storageKey = await _fileStorage.SaveFileAsync(file, folder);

                // Try to resolve a public URL (SAS/CDN/local) for immediate use (may return null)
                string? publicUrl = null;
                try
                {
                    publicUrl = await _fileStorage.GetPublicUrlAsync(storageKey, TimeSpan.FromMinutes(15));
                }
                catch
                {
                    // ignore
                }

                // sanitize original file name for display
                var originalName = SanitizeFileName(file.FileName);

                var fr = new FileResource
                {
                    StorageKey = storageKey,
                    FileUrl = publicUrl ?? storageKey, // store storageKey for portability, but keep FileUrl for quick access / old clients
                    Name = originalName,
                    FileType = file.ContentType,
                    PrivateLessonId = privateLessonId,
                    SchoolLessonId = schoolLessonId,
                    CreatedAtUtc = DateTime.UtcNow
                };

                _db.FileResources.Add(fr);
                await _db.SaveChangesAsync();

                return Json(new { success = true, id = fr.Id, storageKey = fr.StorageKey, fileUrl = fr.FileUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upload failed");
                return StatusCode(500, new { success = false, error = "UploadFailed" });
            }
        }
    }
}
