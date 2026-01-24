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
    [Authorize]
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

        // GET: Admin/FileResources/GetDownloadUrl?id=123
        [HttpGet]
        [Authorize(Roles = "Admin,Teacher,Student")]
        public async Task<IActionResult> GetDownloadUrl(int id, CancellationToken cancellationToken = default)
        {
            var file = await _db.FileResources
                .AsNoTracking()
                .Where(f => f.Id == id)
                .FirstOrDefaultAsync(cancellationToken);

            if (file == null) return NotFound(new { success = false, message = "File not found" });

            // pick storage key or file url
            var keyOrUrl = !string.IsNullOrWhiteSpace(file.StorageKey) ? file.StorageKey : file.FileUrl;
            if (string.IsNullOrWhiteSpace(keyOrUrl))
                return NotFound(new { success = false, message = "No storage key or url" });

            string? publicUrl = null;
            try
            {
                try
                {
                    // Ask storage for public URL (provider may return absolute URL or a virtual path)
                    publicUrl = await _fileStorage.GetPublicUrlAsync(keyOrUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "GetPublicUrlAsync failed for key {Key}", keyOrUrl);
                    publicUrl = null;
                }

                if (!string.IsNullOrWhiteSpace(publicUrl))
                {
                    // Normalize provider returned virtual path to app root (avoid nested relative paths)
                    if (publicUrl.StartsWith("~"))
                        publicUrl = Url.Content(publicUrl);
                    else if (!publicUrl.StartsWith("/") && !Uri.TryCreate(publicUrl, UriKind.Absolute, out _))
                        publicUrl = "/" + publicUrl.TrimStart('/');
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed resolving public url for file {Id}", id);
                publicUrl = null;
            }

            var downloadUrl = Url.Action(nameof(Download), "FileResources", new { area = "Admin", id = id });

            return Ok(new { success = true, publicUrl = publicUrl, downloadUrl = downloadUrl });
        }
        // GET: Admin/FileResources/Download/5
        [HttpGet]
        [Authorize(Roles = "Admin,Teacher,Student")]
        public async Task<IActionResult> Download(int id, CancellationToken cancellationToken = default)
        {
            // load file and relevant ownership info for logging/authorization checks if needed
            var file = await _db.FileResources
                .AsNoTracking()
                .Where(f => f.Id == id)
                .Include(f => f.PrivateLesson).ThenInclude(pl => pl.PrivateCourse).ThenInclude(pc => pc.Teacher).ThenInclude(t => t.User)
                .Include(f => f.ReactiveCourseLesson).ThenInclude(rl => rl.ReactiveCourseMonth).ThenInclude(m => m.ReactiveCourse).ThenInclude(rc => rc.Teacher).ThenInclude(t => t.User)
                .Include(f => f.OnlineCourseLesson).ThenInclude(ol => ol.OnlineCourse)
                .Include(f => f.SchoolLesson)
                .FirstOrDefaultAsync(cancellationToken);

            if (file == null) return NotFound();

            var currentUserId = User.Identity?.IsAuthenticated == true ? _userManager.GetUserId(User) : null;
            var roles = new[] {
        User.IsInRole("Admin") ? "Admin" : null,
        User.IsInRole("Teacher") ? "Teacher" : null,
        User.IsInRole("Student") ? "Student" : null
    }.Where(r => r != null).ToArray();

            _logger.LogDebug("FileResource {Id} download requested by user {User} roles: {Roles}", id, currentUserId ?? "anon", string.Join(',', roles));

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
                    // pass an expiry only when you want a short-lived link; null/overload may be supported by your implementation
                    publicUrl = await _fileStorage.GetPublicUrlAsync(keyOrUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "GetPublicUrlAsync failed for {Key}", keyOrUrl);
                    publicUrl = null;
                }

                if (!string.IsNullOrWhiteSpace(publicUrl) && Uri.TryCreate(publicUrl, UriKind.Absolute, out var pubUri)
                    && (pubUri.Scheme == Uri.UriSchemeHttp || pubUri.Scheme == Uri.UriSchemeHttps))
                {
                    // redirect to provider public URL (fast, avoids server bandwidth)
                    return Redirect(publicUrl);
                }

                // 2) Try to stream from storage (if storage supports streaming)
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
                        var provider = new FileExtensionContentTypeProvider();
                        if (!provider.TryGetContentType(fileName, out var ct)) ct = "application/octet-stream";
                        contentType = ct;
                    }

                    // safe filename header for unicode
                    Response.Headers["Content-Disposition"] = $"attachment; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";

                    return File(stream, contentType);
                }

                // 3) Try to serve local wwwroot file (if keyOrUrl is a relative path like "uploads/...")
                var rel = keyOrUrl;
                if (rel.StartsWith("~/")) rel = rel.Substring(2);
                if (rel.StartsWith("/")) rel = rel.Substring(1);

                var physical = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, rel.Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(physical))
                {
                    var fileName = string.IsNullOrEmpty(file.Name) ? Path.GetFileName(physical) : file.Name;
                    var provider = new FileExtensionContentTypeProvider();
                    if (!provider.TryGetContentType(fileName, out var kind)) kind = file.FileType ?? "application/octet-stream";
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

        // GET: Admin/FileResources/GetFiles?schoolLessonId=5
        [HttpGet]
        [Authorize(Roles = "Admin,Teacher,Student")]
        public async Task<IActionResult> GetFiles(int? schoolLessonId = null, int? privateLessonId = null, int? onlineCourseLessonId = null, int? reactiveCourseLessonId = null, CancellationToken cancellationToken = default)
        {
            // Validate exactly one id supplied
            var provided = new[] { schoolLessonId.HasValue, privateLessonId.HasValue, onlineCourseLessonId.HasValue, reactiveCourseLessonId.HasValue }.Count(b => b);
            if (provided != 1)
            {
                return BadRequest(new { success = false, message = "Provide exactly one id parameter." });
            }

            IQueryable<FileResource> q = _db.FileResources.AsNoTracking();

            if (schoolLessonId.HasValue) q = q.Where(f => f.SchoolLessonId == schoolLessonId.Value);
            else if (privateLessonId.HasValue) q = q.Where(f => f.PrivateLessonId == privateLessonId.Value);
            else if (onlineCourseLessonId.HasValue) q = q.Where(f => f.OnlineCourseLessonId == onlineCourseLessonId.Value);
            else if (reactiveCourseLessonId.HasValue) q = q.Where(f => f.ReactiveCourseLessonId == reactiveCourseLessonId.Value);

            var files = await q.Select(f => new { f.Id, f.Name, f.FileUrl, f.StorageKey, f.FileType }).ToListAsync(cancellationToken);

            if (!files.Any()) return Ok(new { success = true, files = new object[0] });

            // Resolve distinct storage keys in batch
            var keysToResolve = files.Select(f => f.StorageKey).Where(k => !string.IsNullOrWhiteSpace(k)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var keyToPublic = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            if (keysToResolve.Any())
            {
                try
                {
                    var tasks = keysToResolve.Select(k => _fileStorage.GetPublicUrlAsync(k)).ToArray();
                    var urls = await Task.WhenAll(tasks);
                    keyToPublic = keysToResolve.Zip(urls, (k, u) => (k, u)).ToDictionary(x => x.k, x => x.u, StringComparer.OrdinalIgnoreCase);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed resolving some storage keys for GetFiles");
                }
            }

            var result = files.Select(f =>
            {
                string? publicUrl = null;
                if (!string.IsNullOrEmpty(f.StorageKey) && keyToPublic.TryGetValue(f.StorageKey!, out var resolved))
                    publicUrl = resolved;

                if (string.IsNullOrEmpty(publicUrl))
                    publicUrl = f.FileUrl;

                if (!string.IsNullOrEmpty(publicUrl))
                {
                    if (publicUrl.StartsWith("~")) publicUrl = Url.Content(publicUrl);
                    else if (!publicUrl.StartsWith("/") && !Uri.TryCreate(publicUrl, UriKind.Absolute, out _)) publicUrl = "/" + publicUrl.TrimStart('/');
                }

                return new
                {
                    id = f.Id,
                    name = f.Name,
                    fileType = f.FileType,
                    publicUrl = publicUrl,
                    downloadUrl = Url.Action(nameof(Download), "FileResources", new { area = "Admin", id = f.Id })
                };
            }).ToList();

            return Ok(new { success = true, files = result });
        }

        // POST JSON to delete (AJAX)
        public class DeleteAjaxRequest { public int Id { get; set; } public string? ReturnUrl { get; set; } }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> DeleteAjax(DeleteAjaxRequest req, CancellationToken cancellationToken = default)
        {
            if (req == null) return BadRequest(new { success = false, message = "Invalid request" });

            var id = req.Id;
            var returnUrl = req.ReturnUrl;

            var file = await _db.FileResources
                .Include(f => f.PrivateLesson).ThenInclude(pl => pl.PrivateCourse).ThenInclude(pc => pc.Teacher).ThenInclude(t => t.User)
                .Include(f => f.ReactiveCourseLesson).ThenInclude(rl => rl.ReactiveCourseMonth).ThenInclude(m => m.ReactiveCourse).ThenInclude(rc => rc.Teacher).ThenInclude(t => t.User)
                .Include(f => f.OnlineCourseLesson).ThenInclude(ol => ol.OnlineCourse)
                .Include(f => f.SchoolLesson)
                .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

            if (file == null) return NotFound(new { success = false, message = "File not found" });

            var currentUserId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");
            var isTeacher = User.IsInRole("Teacher");

            bool allowedToDelete = false;
            if (isAdmin) allowedToDelete = true;
            else if (isTeacher)
            {
                if (file.PrivateLesson != null && file.PrivateLesson.PrivateCourse?.Teacher?.User?.Id == currentUserId) allowedToDelete = true;
                if (!allowedToDelete && file.ReactiveCourseLesson != null)
                {
                    var teacherId = file.ReactiveCourseLesson?.ReactiveCourseMonth?.ReactiveCourse?.Teacher?.User?.Id;
                    if (!string.IsNullOrEmpty(teacherId) && teacherId == currentUserId) allowedToDelete = true;
                }
            }

            if (!allowedToDelete) return Forbid();

            try
            {
                var keyOrUrl = !string.IsNullOrWhiteSpace(file.StorageKey) ? file.StorageKey : file.FileUrl;
                if (!string.IsNullOrWhiteSpace(keyOrUrl))
                {
                    try { await _fileStorage.DeleteFileAsync(keyOrUrl); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete file from storage for FileResource {Id}", id); }
                }

                _db.FileResources.Remove(file);
                await _db.SaveChangesAsync(cancellationToken);

                return Ok(new { success = true, fileId = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete file {Id}", id);
                return StatusCode(500, new { success = false, message = "Failed to delete file" });
            }
        }
    }
}

// ---------------- Index (list) ----------------
//public async Task<IActionResult> Index(int? privateLessonId, int? schoolLessonId, CancellationToken cancellationToken = default)
//{
//    ViewData["ActivePage"] = "PrivateCourses";

//    var q = _db.FileResources
//        .AsNoTracking()
//        .Include(f => f.PrivateLesson)
//        .Include(f => f.SchoolLesson)
//        .Include(f => f.OnlineCourseLesson)
//        .Include(f => f.ReactiveCourseLesson)
//        .AsQueryable();

//    if (privateLessonId.HasValue) q = q.Where(f => f.PrivateLessonId == privateLessonId.Value);
//    if (schoolLessonId.HasValue) q = q.Where(f => f.SchoolLessonId == schoolLessonId.Value);

//    var list = await q.OrderByDescending(f => f.Id).ToListAsync(cancellationToken);
//    return View(list);
//}


