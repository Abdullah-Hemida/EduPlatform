// File: Areas/Teacher/Controllers/PrivateCoursesController.cs
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
using Microsoft.Extensions.Caching.Memory;
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
        private readonly IMemoryCache _memoryCache;

        // cache key (bump when category schema changes)
        private const string CategoriesCacheKey_Teacher = "Teacher_PrivateCourses_Categories_v1";

        public PrivateCoursesController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IFileStorageService fileStorage,
            IWebHostEnvironment env,
            IStringLocalizer<SharedResource> localizer,
            ILogger<PrivateCoursesController> logger,
            IMemoryCache memoryCache)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _fileStorage = fileStorage ?? throw new ArgumentNullException(nameof(fileStorage));
            _env = env;
            _localizer = localizer;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        }

        // GET: Teacher/Courses
        public async Task<IActionResult> Index(string? q, int? categoryId, bool? showPublished, int page = 1)
        {
            const int PageSize = 12;
            ViewData["Title"] = _localizer["Nav.TeacherDashboard"];
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // build base query - no Includes required for projection
            var baseQuery = _db.PrivateCourses
                .AsNoTracking()
                .Where(pc => pc.TeacherId == user.Id)
                .AsQueryable();

            if (categoryId.HasValue && categoryId.Value > 0)
                baseQuery = baseQuery.Where(pc => pc.CategoryId == categoryId.Value);

            if (showPublished.HasValue)
                baseQuery = baseQuery.Where(pc => pc.IsPublished == showPublished.Value);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                baseQuery = baseQuery.Where(pc =>
                    (pc.Title != null && EF.Functions.Like(pc.Title, $"%{term}%")) ||
                    (pc.Description != null && EF.Functions.Like(pc.Description, $"%{term}%")) ||
                    // search across category language columns
                    (pc.Category != null &&
                        (
                            (pc.Category.NameEn != null && EF.Functions.Like(pc.Category.NameEn, $"%{term}%")) ||
                            (pc.Category.NameIt != null && EF.Functions.Like(pc.Category.NameIt, $"%{term}%")) ||
                            (pc.Category.NameAr != null && EF.Functions.Like(pc.Category.NameAr, $"%{term}%"))
                        )
                    )
                );
            }

            baseQuery = baseQuery.OrderByDescending(pc => pc.Id);

            // project to DTO including category language columns (no EF tracking)
            var projected = baseQuery.Select(pc => new TeacherCourseListItemVm
            {
                Id = pc.Id,
                Title = pc.Title,
                CategoryId = pc.CategoryId,
                CategoryNameEn = pc.Category != null ? pc.Category.NameEn : null,
                CategoryNameIt = pc.Category != null ? pc.Category.NameIt : null,
                CategoryNameAr = pc.Category != null ? pc.Category.NameAr : null,
                Price = pc.Price,
                PriceLabel = pc.Price.ToEuro(),
                IsPublished = pc.IsPublished,
                CoverImageKey = pc.CoverImageKey,
                ModuleCount = pc.PrivateModules.Count(),
                LessonCount = pc.PrivateLessons.Count()
            });

            var paged = await PaginatedList<TeacherCourseListItemVm>.CreateAsync(projected, page, PageSize);

            // compute localized category names (after materialization) and batch-resolve cover URLs
            if (paged != null && paged.Any())
            {
                // localized category name
                foreach (var it in paged)
                {
                    it.CategoryName = LocalizationHelpers.GetLocalizedCategoryName(new Category
                    {
                        NameEn = it.CategoryNameEn ?? string.Empty,
                        NameIt = it.CategoryNameIt ?? string.Empty,
                        NameAr = it.CategoryNameAr ?? string.Empty
                    });
                }

                // resolve cover keys
                var keys = paged.Where(x => !string.IsNullOrEmpty(x.CoverImageKey))
                                .Select(x => x.CoverImageKey!)
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList();

                if (keys.Any())
                {
                    var tasks = keys.ToDictionary(k => k, k => Task.Run(async () =>
                    {
                        try
                        {
                            return await _fileStorage.GetPublicUrlAsync(k);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Failed to resolve cover public url for key {Key}", k);
                            return (string?)null;
                        }
                    }));

                    await Task.WhenAll(tasks.Values);

                    var resolved = tasks.ToDictionary(p => p.Key, p => p.Value.Result);

                    foreach (var item in paged)
                    {
                        if (!string.IsNullOrEmpty(item.CoverImageKey) && resolved.TryGetValue(item.CoverImageKey!, out var pub))
                            item.PublicCoverUrl = pub;
                    }
                }
            }

            // categories for filter: cached multilingual rows; localized names computed per-request and ordered
            if (!_memoryCache.TryGetValue(CategoriesCacheKey_Teacher, out List<(int Id, string NameEn, string NameIt, string NameAr)> cachedCats))
            {
                var fromDb = await _db.Categories.AsNoTracking().Select(c => new { c.Id, c.NameEn, c.NameIt, c.NameAr }).ToListAsync();
                cachedCats = fromDb.Select(x => (x.Id, x.NameEn ?? string.Empty, x.NameIt ?? string.Empty, x.NameAr ?? string.Empty)).ToList();

                // cache for 30 minutes (invalidate in admin on change)
                _memoryCache.Set(CategoriesCacheKey_Teacher, cachedCats, TimeSpan.FromMinutes(30));
            }

            var categoryItems = cachedCats
                .Select(x =>
                {
                    var cat = new Category { NameEn = x.NameEn, NameIt = x.NameIt, NameAr = x.NameAr };
                    return new SelectListItem
                    {
                        Value = x.Id.ToString(),
                        Text = LocalizationHelpers.GetLocalizedCategoryName(cat),
                        Selected = categoryId.HasValue && categoryId.Value == x.Id
                    };
                })
                .OrderBy(s => s.Text)
                .ToList();

            // Put in ViewBag -> view will render explicit All first and then these items
            ViewBag.CategorySelect = categoryItems;

            ViewBag.ShowPublishedSelect = new List<SelectListItem>
            {
               new SelectListItem { Value = "", Text = _localizer["Common.All"], Selected = showPublished == null },
               new SelectListItem { Value = "true", Text = _localizer["Admin.Published"], Selected = showPublished == true },
               new SelectListItem { Value = "false", Text = _localizer["Admin.Unpublished"], Selected = showPublished == false }
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

        // GET: Teacher/Courses/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            // load course + modules + lessons + category
            var course = await _db.PrivateCourses
                                  .AsNoTracking()
                                  .Include(c => c.Category)
                                  .Include(c => c.PrivateModules)
                                  .Include(c => c.PrivateLessons)
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
                CoverImageKey = course.CoverImageKey,
                PriceLabel = course.Price.ToEuro(),
                Price = course.Price,
                IsPublished = course.IsPublished,
                IsPublishRequested = course.IsPublishRequested,
                CategoryId = course.CategoryId,
                CategoryNameEn = course.Category?.NameEn,
                CategoryNameIt = course.Category?.NameIt,
                CategoryNameAr = course.Category?.NameAr,
                TeacherId = course.TeacherId,
                IsForChildren = course.IsForChildren
            };

            // set localized category name
            vm.CategoryName = course.Category != null ? LocalizationHelpers.GetLocalizedCategoryName(course.Category) : null;

            // Resolve public cover url (best-effort)
            if (!string.IsNullOrEmpty(vm.CoverImageKey))
            {
                try { vm.PublicCoverUrl = await _fileStorage.GetPublicUrlAsync(vm.CoverImageKey); }
                catch { vm.PublicCoverUrl = null; }
            }

            // Modules summary
            var modules = course.PrivateModules != null
                          ? course.PrivateModules.OrderBy(m => m.Order).ToList()
                          : new List<PrivateModule>();

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

            // Lessons -> populate VMs with files (and include DownloadUrl)
            foreach (var l in lessonsWithFiles.OrderBy(l => l.Order))
            {
                // ensure we have a YouTube id (extract from VideoUrl if missing)
                var ytId = !string.IsNullOrWhiteSpace(l.YouTubeVideoId)
                           ? l.YouTubeVideoId
                           : (!string.IsNullOrWhiteSpace(l.VideoUrl) ? YouTubeHelper.ExtractYouTubeId(l.VideoUrl) : null);

                var lessonVm = new PrivateLessonVm
                {
                    Id = l.Id,
                    PrivateCourseId = l.PrivateCourseId,
                    PrivateModuleId = l.PrivateModuleId,
                    Title = l.Title,
                    YouTubeVideoId = ytId,
                    VideoUrl = l.VideoUrl,
                    Order = l.Order
                };

                // files
                if (l.Files != null)
                {
                    foreach (var f in l.Files)
                    {
                        lessonVm.Files.Add(new ViewModels.FileResourceVm
                        {
                            Id = f.Id,
                            Name = f.Name,
                            FileType = f.FileType,
                            FileUrl = f.FileUrl,
                            StorageKey = f.StorageKey,
                            // DownloadUrl fallback - server endpoint that will stream/redirect
                            DownloadUrl = Url.Action("Download", "FileResources", new { area = "Admin", id = f.Id })
                        });
                    }
                }

                vm.Lessons.Add(lessonVm);
            }

            // Build LessonsByModule dictionary for quick rendering in view
            var dict = new Dictionary<int, List<PrivateLessonVm>>();
            foreach (var lesson in vm.Lessons)
            {
                var key = lesson.PrivateModuleId ?? 0;
                if (!dict.ContainsKey(key)) dict[key] = new List<PrivateLessonVm>();
                dict[key].Add(lesson);
            }
            vm.LessonsByModule = dict;

            // IsOwner (important)
            var currentUser = await _userManager.GetUserAsync(User);
            vm.IsOwner = currentUser != null && !string.IsNullOrEmpty(course.TeacherId) && currentUser.Id == course.TeacherId;

            // -------------------------
            // Normalize and resolve public URLs for files (batch)
            // -------------------------
            // collect distinct keys/urls (StorageKey preferred)
            var keys = vm.Lessons
                         .SelectMany(l => l.Files ?? Enumerable.Empty<ViewModels.FileResourceVm>())
                         .Select(f => f.StorageKey ?? f.FileUrl)
                         .Where(k => !string.IsNullOrWhiteSpace(k))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .ToList();

            // helper local function
            string? NormalizeUrl(string? u)
            {
                if (string.IsNullOrWhiteSpace(u)) return null;
                u = u.Trim().Trim('"', '\'');
                if (u.StartsWith("~")) u = Url.Content(u);
                if (Uri.TryCreate(u, UriKind.Absolute, out _)) return u;
                if (!u.StartsWith("/")) u = "/" + u.TrimStart('/');
                return u;
            }

            if (keys.Any())
            {
                try
                {
                    var tasks = keys.Select(k => _fileStorage.GetPublicUrlAsync(k)).ToArray();
                    var resolved = await Task.WhenAll(tasks);

                    var map = keys.Select((k, i) => new { Key = k, Url = NormalizeUrl(resolved[i]) ?? NormalizeUrl(k) })
                                  .ToDictionary(x => x.Key, x => x.Url, StringComparer.OrdinalIgnoreCase);

                    // set PublicUrl for each file VM
                    foreach (var fileVm in vm.Lessons.SelectMany(l => l.Files ?? Enumerable.Empty<ViewModels.FileResourceVm>()))
                    {
                        var key = fileVm.StorageKey ?? fileVm.FileUrl;
                        fileVm.PublicUrl = key != null && map.TryGetValue(key, out var v) ? v : NormalizeUrl(key);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed resolving some file public URLs for teacher course {CourseId}", id);
                    // fallback: normalize stored FileUrl or StorageKey
                    foreach (var fileVm in vm.Lessons.SelectMany(l => l.Files ?? Enumerable.Empty<ViewModels.FileResourceVm>()))
                        fileVm.PublicUrl = NormalizeUrl(fileVm.FileUrl ?? fileVm.StorageKey);
                }
            }
            else
            {
                foreach (var fileVm in vm.Lessons.SelectMany(l => l.Files ?? Enumerable.Empty<ViewModels.FileResourceVm>()))
                    fileVm.PublicUrl = NormalizeUrl(fileVm.FileUrl ?? fileVm.StorageKey);
            }

            ViewData["ActivePage"] = "MyPrivateCourses";
            return View(vm);
        }


        // GET: Teacher/Courses/Create
        public async Task<IActionResult> Create()
        {
            // categories for filter (localize in-memory). No selected id for new item.
            var categories = await _db.Categories.AsNoTracking().ToListAsync();
            ViewBag.CategorySelect = categories
                .OrderBy(c => LocalizationHelpers.GetLocalizedCategoryName(c))
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = LocalizationHelpers.GetLocalizedCategoryName(c), Selected = false })
                .ToList();

            return View(new TeacherCourseCreateVm());
        }

        // POST: Teacher/Courses/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TeacherCourseCreateVm vm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // categories for filter (localize in-memory) - preserve selection on validation error
            var categories = await _db.Categories.AsNoTracking().ToListAsync();
            ViewBag.CategorySelect = categories
                .OrderBy(c => LocalizationHelpers.GetLocalizedCategoryName(c))
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = LocalizationHelpers.GetLocalizedCategoryName(c), Selected = vm.CategoryId.HasValue && vm.CategoryId.Value == c.Id })
                .ToList();

            if (!ModelState.IsValid) return View(vm);

            string? coverKey = null;
            if (vm.CoverImage != null)
            {
                try
                {
                    var folder = "private-covers";
                    coverKey = await _fileStorage.SaveFileAsync(vm.CoverImage, folder); // returns storage key
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
                CoverImageKey = coverKey, // store key (not url)
                Price = vm.Price,
                IsForChildren = vm.IsForChildren,
                IsPublished = false,
                IsPublishRequested = false
            };

            _db.PrivateCourses.Add(course);

            try
            {
                await _db.SaveChangesAsync();
                TempData["Success"] = "PrivateCourse.Created";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed creating private course for user {UserId}", user.Id);
                TempData["Error"] = "PrivateCourse.CreateFailed";
                return View(vm);
            }

            ViewData["ActivePage"] = "MyPrivateCourses";
            return RedirectToAction(nameof(Index));
        }

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
                ExistingCoverKey = course.CoverImageKey,
                IsForChildren = course.IsForChildren,
                IsPublishRequested = course.IsPublishRequested
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

            // categories for filter (localize in-memory) - mark selected using vm.CategoryId
            var categories = await _db.Categories.AsNoTracking().ToListAsync();
            ViewBag.CategorySelect = categories
                .OrderBy(c => LocalizationHelpers.GetLocalizedCategoryName(c))
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = LocalizationHelpers.GetLocalizedCategoryName(c),
                    Selected = vm.CategoryId == c.Id
                })
                .ToList();

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

            // categories for filter (localize in-memory) - preserve selection if re-displaying
            var categories = await _db.Categories.AsNoTracking().ToListAsync();
            ViewBag.CategorySelect = categories
                .OrderBy(c => LocalizationHelpers.GetLocalizedCategoryName(c))
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = LocalizationHelpers.GetLocalizedCategoryName(c),
                    Selected = vm.CategoryId.HasValue && vm.CategoryId.Value == c.Id
                })
                .ToList();

            if (!ModelState.IsValid) return View(vm);

            // cover replacement
            if (vm.CoverImage != null)
            {
                try
                {
                    var folder = "private-covers";
                    var newKey = await _fileStorage.SaveFileAsync(vm.CoverImage, folder);

                    // best-effort delete previous key
                    if (!string.IsNullOrEmpty(course.CoverImageKey))
                    {
                        try { await _fileStorage.DeleteFileAsync(course.CoverImageKey); } catch { /* ignore */ }
                    }

                    course.CoverImageKey = newKey;
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
            course.IsForChildren = vm.IsForChildren;

            try
            {
                await _db.SaveChangesAsync();
                TempData["Success"] = "PrivateCourse.Updated";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed updating course {CourseId}", course.Id);
                TempData["Error"] = "PrivateCourse.UpdateFailed";
                return View(vm);
            }

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

            course.IsPublishRequested = true;

            try
            {
                await _db.SaveChangesAsync();
                TempData["Success"] = "PrivateCourse.PublishRequested";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark publish request for course {CourseId}", id);
                TempData["Error"] = "PrivateCourse.PublishRequestFailed";
            }

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
                TempData["Info"] = "PrivateCourse.PublishRequestNotFound";
                return RedirectToAction("Details", new { id });
            }

            course.IsPublishRequested = false;
            try
            {
                await _db.SaveChangesAsync();
                TempData["Success"] = "PrivateCourse.PublishRequestCancelled";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel publish request for course {CourseId}", id);
                TempData["Error"] = "PrivateCourse.PublishRequestFailed";
            }

            return RedirectToAction("Details", new { id });
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
                // delete cover (use storage key)
                if (!string.IsNullOrEmpty(course.CoverImageKey))
                {
                    try { await _fileStorage.DeleteFileAsync(course.CoverImageKey); } catch { /*ignore*/ }
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
                                // prefer StorageKey, fallback to FileUrl
                                var keyOrUrl = !string.IsNullOrEmpty(f.StorageKey) ? f.StorageKey : f.FileUrl;
                                try { await _fileStorage.DeleteFileAsync(keyOrUrl); } catch { /*ignore*/ }
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

            try
            {
                await _db.SaveChangesAsync();
                TempData["Success"] = "PrivateCourse.Deleted";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed deleting private course {CourseId}", course.Id);
                TempData["Error"] = "PrivateCourse.DeleteFailed";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}



