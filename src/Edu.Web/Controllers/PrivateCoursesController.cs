using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Web.Resources;
using Edu.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;

namespace Edu.Web.Controllers
{
    public class PrivateCoursesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fileStorage;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<PrivateCoursesController> _logger;

        private const string CategoriesCacheKey = "Public_PrivateCourses_Categories_v1";

        public PrivateCoursesController(
            ApplicationDbContext db,
            IFileStorageService fileStorage,
            UserManager<ApplicationUser> userManager,
            IStringLocalizer<SharedResource> localizer,
            IMemoryCache memoryCache,
            ILogger<PrivateCoursesController> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _fileStorage = fileStorage ?? throw new ArgumentNullException(nameof(fileStorage));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _logger = logger;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Index(string? q = null, int? categoryId = null, bool? forChildren = null, int page = 1)
        {
            const int pageSize = 12;
            page = Math.Max(1, page);

            // Base query: published courses
            IQueryable<PrivateCourse> baseQuery = _db.PrivateCourses
                .AsNoTracking()
                .Where(c => c.IsPublished);

            // Category filter
            if (categoryId.HasValue && categoryId.Value > 0)
                baseQuery = baseQuery.Where(c => c.CategoryId == categoryId.Value);

            // for children filter
            if (forChildren.HasValue)
                baseQuery = baseQuery.Where(c => c.IsForChildren == forChildren.Value);

            // Search filter (null-safe)
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                baseQuery = baseQuery.Where(c =>
                    EF.Functions.Like(c.Title ?? "", $"%{term}%") ||
                    EF.Functions.Like(c.Description ?? "", $"%{term}%") ||
                    (c.Teacher != null && c.Teacher.User != null && EF.Functions.Like(c.Teacher.User.FullName ?? "", $"%{term}%"))
                );
            }

            // total count for pagination
            var totalCourses = await baseQuery.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCourses / (double)pageSize);

            // Project only fields we need for the list page
            var pageItems = await baseQuery
                .OrderByDescending(c => c.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new
                {
                    c.Id,
                    c.Title,
                    c.Description,
                    c.CoverImageKey,
                    c.Price,
                    c.IsForChildren,
                    TeacherFullName = c.Teacher != null && c.Teacher.User != null ? c.Teacher.User.FullName : null,
                    CategoryNameEn = c.Category != null ? c.Category.NameEn : null,
                    CategoryNameIt = c.Category != null ? c.Category.NameIt : null,
                    CategoryNameAr = c.Category != null ? c.Category.NameAr : null
                })
                .ToListAsync();

            // Build VM
            var vm = new PrivateCourseIndexVm
            {
                Query = q,
                SelectedCategoryId = categoryId,
                ForChildrenOnly = forChildren,
                PageNumber = page,
                TotalPages = totalPages
            };

            // Map items to VMs (category localized)
            foreach (var c in pageItems)
            {
                var category = new Category
                {
                    NameEn = c.CategoryNameEn ?? string.Empty,
                    NameIt = c.CategoryNameIt ?? string.Empty,
                    NameAr = c.CategoryNameAr ?? string.Empty
                };

                vm.Courses.Add(new PrivateCourseListItemVm
                {
                    Id = c.Id,
                    Title = c.Title,
                    Description = string.IsNullOrEmpty(c.Description) ? null
                                  : (c.Description.Length > 240 ? c.Description.Substring(0, 240) + "…" : c.Description),
                    CoverImageKey = c.CoverImageKey,
                    TeacherName = c.TeacherFullName,
                    CategoryName = LocalizationHelpers.GetLocalizedCategoryName(category),
                    PriceLabel = c.Price.ToEuro(),
                    IsForChildren = c.IsForChildren
                });
            }

