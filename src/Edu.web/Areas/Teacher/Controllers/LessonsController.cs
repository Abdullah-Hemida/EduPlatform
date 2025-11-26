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
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var course = await _db.PrivateCourses.AsNoTracking()
                                .Include(c => c.PrivateModules)
                                .FirstOrDefaultAsync(c => c.Id == privateCourseId);

            if (course == null) return NotFound();
            if (course.TeacherId != user.Id) return Forbid();

            // Prepare module select list in ViewBag
            ViewBag.ModuleSelect = course.PrivateModules.OrderBy(m => m.Order).Select(m =>
                new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(m.Title, m.Id.ToString())).ToList();

            var vm = new LessonCreateVm { PrivateCourseId = privateCourseId };
            return View(vm);
        }

        // POST: Teacher/Lessons/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LessonCreateVm vm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var course = await _db.PrivateCourses.Include(c => c.PrivateModules).FirstOrDefaultAsync(c => c.Id == vm.PrivateCourseId);
            if (course == null) return NotFound();
            if (course.TeacherId != user.Id) return Forbid();

            // prepare module select again if we re-render the view
            ViewBag.ModuleSelect = course.PrivateModules.OrderBy(m => m.Order).Select(m =>
                new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(m.Title, m.Id.ToString())).ToList();

            if (!ModelState.IsValid) return View(vm);

            // fix youtube id
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
            await _db.SaveChangesAsync();

            // handle file uploads (if any)
            if (vm.Files != null && vm.Files.Length > 0)
            {
                foreach (var file in vm.Files)
                {
                    if (file == null || file.Length == 0) continue;

                    try
                    {
                        // You can add file extension & size checks here
                        var folder = $"private-lessons/{lesson.Id}";
                        var storageKey = await _fileStorage.SaveFileAsync(file, folder);

                        var fr = new FileResource
                        {
                            PrivateLessonId = lesson.Id,
                            StorageKey = storageKey,
                            Name = file.FileName,
                            FileType = file.ContentType
                        };

                        _db.FileResources.Add(fr);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to save file for lesson {LessonId}", lesson.Id);
                        // continue saving other files
                    }
                }
                await _db.SaveChangesAsync();
            }

            return RedirectToAction("Details", "PrivateCourses", new { area = "Teacher", id = vm.PrivateCourseId });
        }

        // GET: Teacher/Lessons/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var lesson = await _db.PrivateLessons
                                  .AsNoTracking()
                                  .Include(l => l.PrivateCourse)
                                  .Include(l => l.PrivateCourse.PrivateModules)
                                  .FirstOrDefaultAsync(l => l.Id == id);

            if (lesson == null) return NotFound();
            if (lesson.PrivateCourse?.TeacherId != user.Id) return Forbid();

            // modules select
            ViewBag.ModuleSelect = lesson.PrivateCourse.PrivateModules.OrderBy(m => m.Order).Select(m =>
                new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(m.Title, m.Id.ToString())).ToList();

            var vm = new LessonEditVm
            {
                Id = lesson.Id,
                PrivateCourseId = lesson.PrivateCourseId,
                PrivateModuleId = lesson.PrivateModuleId,
                Title = lesson.Title,
                Description = null, // you didn't include Description in PrivateLesson entity earlier, leave null or extend entity
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
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var lesson = await _db.PrivateLessons.Include(l => l.PrivateCourse).FirstOrDefaultAsync(l => l.Id == vm.Id);
            if (lesson == null) return NotFound();
            if (lesson.PrivateCourse?.TeacherId != user.Id) return Forbid();

            // prepare module select in case of validation error
            var modules = await _db.PrivateModules.Where(m => m.PrivateCourseId == lesson.PrivateCourseId).OrderBy(m => m.Order).ToListAsync();
            ViewBag.ModuleSelect = modules.Select(m => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(m.Title, m.Id.ToString())).ToList();

            if (!ModelState.IsValid) return View(vm);

            // update lesson
            lesson.Title = vm.Title;
            lesson.PrivateModuleId = vm.PrivateModuleId;
            lesson.Order = vm.Order;
            lesson.VideoUrl = vm.YouTubeUrl;
            lesson.YouTubeVideoId = YouTubeHelper.ExtractYouTubeId(vm.YouTubeUrl ?? string.Empty);

            await _db.SaveChangesAsync();

            // handle new files uploads
            if (vm.Files != null && vm.Files.Length > 0)
            {
                foreach (var file in vm.Files)
                {
                    if (file == null || file.Length == 0) continue;
                    try
                    {
                        var folder = $"private-lessons/{lesson.Id}";
                        var storageKey = await _fileStorage.SaveFileAsync(file, folder);

                        var fr = new FileResource
                        {
                            PrivateLessonId = lesson.Id,
                            StorageKey = storageKey,
                            Name = file.FileName,
                            FileType = file.ContentType
                        };
                        _db.FileResources.Add(fr);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to save file for lesson {LessonId}", lesson.Id);
                    }
                }
                await _db.SaveChangesAsync();
            }

            return RedirectToAction("Details", "PrivateCourses", new { area = "Teacher", id = vm.PrivateCourseId });
        }

        // POST: Teacher/Lessons/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, int? courseId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var lesson = await _db.PrivateLessons.Include(l => l.PrivateCourse).Include(l => l.Files).FirstOrDefaultAsync(l => l.Id == id);
            if (lesson == null) return NotFound();
            if (lesson.PrivateCourse?.TeacherId != user.Id) return Forbid();

            // Attempt to delete associated files from storage (best-effort)
            if (lesson.Files != null)
            {
                foreach (var f in lesson.Files)
                {
                    try
                    {
                        await _fileStorage.DeleteFileAsync(f.StorageKey ?? f.FileUrl);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete file resource {FileId}", f.Id);
                    }
                }
            }

            _db.PrivateLessons.Remove(lesson);
            await _db.SaveChangesAsync();

            return RedirectToAction("Details", "PrivateCourses", new { area = "Teacher", id = courseId ?? lesson.PrivateCourseId });
        }

        // Optional: UploadFiles endpoint for adding files to existing lesson
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadFiles(int lessonId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var lesson = await _db.PrivateLessons.Include(l => l.PrivateCourse).FirstOrDefaultAsync(l => l.Id == lessonId);
            if (lesson == null) return NotFound();
            if (lesson.PrivateCourse?.TeacherId != user.Id) return Forbid();

            var files = Request.Form.Files;
            if (files != null && files.Count > 0)
            {
                foreach (var file in files)
                {
                    if (file.Length == 0) continue;
                    try
                    {
                        var folder = $"private-lessons/{lesson.Id}";
                        var storageKey = await _fileStorage.SaveFileAsync(file, folder);

                        var fr = new FileResource
                        {
                            PrivateLessonId = lesson.Id,
                            StorageKey = storageKey,
                            Name = file.FileName,
                            FileType = file.ContentType
                        };
                        _db.FileResources.Add(fr);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to upload file for lesson {LessonId}", lesson.Id);
                    }
                }
                await _db.SaveChangesAsync();
            }

            return RedirectToAction("Details", "PrivateCourses", new { area = "Teacher", id = lesson.PrivateCourseId });
        }
    }
}



