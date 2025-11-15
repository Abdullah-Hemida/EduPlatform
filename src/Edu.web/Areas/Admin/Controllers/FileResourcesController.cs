using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Edu.Infrastructure.Data;
using Edu.Application.IServices;
using Microsoft.Extensions.Logging;

namespace Edu.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class FileResourcesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fileStorage;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<FileResourcesController> _logger;

        public FileResourcesController(
            ApplicationDbContext db,
            IFileStorageService fileStorage,
            IWebHostEnvironment env,
            ILogger<FileResourcesController> logger)
        {
            _db = db;
            _fileStorage = fileStorage;
            _env = env;
            _logger = logger;
        }

        // GET: Admin/FileResources
        public async Task<IActionResult> Index(int? privateLessonId, int? schoolLessonId)
        {
            ViewData["ActivePage"] = "PrivateCourses";
            var q = _db.FileResources
                .AsNoTracking()
                .Include(f => f.PrivateLesson)
                .Include(f => f.SchoolLesson)
                .AsQueryable();

            if (privateLessonId.HasValue) q = q.Where(f => f.PrivateLessonId == privateLessonId.Value);
            if (schoolLessonId.HasValue) q = q.Where(f => f.SchoolLessonId == schoolLessonId.Value);

            var list = await q.OrderByDescending(f => f.Id).ToListAsync();
            return View(list);
        }

        // GET: Admin/FileResources/IndexByPrivateLesson/5
        public async Task<IActionResult> IndexByPrivateLesson(int lessonId)
        {
            ViewData["ActivePage"] = "PrivateCourses";
            var files = await _db.FileResources
                .AsNoTracking()
                .Where(f => f.PrivateLessonId == lessonId)
                .OrderByDescending(f => f.Id)
                .ToListAsync();

            return View("Index", files);
        }

        /// <summary>
        /// Download file resource by id.
        /// Uses IFileStorageService.GetPublicUrlAsync(key, expiry) first; if returns an absolute URL -> redirect.
        /// Else tries OpenReadAsync (stream). Else falls back to local wwwroot file if present.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Download(int id)
        {
            var file = await _db.FileResources.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
            if (file == null) return NotFound();

            // Prefer StorageKey; fall back to FileUrl for older records
            var keyOrUrl = !string.IsNullOrWhiteSpace(file.StorageKey) ? file.StorageKey : file.FileUrl;
            if (string.IsNullOrWhiteSpace(keyOrUrl))
                return NotFound();

            try
            {
                // 1) Ask storage provider for a public URL (may be SAS, CDN or direct)
                string? publicUrl = null;
                try
                {
                    publicUrl = await _fileStorage.GetPublicUrlAsync(keyOrUrl, TimeSpan.FromMinutes(15));
                }
                catch (Exception ex)
                {
                    // Non-fatal — provider might not support SAS, we'll attempt streaming
                    _logger.LogDebug(ex, "GetPublicUrlAsync failed for key {Key}", keyOrUrl);
                    publicUrl = null;
                }

                if (!string.IsNullOrWhiteSpace(publicUrl) &&
                    (publicUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                     publicUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    // Redirect to the CDN/SAS URL for direct download/streaming
                    return Redirect(publicUrl);
                }

                // 2) Try to stream from storage service
                var stream = await _fileStorage.OpenReadAsync(keyOrUrl);
                if (stream != null)
                {
                    var provider = new FileExtensionContentTypeProvider();
                    var fileName = string.IsNullOrEmpty(file.Name) ? Path.GetFileName(keyOrUrl) : file.Name;
                    var contentType = _fileStorage.GetContentType(fileName) ?? (provider.TryGetContentType(fileName, out var ct) ? ct : "application/octet-stream");

                    // Return the stream - ASP.NET will dispose the stream when response completes
                    return File(stream, contentType, fileName);
                }

                // 3) Try to serve as local wwwroot file (relative path)
                var rel = keyOrUrl;
                if (rel.StartsWith("~/")) rel = rel.Substring(2);
                if (rel.StartsWith("/")) rel = rel.Substring(1);

                var physical = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, rel.Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(physical))
                {
                    var provider2 = new FileExtensionContentTypeProvider();
                    if (!provider2.TryGetContentType(physical, out var kind)) kind = "application/octet-stream";
                    var fs = System.IO.File.OpenRead(physical);
                    var fileName = string.IsNullOrEmpty(file.Name) ? Path.GetFileName(physical) : file.Name;
                    return File(fs, kind, fileName);
                }

                // 4) fallback: if keyOrUrl looks like absolute url, redirect
                if (keyOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    keyOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    return Redirect(keyOrUrl);
                }

                return NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while downloading FileResource {Id}", id);
                return StatusCode(500, "Error while downloading file.");
            }
        }

        // POST: Admin/FileResources/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var file = await _db.FileResources.FindAsync(id);
            if (file == null) return NotFound();

            try
            {
                var keyOrUrl = !string.IsNullOrWhiteSpace(file.StorageKey) ? file.StorageKey : file.FileUrl;

                if (!string.IsNullOrWhiteSpace(keyOrUrl))
                {
                    try
                    {
                        await _fileStorage.DeleteFileAsync(keyOrUrl);
                    }
                    catch (Exception ex)
                    {
                        // log but continue to remove DB record
                        _logger.LogWarning(ex, "Failed to delete file from storage for FileResource {Id}", id);
                    }
                }

                _db.FileResources.Remove(file);
                await _db.SaveChangesAsync();

                TempData["Success"] = "File deleted.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete FileResource {Id}", id);
                TempData["Error"] = "Unable to delete file.";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}