            // Resolve distinct cover keys in parallel (best-effort, non-blocking)
            var keys = vm.Courses
                .Where(x => !string.IsNullOrEmpty(x.CoverImageKey))
                .Select(x => x.CoverImageKey!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (keys.Any())
            {
                var tasks = keys.Select(k => ResolvePublicUrlSafeAsync(k)).ToArray();
                var results = await Task.WhenAll(tasks);
                var map = keys.Zip(results, (k, u) => (k, u)).ToDictionary(x => x.k, x => x.u, StringComparer.OrdinalIgnoreCase);

                foreach (var course in vm.Courses)
                {
                    if (!string.IsNullOrEmpty(course.CoverImageKey) && map.TryGetValue(course.CoverImageKey!, out var pub))
                        course.CoverPublicUrl = pub;
                    else
                        course.CoverPublicUrl = null;
                }
            }

            // --- Categories for filter: use IMemoryCache for multilingual fields, compute localized names per-request ---
            if (!_memoryCache.TryGetValue(CategoriesCacheKey, out List<(int Id, string NameEn, string NameIt, string NameAr)> cachedCats))
            {
                var catsFromDb = await _db.Categories
                    .AsNoTracking()
                    .Select(x => new { x.Id, x.NameEn, x.NameIt, x.NameAr })
                    .ToListAsync();

                cachedCats = catsFromDb
                    .Select(x => (x.Id, x.NameEn ?? string.Empty, x.NameIt ?? string.Empty, x.NameAr ?? string.Empty))
                    .ToList();

                // Cache for 30 minutes. Invalidate when categories change.
                _memoryCache.Set(CategoriesCacheKey, cachedCats, TimeSpan.FromMinutes(30));
            }

            vm.Categories = cachedCats
                .Select(x => new CategoryVm
                {
                    Id = x.Id,
                    Name = LocalizationHelpers.GetLocalizedCategoryName(new Category
                    {
                        NameEn = x.NameEn,
                        NameIt = x.NameIt,
                        NameAr = x.NameAr
                    })
                })
                .OrderBy(c => c.Name)
                .ToList();

            return View(vm);
        }

        // helper: resolve public url safely
        private async Task<string?> ResolvePublicUrlSafeAsync(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            try
            {
                return await _fileStorage.GetPublicUrlAsync(key);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Failed to resolve public url for key {Key}", key);
                return null;
            }
        }
    

        // GET: /PrivateCourses/Details/5 (public view — NO modules/lessons)
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Details(int id)
        {
            // lightweight projection
            var course = await _db.PrivateCourses
                .AsNoTracking()
                .Where(c => c.Id == id && c.IsPublished)
                .Select(c => new
                {
                    c.Id,
                    c.Title,
                    c.Description,
                    c.CoverImageKey,
                    c.Price,
                    TeacherFullName = c.Teacher != null && c.Teacher.User != null ? c.Teacher.User.FullName : null,
                    CategoryNameEn = c.Category != null ? c.Category.NameEn : null,
                    CategoryNameIt = c.Category != null ? c.Category.NameIt : null,
                    CategoryNameAr = c.Category != null ? c.Category.NameAr : null
                })
                .FirstOrDefaultAsync();

            if (course == null) return NotFound();

            var userId = _user_manager_getid_safe();

            bool hasCompletedPurchase = false;
            bool hasPendingPurchase = false;
            if (!string.IsNullOrEmpty(userId))
            {
                hasCompletedPurchase = await _db.PurchaseRequests
                    .AsNoTracking()
                    .AnyAsync(p => p.PrivateCourseId == id && p.StudentId == userId && p.Status == PurchaseStatus.Completed);

                hasPendingPurchase = await _db.PurchaseRequests
                    .AsNoTracking()
                    .AnyAsync(p => p.PrivateCourseId == id && p.StudentId == userId && p.Status == PurchaseStatus.Pending);
            }

            var vm = new PrivateCourseDetailsVm
            {
                Id = course.Id,
                Title = course.Title,
                Description = course.Description,
                CoverImageKey = course.CoverImageKey,
                TeacherName = course.TeacherFullName,
                CategoryName = LocalizationHelpers.GetLocalizedCategoryName(new Category
                {
                    NameEn = course.CategoryNameEn ?? string.Empty,
                    NameIt = course.CategoryNameIt ?? string.Empty,
                    NameAr = course.CategoryNameAr ?? string.Empty
                }),
                PriceLabel = course.Price.ToEuro(),
                IsPurchased = hasCompletedPurchase,
                HasPendingPurchase = hasPendingPurchase
            };

            if (!string.IsNullOrEmpty(vm.CoverImageKey))
            {
                vm.CoverPublicUrl = await ResolvePublicUrlSafeAsync(vm.CoverImageKey)!;
            }
            else
            {
                vm.CoverPublicUrl = null;
            }

            return View(vm);
        }

        // small helper to get current user id safely (wrap call to _userManager.GetUserId(User))
        private string? _user_manager_getid_safe()
        {
            try { return _userManager.GetUserId(User); }
            catch { return null; }
        }
    }
}




