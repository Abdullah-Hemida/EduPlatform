using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Edu.Infrastructure.Data;
using Edu.Application.IServices;
using Edu.Domain.Entities;

namespace Edu.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize] // need to be authenticated for download; role checks done on actions
    public class FileResourcesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fileStorage;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<FileResourcesController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        private static readonly FileExtensionContentTypeProvider s_contentTypeProvider = new();

        public FileResourcesController(
            ApplicationDbContext db,
            IFileStorageService fileStorage,
            IWebHostEnvironment env,
            ILogger<FileResourcesController> logger,
            UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _fileStorage = fileStorage;
            _env = env;
            _logger = logger;
            _userManager = userManager;
        }

        // ---------------- Download ----------------
        // Students are allowed to download — the app enforces access to paid lessons elsewhere.
        [HttpGet]
        [Authorize(Roles = "Admin,Teacher,Student")]
        public async Task<IActionResult> Download(int id, CancellationToken cancellationToken = default)
        {
            // Load the FileResource and related lesson/course ownership chains so we can log/verify if needed
            var file = await _db.FileResources
                .AsNoTracking()
                .Where(f => f.Id == id)
                .Include(f => f.PrivateLesson)
                    .ThenInclude(pl => pl.PrivateCourse)
                        .ThenInclude(pc => pc.Teacher)
                            .ThenInclude(t => t.User)
                .Include(f => f.ReactiveCourseLesson)
                    .ThenInclude(rl => rl.ReactiveCourseMonth)
                        .ThenInclude(m => m.ReactiveCourse)
                            .ThenInclude(rc => rc.Teacher)
                                .ThenInclude(t => t.User)
                .Include(f => f.OnlineCourseLesson)
                    .ThenInclude(ol => ol.OnlineCourse)
                .Include(f => f.SchoolLesson)
                .FirstOrDefaultAsync(cancellationToken);

            if (file == null) return NotFound();

            // Authorization: by request, being a Student is sufficient to allow download.
            // Still log user role/ownership info for auditing.
            var currentUserId = User.Identity?.IsAuthenticated == true ? _userManager.GetUserId(User) : null;
            var roles = new[] {
                User.IsInRole("Admin") ? "Admin" : null,
                User.IsInRole("Teacher") ? "Teacher" : null,
                User.IsInRole("Student") ? "Student" : null
            }.Where(r => r != null).ToArray();

            _logger.LogDebug("FileResource {Id} download requested by user {User} roles: {Roles}", id, currentUserId ?? "anon", string.Join(',', roles));

            // Fetch storage key or url
            var keyOrUrl = !string.IsNullOrWhiteSpace(file.StorageKey) ? file.StorageKey : file.FileUrl;
            if (string.IsNullOrWhiteSpace(keyOrUrl))
            {
                _logger.LogWarning("FileResource {Id} has no StorageKey or FileUrl", id);
                return NotFound();
            }

            try
            {
                // 1) Try provider public URL (SAS/CDN)
                string? publicUrl = null;
                try
                {
                    publicUrl = await _fileStorage.GetPublicUrlAsync(keyOrUrl, TimeSpan.FromMinutes(15));
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "GetPublicUrlAsync failed for {Key}", keyOrUrl);
                    publicUrl = null;
                }

                if (!string.IsNullOrWhiteSpace(publicUrl) && Uri.TryCreate(publicUrl, UriKind.Absolute, out var pubUri)
                    && (pubUri.Scheme == Uri.UriSchemeHttp || pubUri.Scheme == Uri.UriSchemeHttps))
                {
                    // redirect to public URL (CDN or SAS)
                    return Redirect(publicUrl);
                }

                // 2) Try to stream from storage
                Stream? stream = null;
                try
                {
                    stream = await _fileStorage.OpenReadAsync(keyOrUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "OpenReadAsync failed for {Key}", keyOrUrl);
                    stream = null;
                }

                if (stream != null)
                {
                    var fileName = string.IsNullOrEmpty(file.Name) ? Path.GetFileName(keyOrUrl) : file.Name;
                    var contentType = file.FileType;
                    if (string.IsNullOrEmpty(contentType))
                    {
                        if (!s_contentTypeProvider.TryGetContentType(fileName, out var ct)) ct = "application/octet-stream";
                        contentType = ct;
                    }

                    // Set content-disposition for safe unicode filename (RFC5987)
                    Response.Headers["Content-Disposition"] = $"attachment; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";

                    return File(stream, contentType);
                }

                // 3) Try to serve local wwwroot file (relative path)
                var rel = keyOrUrl;
                if (rel.StartsWith("~/")) rel = rel.Substring(2);
                if (rel.StartsWith("/")) rel = rel.Substring(1);

                var physical = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, rel.Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(physical))
                {
                    var fileName = string.IsNullOrEmpty(file.Name) ? Path.GetFileName(physical) : file.Name;
                    if (!s_contentTypeProvider.TryGetContentType(fileName, out var kind)) kind = file.FileType ?? "application/octet-stream";
                    return PhysicalFile(physical, kind, fileName);
                }

                // 4) fallback: if keyOrUrl is absolute url, redirect
                if (Uri.TryCreate(keyOrUrl, UriKind.Absolute, out var fallbackUri)
                    && (fallbackUri.Scheme == Uri.UriSchemeHttp || fallbackUri.Scheme == Uri.UriSchemeHttps))
                {
                    return Redirect(keyOrUrl);
                }

                _logger.LogWarning("FileResource {Id} could not be resolved to a readable resource: {KeyOrUrl}", id, keyOrUrl);
                return NotFound();
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Download for FileResource {Id} cancelled by client", id);
                return new StatusCodeResult(499); // client closed request
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while downloading FileResource {Id}", id);
                return StatusCode(500, "Error while downloading file.");
            }
        }

        // ---------------- Delete ----------------
        // Only Admin or Teacher are allowed to delete. Teachers can delete files for their own PrivateLesson/ReactiveCourseLesson.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default)
        {
            var file = await _db.FileResources
                .Include(f => f.PrivateLesson).ThenInclude(pl => pl.PrivateCourse).ThenInclude(pc => pc.Teacher).ThenInclude(t => t.User)
                .Include(f => f.ReactiveCourseLesson).ThenInclude(rl => rl.ReactiveCourseMonth).ThenInclude(m => m.ReactiveCourse).ThenInclude(rc => rc.Teacher).ThenInclude(t => t.User)
                .Include(f => f.OnlineCourseLesson)
                .Include(f => f.SchoolLesson)
                .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

            if (file == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");
            var isTeacher = User.IsInRole("Teacher");

            bool allowedToDelete = false;

            if (isAdmin)
            {
                // Admin may delete any file (Admin manages SchoolLesson and OnlineCourseLesson)
                allowedToDelete = true;
            }
            else if (isTeacher)
            {
                // Teacher may delete private lesson files if they own the private course
                if (file.PrivateLesson != null && file.PrivateLesson.PrivateCourse?.Teacher?.User?.Id == currentUserId)
                {
                    allowedToDelete = true;
                }

                // Teacher may delete reactive lesson files if they own the reactive course
                if (!allowedToDelete && file.ReactiveCourseLesson != null)
                {
                    var teacherId = file.ReactiveCourseLesson?.ReactiveCourseMonth?.ReactiveCourse?.Teacher?.User?.Id;
                    if (!string.IsNullOrEmpty(teacherId) && teacherId == currentUserId) allowedToDelete = true;
                }

                // NOTE: by policy teachers do not delete SchoolLesson or OnlineCourseLesson (Admin does).
            }

            if (!allowedToDelete)
            {
                _logger.LogInformation("Delete forbidden for user {User} on FileResource {Id}", currentUserId, id);
                return Forbid();
            }

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
                        // log and continue removing DB record
                        _logger.LogWarning(ex, "Failed to delete file from storage for FileResource {Id}", id);
                    }
                }

                _db.FileResources.Remove(file);
                await _db.SaveChangesAsync(cancellationToken);

                TempData["Success"] = "FileResource.Deleted";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete FileResource {Id}", id);
                TempData["Error"] = "FileResource.DeleteFailed";
                return RedirectToAction(nameof(Index));
            }
        }

        // ---------------- Index (list) ----------------
        public async Task<IActionResult> Index(int? privateLessonId, int? schoolLessonId, CancellationToken cancellationToken = default)
        {
            ViewData["ActivePage"] = "PrivateCourses";

            var q = _db.FileResources
                .AsNoTracking()
                .Include(f => f.PrivateLesson)
                .Include(f => f.SchoolLesson)
                .Include(f => f.OnlineCourseLesson)
                .Include(f => f.ReactiveCourseLesson)
                .AsQueryable();

            if (privateLessonId.HasValue) q = q.Where(f => f.PrivateLessonId == privateLessonId.Value);
            if (schoolLessonId.HasValue) q = q.Where(f => f.SchoolLessonId == schoolLessonId.Value);

            var list = await q.OrderByDescending(f => f.Id).ToListAsync(cancellationToken);
            return View(list);
        }
    }
}




