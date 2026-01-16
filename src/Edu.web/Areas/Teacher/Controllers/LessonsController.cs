using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Web.Areas.Teacher.ViewModels;
using Edu.Web.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Edu.Web.Areas.Teacher.Controllers
{
    [Area("Teacher")]
    [Authorize(Roles = "Teacher")]
    public class LessonsController : TeacherBaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileStorageService _fileStorage;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly ILogger<LessonsController> _logger;

        // File upload policy
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".zip", ".rar",
            ".jpg", ".jpeg", ".png", ".gif", ".txt", ".csv", ".mp4", ".mkv"
        };
        private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

        public LessonsController(ApplicationDbContext db,
                                 UserManager<ApplicationUser> userManager,
                                 IFileStorageService fileStorage,
                                 IStringLocalizer<SharedResource> localizer,
                                 ILogger<LessonsController> logger)
        {
            _db = db;
            _userManager = userManager;
            _fileStorage = fileStorage;
            _localizer = localizer;
            _logger = logger;
        }

        // GET: Teacher/Lessons/Create?privateCourseId={id}
        [HttpGet]
        public async Task<IActionResult> Create(int privateCourseId)
        {
            var teacherId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(teacherId)) return Challenge();

            // load just the course id + teacher id to check ownership (lightweight)
            var course = await _db.PrivateCourses
                                 .AsNoTracking()
                                 .Where(c => c.Id == privateCourseId)
                                 .Select(c => new { c.Id, c.TeacherId })
                                 .FirstOrDefaultAsync();
            if (course == null) return NotFound();
            if (!string.Equals(course.TeacherId, teacherId, StringComparison.Ordinal)) return Forbid();

            // load modules separately — cheaper than Include when only modules are needed
            var modules = await _db.PrivateModules
                                   .AsNoTracking()
                                   .Where(m => m.PrivateCourseId == privateCourseId)
                                   .OrderBy(m => m.Order)
                                   .Select(m => new { m.Id, m.Title })
                                   .ToListAsync();

            ViewBag.ModuleSelect = modules
                .Select(m => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(m.Title, m.Id.ToString()))
                .ToList();

            var vm = new LessonCreateVm { PrivateCourseId = privateCourseId };
            return View(vm);
        }

        // POST: Teacher/Lessons/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LessonCreateVm vm)
        {
            var teacherId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(teacherId)) return Challenge();

            // verify course and ownership (projection)
            var course = await _db.PrivateCourses
                                  .AsNoTracking()
                                  .Where(c => c.Id == vm.PrivateCourseId)
                                  .Select(c => new { c.Id, c.TeacherId })
                                  .FirstOrDefaultAsync();

            if (course == null) return NotFound();
            if (!string.Equals(course.TeacherId, teacherId, StringComparison.Ordinal)) return Forbid();

            // prepare modules for re-render if needed
            var modules = await _db.PrivateModules
                                   .AsNoTracking()
                                   .Where(m => m.PrivateCourseId == vm.PrivateCourseId)
                                   .OrderBy(m => m.Order)
                                   .Select(m => new { m.Id, m.Title })
                                   .ToListAsync();

            ViewBag.ModuleSelect = modules
                .Select(m => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(m.Title, m.Id.ToString()))
                .ToList();

            if (!ModelState.IsValid) return View(vm);

            // validate title length
            vm.Title = (vm.Title ?? string.Empty).Trim();
            if (vm.Title.Length == 0 || vm.Title.Length > 300)
            {
                ModelState.AddModelError(nameof(vm.Title), _localizer["Admin.InvalidTitle"].Value ?? "Invalid title.");
                return View(vm);
            }

            // extract YouTube id safely
            var youtubeId = YouTubeHelper.ExtractYouTubeId(vm.YouTubeUrl ?? string.Empty);

            var lesson = new PrivateLesson
            {
                PrivateCourseId = vm.PrivateCourseId,
                PrivateModuleId = vm.PrivateModuleId,
                Title = vm.Title,
                Order = vm.Order,
                VideoUrl = vm.YouTubeUrl,
                YouTubeVideoId = youtubeId
            };

            _db.PrivateLessons.Add(lesson);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create PrivateLesson for course {CourseId}", vm.PrivateCourseId);
                ModelState.AddModelError("", _localizer["Admin.OperationFailed"].Value ?? "Operation failed.");
                return View(vm);
            }

            // handle file uploads (if any)
            if (vm.Files != null && vm.Files.Length > 0)
            {
                foreach (var file in vm.Files)
                {
                    if (file == null || file.Length == 0) continue;

                    // size check
                    if (file.Length > MaxFileSizeBytes)
                    {
                        _logger.LogWarning("Rejected file (too large) for lesson {LessonId}: {FileName} ({Length} bytes)", lesson.Id, file.FileName, file.Length);
                        continue;
                    }

                    // extension check & sanitize name
                    var originalName = Path.GetFileName(file.FileName ?? "");
                    var ext = Path.GetExtension(originalName);
                    if (!string.IsNullOrEmpty(ext) && !AllowedExtensions.Contains(ext))
                    {
                        _logger.LogWarning("Rejected file (bad ext) for lesson {LessonId}: {FileName}", lesson.Id, originalName);
                        continue;
                    }

                    // shorten name to avoid DB column overflow
                    var safeName = originalName.Length > 200 ? originalName.Substring(0, 200) : originalName;

                    try
                    {
                        var folder = $"private-lessons/{lesson.Id}";
                        var storageKey = await _fileStorage.SaveFileAsync(file, folder);

                        var fr = new FileResource
                        {
                            PrivateLessonId = lesson.Id,
                            StorageKey = storageKey,
                            Name = safeName,
                            FileType = file.ContentType,
                            CreatedAtUtc = DateTime.UtcNow
                        };

                        _db.FileResources.Add(fr);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to save file for lesson {LessonId}", lesson.Id);
                        // continue saving other files
                    }
                }

                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save file resources for lesson {LessonId}", lesson.Id);
                }
            }

            // Use resource key in TempData — centralize localization in layout/partial.
            TempData["Success"] = "PrivateLesson.Created";

            return RedirectToAction("Details", "PrivateCourses", new { area = "Teacher", id = vm.PrivateCourseId });
        }

        // GET: Teacher/Lessons/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var teacherId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(teacherId)) return Challenge();

            // load lesson with course ownership check and modules separately
            var lesson = await _db.PrivateLessons
                                  .AsNoTracking()
                                  .Where(l => l.Id == id)
                                  .Select(l => new
                                  {
                                      l.Id,
                                      l.PrivateCourseId,
                                      l.PrivateModuleId,
                                      l.Title,
                                      l.VideoUrl,
                                      l.Order,
                                      CourseTeacherId = l.PrivateCourse != null ? l.PrivateCourse.TeacherId : null
                                  })
                                  .FirstOrDefaultAsync();

            if (lesson == null) return NotFound();
            if (!string.Equals(lesson.CourseTeacherId, teacherId, StringComparison.Ordinal)) return Forbid();

            var modules = await _db.PrivateModules
                                   .AsNoTracking()
                                   .Where(m => m.PrivateCourseId == lesson.PrivateCourseId)
                                   .OrderBy(m => m.Order)
                                   .Select(m => new { m.Id, m.Title })
                                   .ToListAsync();

            ViewBag.ModuleSelect = modules
                .Select(m => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(m.Title, m.Id.ToString()))
                .ToList();

            var vm = new LessonEditVm
            {
                Id = lesson.Id,
                PrivateCourseId = lesson.PrivateCourseId,
                PrivateModuleId = lesson.PrivateModuleId,
                Title = lesson.Title,
                YouTubeUrl = lesson.VideoUrl,
                Order = lesson.Order
            };

            return View(vm);
        }

        // POST: Teacher/Lessons/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(LessonEditVm vm)
        {
            var teacherId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(teacherId)) return Challenge();

            var lesson = await _db.PrivateLessons.Include(l => l.PrivateCourse).FirstOrDefaultAsync(l => l.Id == vm.Id);
            if (lesson == null) return NotFound();
            if (!string.Equals(lesson.PrivateCourse?.TeacherId, teacherId, StringComparison.Ordinal)) return Forbid();

            // prepare module select in case of validation error
            var modules = await _db.PrivateModules
                                   .AsNoTracking()
                                   .Where(m => m.PrivateCourseId == lesson.PrivateCourseId)
                                   .OrderBy(m => m.Order)
                                   .ToListAsync();

            ViewBag.ModuleSelect = modules.Select(m => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(m.Title, m.Id.ToString())).ToList();

            if (!ModelState.IsValid) return View(vm);

            // update lesson
            lesson.Title = (vm.Title ?? string.Empty).Trim();
            lesson.PrivateModuleId = vm.PrivateModuleId;
            lesson.Order = vm.Order;
            lesson.VideoUrl = vm.YouTubeUrl;
            lesson.YouTubeVideoId = YouTubeHelper.ExtractYouTubeId(vm.YouTubeUrl ?? string.Empty);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed updating lesson {LessonId}", lesson.Id);
                ModelState.AddModelError("", _localizer["Admin.OperationFailed"].Value ?? "Operation failed.");
                return View(vm);
            }

            // handle new file uploads (if any)
            if (vm.Files != null && vm.Files.Length > 0)
            {
                foreach (var file in vm.Files)
                {
                    if (file == null || file.Length == 0) continue;
                    if (file.Length > MaxFileSizeBytes) { _logger.LogWarning("Skipped oversized file for lesson {LessonId}", lesson.Id); continue; }

                    var originalName = Path.GetFileName(file.FileName ?? "");
                    var ext = Path.GetExtension(originalName);
                    if (!string.IsNullOrEmpty(ext) && !AllowedExtensions.Contains(ext)) { _logger.LogWarning("Skipped file with disallowed extension for lesson {LessonId}", lesson.Id); continue; }

                    var safeName = originalName.Length > 200 ? originalName.Substring(0, 200) : originalName;

                    try
                    {
                        var folder = $"private-lessons/{lesson.Id}";
                        var storageKey = await _fileStorage.SaveFileAsync(file, folder);
                        var fr = new FileResource
                        {
                            PrivateLessonId = lesson.Id,
                            StorageKey = storageKey,
                            Name = safeName,
                            FileType = file.ContentType,
                            CreatedAtUtc = DateTime.UtcNow
                        };
                        _db.FileResources.Add(fr);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to save file for lesson {LessonId}", lesson.Id);
                    }
                }
                try { await _db.SaveChangesAsync(); } catch (Exception ex) { _logger.LogError(ex, "Failed saving uploaded files for lesson {LessonId}", lesson.Id); }
            }

            TempData["Success"] = "PrivateLesson.Updated";
            return RedirectToAction("Details", "PrivateCourses", new { area = "Teacher", id = vm.PrivateCourseId });
        }

        // POST: Teacher/Lessons/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, int? courseId)
        {
            var teacherId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(teacherId)) return Challenge();

            var lesson = await _db.PrivateLessons
                                  .Include(l => l.PrivateCourse)
                                  .Include(l => l.Files)
                                  .FirstOrDefaultAsync(l => l.Id == id);

            if (lesson == null) return NotFound();
            if (!string.Equals(lesson.PrivateCourse?.TeacherId, teacherId, StringComparison.Ordinal)) return Forbid();

            // Attempt to delete associated files from storage (best-effort)
            if (lesson.Files != null)
            {
                foreach (var f in lesson.Files)
                {
                    var keyOrUrl = f.StorageKey ?? f.FileUrl;
                    if (string.IsNullOrWhiteSpace(keyOrUrl)) continue;
                    try
                    {
                        await _fileStorage.DeleteFileAsync(keyOrUrl);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete file resource {FileId} (key={Key})", f.Id, keyOrUrl);
                        // continue: still remove DB rows
                    }
                }
            }

            _db.PrivateLessons.Remove(lesson);
            try
            {
                await _db.SaveChangesAsync();
                TempData["Success"] = "PrivateLesson.Deleted";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed deleting lesson {LessonId}", id);
                TempData["Error"] = "PrivateLesson.DeleteFailed";
            }

            return RedirectToAction("Details", "PrivateCourses", new { area = "Teacher", id = courseId ?? lesson.PrivateCourseId });
        }

        // POST: Teacher/Lessons/UploadFiles
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadFiles(int lessonId)
        {
            var teacherId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(teacherId)) return Challenge();

            var lesson = await _db.PrivateLessons.Include(l => l.PrivateCourse).FirstOrDefaultAsync(l => l.Id == lessonId);
            if (lesson == null) return NotFound();
            if (!string.Equals(lesson.PrivateCourse?.TeacherId, teacherId, StringComparison.Ordinal)) return Forbid();

            var files = Request.Form.Files;
            if (files != null && files.Count > 0)
            {
                foreach (var file in files)
                {
                    if (file == null || file.Length == 0) continue;
                    if (file.Length > MaxFileSizeBytes) { _logger.LogWarning("Skipped oversized file for lesson {LessonId}", lesson.Id); continue; }

                    var originalName = Path.GetFileName(file.FileName ?? "");
                    var ext = Path.GetExtension(originalName);
                    if (!string.IsNullOrEmpty(ext) && !AllowedExtensions.Contains(ext)) { _logger.LogWarning("Skipped file with disallowed extension for lesson {LessonId}", lesson.Id); continue; }

                    var safeName = originalName.Length > 200 ? originalName.Substring(0, 200) : originalName;

                    try
                    {
                        var folder = $"private-lessons/{lesson.Id}";
                        var storageKey = await _fileStorage.SaveFileAsync(file, folder);

                        var fr = new FileResource
                        {
                            PrivateLessonId = lesson.Id,
                            StorageKey = storageKey,
                            Name = safeName,
                            FileType = file.ContentType,
                            CreatedAtUtc = DateTime.UtcNow
                        };
                        _db.FileResources.Add(fr);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to upload file for lesson {LessonId}", lesson.Id);
                    }
                }

                try
                {
                    await _db.SaveChangesAsync();
                    TempData["Success"] = "PrivateLesson.FilesUploaded";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed saving uploaded file records for lesson {LessonId}", lesson.Id);
                    TempData["Error"] = "PrivateLesson.FilesUploadFailed";
                }
            }

            return RedirectToAction("Details", "PrivateCourses", new { area = "Teacher", id = lesson.PrivateCourseId });
        }
    }
}




