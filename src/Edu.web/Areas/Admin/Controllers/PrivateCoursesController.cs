// File: Areas/Admin/Controllers/PrivateCoursesController.cs
using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Infrastructure.Services;
using Edu.Web.Areas.Admin.ViewModels;
using Edu.Web.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;

namespace Edu.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class PrivateCoursesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<PrivateCoursesController> _logger;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IEmailSender _emailSender;
        private readonly IFileStorageService _fileStorage;
        private readonly IMemoryCache _memoryCache;

        private const string CategoriesCacheKey = "Admin_PrivateCourses_Categories_v1";

        public PrivateCoursesController(
            ApplicationDbContext db,
            IFileStorageService fileStorage,
            UserManager<ApplicationUser> userManager,
            ILogger<PrivateCoursesController> logger,
            IStringLocalizer<SharedResource> localizer,
            IEmailSender emailSender,
            IMemoryCache memoryCache)    // <-- new param
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _fileStorage = fileStorage ?? throw new ArgumentNullException(nameof(fileStorage));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _localizer = localizer;
            _emailSender = emailSender;
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        }

        // GET: Admin/PrivateCourses
        public async Task<IActionResult> Index(
            string? q, int? categoryId, bool? showPublished, bool showAll = false, bool? forChildren = null, int page = 1,
            CancellationToken cancellationToken = default)
        {
            const int PageSize = 12;
            ViewData["ActivePage"] = "PrivateCourses";

            // Base query (no tracking)
            var baseQuery = _db.PrivateCourses.AsNoTracking().AsQueryable();

            if (!showAll)
                baseQuery = baseQuery.Where(pc => pc.IsPublishRequested == true);

            if (categoryId.HasValue)
                baseQuery = baseQuery.Where(pc => pc.CategoryId == categoryId.Value);

            if (showPublished.HasValue)
                baseQuery = baseQuery.Where(pc => pc.IsPublished == showPublished.Value);

            if (forChildren.HasValue)
                baseQuery = baseQuery.Where(pc => pc.IsForChildren == forChildren.Value);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                baseQuery = baseQuery.Where(pc =>
                    (pc.Title != null && EF.Functions.Like(pc.Title, $"%{term}%")) ||
                    (pc.Description != null && EF.Functions.Like(pc.Description, $"%{term}%")) ||
                    (pc.Category != null &&
                        (
                            (pc.Category.NameEn != null && EF.Functions.Like(pc.Category.NameEn, $"%{term}%")) ||
                            (pc.Category.NameIt != null && EF.Functions.Like(pc.Category.NameIt, $"%{term}%")) ||
                            (pc.Category.NameAr != null && EF.Functions.Like(pc.Category.NameAr, $"%{term}%"))
                        )
                    ) ||
                    (pc.Teacher != null && pc.Teacher.User != null &&
                        ((pc.Teacher.User.FullName != null && EF.Functions.Like(pc.Teacher.User.FullName, $"%{term}%")) ||
                         (pc.Teacher.User.Email != null && EF.Functions.Like(pc.Teacher.User.Email, $"%{term}%"))))
                );
            }

            baseQuery = baseQuery.OrderByDescending(pc => pc.Id);

            // Project only the fields we need
            var projected = baseQuery.Select(pc => new PrivateCourseListItemVm
            {
                Id = pc.Id,
                Title = pc.Title,
                CategoryId = pc.CategoryId,
                CategoryNameEn = pc.Category != null ? pc.Category.NameEn : null,
                CategoryNameIt = pc.Category != null ? pc.Category.NameIt : null,
                CategoryNameAr = pc.Category != null ? pc.Category.NameAr : null,
                Price = pc.Price,
                IsPublished = pc.IsPublished,
                IsPublishRequested = pc.IsPublishRequested,
                CoverImageKey = pc.CoverImageKey,
                TeacherId = pc.TeacherId,
                TeacherName = pc.Teacher != null && pc.Teacher.User != null ? pc.Teacher.User.FullName : null,
                TeacherEmail = pc.Teacher != null && pc.Teacher.User != null ? pc.Teacher.User.Email : null,
                IsForChildren = pc.IsForChildren
            });

            // Pagination
            var paged = await PaginatedList<PrivateCourseListItemVm>.CreateAsync(projected, page, PageSize);

            // Post-processing after materialization
            if (paged != null && paged.Any())
            {
                foreach (var it in paged)
                {
                    var pseudo = new Edu.Domain.Entities.Category
                    {
                        NameEn = it.CategoryNameEn ?? string.Empty,
                        NameIt = it.CategoryNameIt ?? string.Empty,
                        NameAr = it.CategoryNameAr ?? string.Empty
                    };
                    it.CategoryName = LocalizationHelpers.GetLocalizedCategoryName(pseudo);

                    try { it.PriceLabel = it.Price.ToEuro(); }
                    catch { it.PriceLabel = it.Price.ToString("0.##"); }
                }

                // Batch-resolve distinct cover keys
                var keys = paged.Select(x => x.CoverImageKey).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
                if (keys.Any())
                {
                    var tasks = keys.ToDictionary(k => k, k => Task.Run(async () =>
                    {
                        try { return await _fileStorage.GetPublicUrlAsync(k); }
                        catch
                        {
                            _logger.LogDebug("Failed to resolve cover public url for key {Key}", k);
                            return (string?)null;
                        }
                    }));

                    await Task.WhenAll(tasks.Values);

                    var resolved = tasks.ToDictionary(p => p.Key, p => p.Value.Result);

                    foreach (var it in paged)
                    {
                        if (!string.IsNullOrEmpty(it.CoverImageKey) && resolved.TryGetValue(it.CoverImageKey!, out var pub))
                            it.PublicCoverUrl = pub;
                        else
                            it.PublicCoverUrl = null;
                    }
                }
            }

            var vm = new PrivateCourseIndexVm
            {
                Query = q,
                CategoryId = categoryId,
                ShowPublished = showPublished,
                ShowAll = showAll,
                PageIndex = paged.PageIndex,
                PageSize = paged.PageSize,
                TotalCount = paged.TotalCount,
                Courses = paged,
                ForChildren = forChildren
            };

            // --- Categories for filter: cached, minimal fields, localized in-memory ---
            // Try cache
            if (!_memoryCache.TryGetValue(CategoriesCacheKey, out List<SelectListItem> categoryItems))
            {
                var cats = await _db.Categories
                    .AsNoTracking()
                    .Select(c => new { c.Id, c.NameEn, c.NameIt, c.NameAr })
                    .ToListAsync(cancellationToken);

                categoryItems = cats
                    .Select(c =>
                    {
                        var tmp = new Edu.Domain.Entities.Category
                        {
                            NameEn = c.NameEn ?? string.Empty,
                            NameIt = c.NameIt ?? string.Empty,
                            NameAr = c.NameAr ?? string.Empty
                        };
                        return new SelectListItem
                        {
                            Value = c.Id.ToString(),
                            Text = LocalizationHelpers.GetLocalizedCategoryName(tmp)
                        };
                    })
                    .OrderBy(x => x.Text)
                    .ToList();

                // cache for 30 minutes (invalidated by your admin actions when categories change)
                _memoryCache.Set(CategoriesCacheKey, categoryItems, TimeSpan.FromMinutes(30));
            }

            // mark selected
            foreach (var si in categoryItems) si.Selected = categoryId.HasValue && si.Value == categoryId.Value.ToString();

            // Expose for view; the view will render an explicit "All" option first
            ViewBag.CategorySelect = categoryItems;

            ViewBag.ShowPublishedSelect = new List<SelectListItem>
        {
            new SelectListItem { Value = "true", Text = _localizer != null ? _localizer["Admin.Published"].Value : "Published", Selected = showPublished == true},
            new SelectListItem { Value = "false", Text = _localizer != null ? _localizer["Admin.Unpublished"].Value : "Unpublished", Selected = showPublished == false}
        };

            ViewBag.ForChildrenSelect = new List<SelectListItem>
        {
            new SelectListItem { Value = "", Text = _localizer != null ? _localizer["Common.All"].Value : "All", Selected = forChildren == null },
            new SelectListItem { Value = "true", Text = _localizer != null ? _localizer["Teacher.ForChildren"].Value : "For children", Selected = forChildren == true },
            new SelectListItem { Value = "false", Text = _localizer != null ? _localizer["Teacher.ForAdults"].Value : "For adults", Selected = forChildren == false }
        };

            ViewData["ActivePage"] = "PrivateCourses";
            return View(vm);
        }

        // GET: Admin/PrivateCourses/Details/5
        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken = default)
        {
            // lightweight projection
            var courseBasic = await _db.PrivateCourses
                .AsNoTracking()
                .Where(pc => pc.Id == id)
                .Select(pc => new
                {
                    pc.Id,
                    pc.Title,
                    pc.Description,
                    pc.CategoryId,
                    CategoryNameEn = pc.Category != null ? pc.Category.NameEn : null,
                    CategoryNameIt = pc.Category != null ? pc.Category.NameIt : null,
                    CategoryNameAr = pc.Category != null ? pc.Category.NameAr : null,
                    pc.Price,
                    pc.IsPublished,
                    pc.CoverImageKey,
                    pc.TeacherId,
                    TeacherFullName = pc.Teacher != null && pc.Teacher.User != null ? pc.Teacher.User.FullName : null,
                    TeacherEmail = pc.Teacher != null && pc.Teacher.User != null ? pc.Teacher.User.Email : null
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (courseBasic == null) return NotFound();

            var courseVm = new PrivateCourseDetailsVm
            {
                Id = courseBasic.Id,
                Title = courseBasic.Title,
                Description = courseBasic.Description,
                CategoryId = courseBasic.CategoryId,
                CategoryNameEn = courseBasic.CategoryNameEn,
                CategoryNameIt = courseBasic.CategoryNameIt,
                CategoryNameAr = courseBasic.CategoryNameAr,
                PriceLabel = courseBasic.Price.ToEuro(),
                IsPublished = courseBasic.IsPublished,
                CoverImageKey = courseBasic.CoverImageKey,
                Teacher = new TeacherVm
                {
                    Id = courseBasic.TeacherId,
                    FullName = courseBasic.TeacherFullName,
                    Email = courseBasic.TeacherEmail
                }
            };
            // set localized CategoryName
            courseVm.CategoryName = LocalizationHelpers.GetLocalizedCategoryName(new Edu.Domain.Entities.Category
            {
                NameEn = courseVm.CategoryNameEn ?? string.Empty,
                NameIt = courseVm.CategoryNameIt ?? string.Empty,
                NameAr = courseVm.CategoryNameAr ?? string.Empty
            });
            // resolve cover public url (best-effort)
            if (!string.IsNullOrEmpty(courseVm.CoverImageKey))
            {
                try { courseVm.PublicCoverUrl = await _fileStorage.GetPublicUrlAsync(courseVm.CoverImageKey, TimeSpan.FromHours(1)); }
                catch { courseVm.PublicCoverUrl = null; }
            }

            // modules & lessons (include files)
            var modules = await _db.PrivateModules.AsNoTracking().Where(m => m.PrivateCourseId == id).OrderBy(m => m.Order).ToListAsync(cancellationToken);
            var lessonsWithFiles = await _db.PrivateLessons.AsNoTracking().Where(l => l.PrivateCourseId == id).Include(l => l.Files).ToListAsync(cancellationToken);

            // group lessons by module id
            var lessonsByModule = lessonsWithFiles.GroupBy(l => l.PrivateModuleId ?? 0)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Order).ToList());

            // map modules & lessons to VMs (no reflection)
            foreach (var m in modules)
            {
                var moduleVm = new PrivateModuleVm
                {
                    Id = m.Id,
                    Title = m.Title,
                    Description = m.Description,
                    Order = m.Order,
                    Lessons = new List<PrivateLessonVm>()
                };

                if (lessonsByModule.TryGetValue(m.Id, out var lessonsForModule))
                {
                    foreach (var l in lessonsForModule)
                    {
                        var lessonVm = new PrivateLessonVm
                        {
                            Id = l.Id,
                            Title = l.Title,
                            Order = l.Order,
                            YouTubeVideoId = l.YouTubeVideoId,
                            VideoUrl = l.VideoUrl,
                            Files = l.Files?.Select(f => new FileResourceVm
                            {
                                Id = f.Id,
                                Name = f.Name,
                                StorageKey = f.StorageKey, // direct property access
                                FileUrl = f.FileUrl,
                                FileType = f.FileType
                            }).ToList() ?? new List<FileResourceVm>()
                        };
                        moduleVm.Lessons.Add(lessonVm);
                    }
                }

                courseVm.Modules.Add(moduleVm);
            }
            // helper local function to normalize a provider returned url or stored FileUrl/key
            string? NormalizeUrl(string? u)
            {
                if (string.IsNullOrWhiteSpace(u)) return null;
                u = u.Trim().Trim('"', '\''); // remove stray quotes
                                              // ~ => root-relative
                if (u.StartsWith("~")) u = Url.Content(u);
                // if already absolute, return as-is
                if (Uri.TryCreate(u, UriKind.Absolute, out _)) return u;
                // ensure root-relative
                if (!u.StartsWith("/")) u = "/" + u.TrimStart('/');
                return u;
            }

            // standalone lessons (moduleId == null -> key 0)
            if (lessonsByModule.TryGetValue(0, out var standalone))
            {
                foreach (var l in standalone)
                {
                    var lvm = new PrivateLessonVm
                    {
                        Id = l.Id,
                        Title = l.Title,
                        Order = l.Order,
                        YouTubeVideoId = l.YouTubeVideoId,
                        VideoUrl = l.VideoUrl,
                        Files = l.Files?.Select(f => new FileResourceVm
                        {
                            Id = f.Id,
                            Name = f.Name,
                            StorageKey = f.StorageKey,
                            FileUrl = f.FileUrl,
                            FileType = f.FileType,
                            // DownloadUrl points to server Download endpoint (safe fallback)
                            DownloadUrl = Url.Action("Download", "FileResources", new { area = "Admin", id = f.Id })
                        }).ToList() ?? new List<FileResourceVm>()
                    };
                    courseVm.StandaloneLessons.Add(lvm);
                }
            }

            // Resolve file public URLs in parallel for all distinct keys
            var fileKeys = new List<string>();
            foreach (var m in courseVm.Modules)
                foreach (var l in m.Lessons ?? Enumerable.Empty<PrivateLessonVm>())
                    foreach (var f in l.Files ?? Enumerable.Empty<FileResourceVm>())
                        if (!string.IsNullOrEmpty(f.StorageKey ?? f.FileUrl)) fileKeys.Add(f.StorageKey ?? f.FileUrl);

            foreach (var l in courseVm.StandaloneLessons)
                foreach (var f in l.Files ?? Enumerable.Empty<FileResourceVm>())
                    if (!string.IsNullOrEmpty(f.StorageKey ?? f.FileUrl)) fileKeys.Add(f.StorageKey ?? f.FileUrl);

            var distinctKeys = fileKeys.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (distinctKeys.Any())
            {
                var urlTasks = distinctKeys.Select(k => _fileStorage.GetPublicUrlAsync(k, TimeSpan.FromHours(1))).ToArray();
                string?[] urls;
                try
                {
                    urls = await Task.WhenAll(urlTasks);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Some file public URL lookups failed for course {CourseId}", id);
                    // fallback to using key itself (but normalize)
                    urls = distinctKeys.Select(k => (string?)k).ToArray();
                }

                var map = distinctKeys.Select((k, i) => new { Key = k, Url = NormalizeUrl(urls[i]) }).ToDictionary(x => x.Key, x => x.Url);

                foreach (var m in courseVm.Modules)
                    foreach (var l in m.Lessons ?? Enumerable.Empty<PrivateLessonVm>())
                        foreach (var f in l.Files ?? Enumerable.Empty<FileResourceVm>())
                        {
                            var key = f.StorageKey ?? f.FileUrl;
                            var resolved = key != null && map.TryGetValue(key, out var pu) ? pu : NormalizeUrl(key);
                            f.PublicUrl = resolved;
                        }

                foreach (var l in courseVm.StandaloneLessons)
                    foreach (var f in l.Files ?? Enumerable.Empty<FileResourceVm>())
                    {
                        var key = f.StorageKey ?? f.FileUrl;
                        var resolved = key != null && map.TryGetValue(key, out var pu) ? pu : NormalizeUrl(key);
                        f.PublicUrl = resolved;
                    }
            }
            else
            {
                // if no storage keys but file urls may exist, normalize them
                foreach (var m in courseVm.Modules)
                    foreach (var l in m.Lessons ?? Enumerable.Empty<PrivateLessonVm>())
                        foreach (var f in l.Files ?? Enumerable.Empty<FileResourceVm>())
                            f.PublicUrl = NormalizeUrl(f.FileUrl ?? f.StorageKey);

                foreach (var l in courseVm.StandaloneLessons)
                    foreach (var f in l.Files ?? Enumerable.Empty<FileResourceVm>())
                        f.PublicUrl = NormalizeUrl(f.FileUrl ?? f.StorageKey);
            }

            ViewData["ActivePage"] = "PrivateCourses";
            return View(courseVm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePublish(int id, CancellationToken cancellationToken = default)
        {
            var course = await _db.PrivateCourses.FindAsync(new object[] { id }, cancellationToken);
            if (course == null) return NotFound();

            course.IsPublished = !course.IsPublished;

            var adminId = _userManager.GetUserId(User);
            await _db.SaveChangesAsync(cancellationToken);

            // Return new state
            return Json(new { success = true, isPublished = course.IsPublished });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default)
        {
            var course = await _db.PrivateCourses.FindAsync(new object[] { id }, cancellationToken);
            if (course == null) return NotFound();

            try
            {
                // Attempt best-effort cleanup of files attached to lessons
                var lessonIds = await _db.PrivateLessons.AsNoTracking().Where(l => l.PrivateCourseId == id).Select(l => l.Id).ToListAsync(cancellationToken);
                var files = await _db.FileResources.Where(f => f.PrivateLessonId != null && lessonIds.Contains(f.PrivateLessonId.Value)).ToListAsync(cancellationToken);

                foreach (var f in files)
                {
                    try
                    {
                        var key = f.StorageKey ?? f.FileUrl;
                        if (!string.IsNullOrEmpty(key))
                            await _fileStorage.DeleteFileAsync(key);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete storage item for FileResource {FileId}", f.Id);
                    }
                }

                if (files.Any()) _db.FileResources.RemoveRange(files);

                _db.PrivateCourses.Remove(course);
                await _db.SaveChangesAsync(cancellationToken);

                TempData["Success"] = "PrivateCourse.Deleted";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed deleting private course {CourseId}", id);
                TempData["Error"] = "PrivateCourse.DeleteFailed";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}

