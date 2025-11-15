using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Web.Areas.Teacher.ViewModels;
using Edu.Web.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Edu.Web.Areas.Teacher.Controllers
{
    [Area("Teacher")]
    [Authorize(Roles = "Teacher")]
    public class PrivateCoursesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileStorageService _fileStorage;
        private readonly IWebHostEnvironment _env;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly ILogger<PrivateCoursesController> _logger;

        public PrivateCoursesController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IFileStorageService fileStorage,
            IWebHostEnvironment env,
            IStringLocalizer<SharedResource> localizer,
            ILogger<PrivateCoursesController> logger)
        {
            _db = db;
            _userManager = userManager;
            _fileStorage = fileStorage;
            _env = env;
            _localizer = localizer;
            _logger = logger;
        }

        // GET: Teacher/Courses
        // GET: Teacher/Courses
        public async Task<IActionResult> Index(string? q, int? categoryId, bool? showPublished, int page = 1)
        {
            const int PageSize = 12;
            ViewData["Title"] = _localizer["Nav.TeacherDashboard"];
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var baseQuery = _db.PrivateCourses
                .AsNoTracking()
                .Where(pc => pc.TeacherId == user.Id)
                .Include(pc => pc.Category)
                .AsQueryable();

            if (categoryId.HasValue)
                baseQuery = baseQuery.Where(pc => pc.CategoryId == categoryId.Value);

            if (showPublished.HasValue)
                baseQuery = baseQuery.Where(pc => pc.IsPublished == showPublished.Value);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                baseQuery = baseQuery.Where(pc =>
                    (pc.Title != null && EF.Functions.Like(pc.Title, $"%{term}%")) ||
                    (pc.Description != null && EF.Functions.Like(pc.Description, $"%{term}%"))
                );
            }

            baseQuery = baseQuery.OrderByDescending(pc => pc.Id);

            var projected = baseQuery.Select(pc => new TeacherCourseListItemVm
            {
                Id = pc.Id,
                Title = pc.Title,
                CategoryId = pc.CategoryId,
                CategoryName = pc.Category != null ? pc.Category.Name : null,
                Price = pc.Price,
                PriceLabel = pc.Price.ToEuro(),
                IsPublished = pc.IsPublished,
                CoverImageKey = pc.CoverImageUrl, // storage key in DB
                ModuleCount = pc.PrivateModules != null ? pc.PrivateModules.Count() : 0,
                LessonCount = pc.PrivateLessons != null ? pc.PrivateLessons.Count() : 0
            });

            var paged = await PaginatedList<TeacherCourseListItemVm>.CreateAsync(projected, page, PageSize);

            // Resolve PublicCoverUrl for the page items (efficient: only distinct keys from current page)
            var keys = paged.Where(x => !string.IsNullOrEmpty(x.CoverImageKey))
                            .Select(x => x.CoverImageKey)
                            .Distinct()
                            .ToList();

            if (keys.Any())
            {
                var resolved = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (var key in keys)
                {
                    try
                    {
                        resolved[key!] = await _fileStorage.GetPublicUrlAsync(key!);
                    }
                    catch
                    {
                        resolved[key!] = null;
                    }
                }

                foreach (var item in paged)
                {
                    if (!string.IsNullOrEmpty(item.CoverImageKey) && resolved.TryGetValue(item.CoverImageKey!, out var pub))
                        item.PublicCoverUrl = pub;
                }
            }

            // categories for filter
            var categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.CategorySelect = new SelectList(categories, "Id", "Name", categoryId);

            ViewBag.ShowPublishedSelect = new List<SelectListItem>
    {
       new SelectListItem { Value = "", Text = _localizer["Common.All"], Selected = showPublished == null },
       new SelectListItem { Value = "true", Text = _localizer["Status.Accepted"], Selected = showPublished == true },
       new SelectListItem { Value = "false", Text = _localizer["Status.Rejected"], Selected = showPublished == false }
    };

            var vm = new TeacherCourseIndexVm
            {
                Query = q,
                CategoryId = categoryId,
                ShowPublished = showPublished,
                Courses = paged
            };

            ViewData["ActivePage"] = "MyPrivateCourses";
            return View(vm);
        }

        // GET: Teacher/Courses/Create
        // GET: Teacher/Courses/Create
        public async Task<IActionResult> Create()
        {
            var categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.CategorySelect = new SelectList(categories, "Id", "Name");
            return View(new TeacherCourseCreateVm());
        }

        // POST: Teacher/Courses/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TeacherCourseCreateVm vm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.CategorySelect = new SelectList(categories, "Id", "Name", vm.CategoryId);

            if (!ModelState.IsValid) return View(vm);

            string? coverKey = null;
            if (vm.CoverImage != null)
            {
                try
                {
                    var folder = "private-covers";
                    coverKey = await _fileStorage.SaveFileAsync(vm.CoverImage, folder);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save cover image for teacher {UserId}", user.Id);
                    ModelState.AddModelError("", _localizer["InvalidImage"]);
                    return View(vm);
                }
            }

            var course = new PrivateCourse
            {
                TeacherId = user.Id,
                CategoryId = vm.CategoryId ?? 0,
                Title = vm.Title,
                Description = vm.Description,
                CoverImageUrl = coverKey, // store the key
                Price = vm.Price,
                IsPublished = false,
                IsPublishRequested = false
            };

            _db.PrivateCourses.Add(course);
            await _db.SaveChangesAsync();

            TempData["Success"] = _localizer["Admin.Create"].Value + " " + _localizer["Common.Success"].Value;
            ViewData["ActivePage"] = "MyPrivateCourses";
            return RedirectToAction(nameof(Index));
        }


        // GET: Teacher/Courses/Edit/5
        // GET: Teacher/Courses/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var course = await _db.PrivateCourses.AsNoTracking().FirstOrDefaultAsync(pc => pc.Id == id && pc.TeacherId == user.Id);
            if (course == null) return NotFound();

            var vm = new TeacherCourseEditVm
            {
                Id = course.Id,
                CategoryId = course.CategoryId,
                Title = course.Title,
                Description = course.Description,
                Price = course.Price,
                ExistingCoverKey = course.CoverImageUrl
            };

            if (!string.IsNullOrEmpty(vm.ExistingCoverKey))
            {
                try
                {
                    vm.ExistingCoverPublicUrl = await _fileStorage.GetPublicUrlAsync(vm.ExistingCoverKey);
                }
                catch
                {
                    vm.ExistingCoverPublicUrl = null;
                }
            }

            var categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.CategorySelect = new SelectList(categories, "Id", "Name", vm.CategoryId);
            ViewData["ActivePage"] = "MyPrivateCourses";
            return View(vm);
        }

        // POST: Teacher/Courses/Edit/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TeacherCourseEditVm vm)
        {
            if (id != vm.Id) return BadRequest();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var course = await _db.PrivateCourses.FirstOrDefaultAsync(pc => pc.Id == id && pc.TeacherId == user.Id);
            if (course == null) return NotFound();

            var categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.CategorySelect = new SelectList(categories, "Id", "Name", vm.CategoryId);

            if (!ModelState.IsValid) return View(vm);

            // cover replacement
            if (vm.CoverImage != null)
            {
                try
                {
                    var folder = "private-covers";
                    var newKey = await _fileStorage.SaveFileAsync(vm.CoverImage, folder);

                    // best-effort delete previous key
                    if (!string.IsNullOrEmpty(course.CoverImageUrl))
                    {
                        try { await _fileStorage.DeleteFileAsync(course.CoverImageUrl); } catch { /* ignore */ }
                    }

                    course.CoverImageUrl = newKey;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save cover image for course {CourseId}", course.Id);
                    ModelState.AddModelError("", _localizer["InvalidImage"]);
                    return View(vm);
                }
            }

            // update other fields
            course.Title = vm.Title;
            course.Description = vm.Description;
            course.CategoryId = vm.CategoryId ?? course.CategoryId;
            course.Price = vm.Price;

            await _db.SaveChangesAsync();

            TempData["Success"] = _localizer["ProfileSaved"].Value;
            ViewData["ActivePage"] = "MyPrivateCourses";
            return RedirectToAction(nameof(Index));
        }

        // POST: Teacher/Courses/RequestPublish/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestPublish(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var course = await _db.PrivateCourses.FindAsync(id);
            if (course == null) return NotFound();
            if (course.TeacherId != user.Id) return Forbid();

            // set request true
            course.IsPublishRequested = true;

            _db.CourseModerationLogs.Add(new CourseModerationLog
            {
                PrivateCourseId = course.Id,
                AdminId = user.Id, // actor is the teacher
                Action = "PublishRequested",
                Note = null,
                CreatedAtUtc = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            TempData["Success"] = _localizer["Admin.PublishRequestSent"].Value ?? "Publish request sent.";
            return RedirectToAction("Details", new { id });
        }

        // POST: Teacher/Courses/CancelRequestPublish/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelRequestPublish(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var course = await _db.PrivateCourses.FindAsync(id);
            if (course == null) return NotFound();
            if (course.TeacherId != user.Id) return Forbid();

            if (!course.IsPublishRequested)
            {
                TempData["Info"] = _localizer["Admin.PublishRequestNotFound"].Value ?? "No publish request to cancel.";
                return RedirectToAction("Details", new { id });
            }

            course.IsPublishRequested = false;

            _db.CourseModerationLogs.Add(new CourseModerationLog
            {
                PrivateCourseId = course.Id,
                AdminId = user.Id, // actor = teacher
                Action = "PublishRequestCancelledByTeacher",
                Note = null,
                CreatedAtUtc = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            TempData["Success"] = _localizer["Admin.PublishRequestCancelled"].Value ?? "Publish request cancelled.";
            return RedirectToAction("Details", new { id });
        }


        // GET: Teacher/Courses/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            // load course + modules + lessons + files + category
            var course = await _db.PrivateCourses
                                  .AsNoTracking()
                                  .Include(c => c.Category)
                                  .Include(c => c.PrivateModules)
                                  .Include(c => c.PrivateLessons) // includes lessons, but not files; we will load files separately
                                  .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null) return NotFound();

            // Load lessons with files in one round-trip if possible
            var lessonsWithFiles = await _db.PrivateLessons
                                            .AsNoTracking()
                                            .Where(l => l.PrivateCourseId == id)
                                            .Include(l => l.Files)
                                            .ToListAsync();

            // Map to ViewModel
            var vm = new TeacherCourseDetailsVm
            {
                Id = course.Id,
                Title = course.Title,
                Description = course.Description,
                CoverImageKey = course.CoverImageUrl,
                PriceLabel = course.Price.ToEuro(),
                IsPublished = course.IsPublished,
                IsPublishRequested = course.IsPublishRequested,
                CategoryId = course.CategoryId,
                CategoryName = course.Category?.Name,
                TeacherId = course.TeacherId
            };

            // Modules summary
            var modules = course.PrivateModules != null
                          ? course.PrivateModules.OrderBy(m => m.Order).ToList()
                          : new System.Collections.Generic.List<PrivateModule>();

            foreach (var m in modules)
            {
                var lessonCount = lessonsWithFiles.Count(l => l.PrivateModuleId == m.Id);
                vm.Modules.Add(new ModuleSummaryVm
                {
                    Id = m.Id,
                    Title = m.Title,
                    Order = m.Order,
                    LessonCount = lessonCount
                });
            }

            // Lessons
            foreach (var l in lessonsWithFiles.OrderBy(l => l.Order))
            {
                var lessonVm = new PrivateLessonVm
                {
                    Id = l.Id,
                    PrivateCourseId = l.PrivateCourseId,
                    PrivateModuleId = l.PrivateModuleId,
                    Title = l.Title,
                    YouTubeVideoId = l.YouTubeVideoId,
                    VideoUrl = l.VideoUrl,
                    Order = l.Order
                };

                // files
                if (l.Files != null)
                {
                    foreach (var f in l.Files)
                    {
                        lessonVm.Files.Add(new FileResourceVm
                        {
                            Id = f.Id,
                            Name = f.Name,
                            FileType = f.FileType,
                            FileUrl = f.FileUrl,
                            StorageKey = f.StorageKey
                        });
                    }
                }

                vm.Lessons.Add(lessonVm);
            }

            // Build LessonsByModule dictionary for quick rendering in view
            // use key = moduleId (int) or 0 for no module
            var dict = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<PrivateLessonVm>>();
            foreach (var lesson in vm.Lessons)
            {
                var key = lesson.PrivateModuleId ?? 0;
                if (!dict.ContainsKey(key)) dict[key] = new System.Collections.Generic.List<PrivateLessonVm>();
                dict[key].Add(lesson);
            }
            vm.LessonsByModule = dict;

            // IsOwner (important)
            var currentUser = await _userManager.GetUserAsync(User);
            vm.IsOwner = currentUser != null && !string.IsNullOrEmpty(course.TeacherId) && currentUser.Id == course.TeacherId;

            // (Optional) load moderation logs if you have CourseModerationLog table
            // vm.ModerationLogs = ... populate if available

            ViewData["ActivePage"] = "MyPrivateCourses";
            return View(vm);
        }

        // POST: Teacher/Courses/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var course = await _db.PrivateCourses
                .Include(pc => pc.PrivateLessons)
                    .ThenInclude(l => l.Files)
                .FirstOrDefaultAsync(pc => pc.Id == id && pc.TeacherId == user.Id);

            if (course == null) return NotFound();

            // Attempt to delete files from storage (best-effort)
            try
            {
                // delete cover
                if (!string.IsNullOrEmpty(course.CoverImageUrl))
                {
                    try { await _fileStorage.DeleteFileAsync(course.CoverImageUrl); } catch { /*ignore*/ }
                }

                // delete lesson files
                if (course.PrivateLessons != null)
                {
                    foreach (var lesson in course.PrivateLessons)
                    {
                        if (lesson.Files != null)
                        {
                            foreach (var f in lesson.Files)
                            {
                                try { await _fileStorage.DeleteFileAsync(f.StorageKey ?? f.FileUrl); } catch { /*ignore*/ }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while deleting files for course {CourseId}", course.Id);
            }

            _db.PrivateCourses.Remove(course);
            await _db.SaveChangesAsync();

            TempData["Success"] = _localizer["Admin.Delete"].Value + " " + _localizer["Common.Success"].Value;
            return RedirectToAction(nameof(Index));
        }
    }
}

