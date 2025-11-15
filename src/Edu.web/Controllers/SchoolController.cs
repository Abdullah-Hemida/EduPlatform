using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Web.Resources;
using Edu.Web.ViewModels;
using Edu.Web.Views.Shared.Components.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;

namespace Edu.Web.Controllers
{
    public class SchoolController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fileStorage;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStringLocalizer<SharedResource> _L;
        private readonly IMemoryCache _cache;
        private readonly IHeroService _heroService;

        private const string CACHE_KEY_ALL_LEVELS = "School_AllLevels_v1";

        public SchoolController(ApplicationDbContext db,
                                IFileStorageService fileStorage,
                                UserManager<ApplicationUser> userManager,
                                IStringLocalizer<SharedResource> localizer,
                                IMemoryCache memoryCache,
                                IHeroService heroService
                                                        ) 
        {
            _db = db;
            _fileStorage = fileStorage;
            _userManager = userManager;
            _L = localizer;
            _cache = memoryCache;
            _heroService = heroService;
        }

        // GET: /School
        public async Task<IActionResult> Index(int? levelId = null, int page = 1, int pageSize = 9)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 9;

            // Current user access flags
            var user = await _userManager.GetUserAsync(User);
            bool isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Admin");
            bool studentIsAllowed = false;
            if (user != null && !isAdmin)
            {
                var studentRecord = await _db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == user.Id);
                studentIsAllowed = studentRecord?.IsAllowed ?? false;
            }

            // Load AllLevels (cached)
            List<Level> levels;
            if (!_cache.TryGetValue(CACHE_KEY_ALL_LEVELS, out List<Level>? cachedLevels))
            {
                levels = await _db.Levels
                                 .AsNoTracking()
                                 .Include(l => l.Curricula)
                                 .OrderBy(l => l.Order)
                                 .ToListAsync();

                // Cache options - tune durations as you need
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(30)) // persist for 30 minutes
                    .SetSlidingExpiration(TimeSpan.FromMinutes(5));

                _cache.Set(CACHE_KEY_ALL_LEVELS, levels, cacheEntryOptions);
            }
            else
            {
                levels = cachedLevels;
            }

            // Build AllLevels view model for filter buttons (localized counts)
            var allLevelVms = new List<SchoolLevelVm>();
            foreach (var l in levels)
            {
                var curriculaCount = l.Curricula?.Count ?? 0;
                string countText = curriculaCount == 1
                    ? _L["School.Curricula.One"].Value ?? "1 curriculum"
                    : string.Format(_L["School.Curricula.Many"].Value ?? "{0} curricula", curriculaCount);

                allLevelVms.Add(new SchoolLevelVm
                {
                    Id = l.Id,
                    Name = l.Name,
                    Order = l.Order,
                    CurriculaCountText = countText
                });
            }

            // Build curricula query (flat) and filter by levelId when provided
            var curriculaQuery = _db.Curricula
                                   .AsNoTracking()
                                   .Include(c => c.SchoolModules)
                                   .OrderBy(c => c.Order)
                                   .AsQueryable();

            if (levelId.HasValue)
                curriculaQuery = curriculaQuery.Where(c => c.LevelId == levelId.Value);

            // Total count for pagination
            var totalCount = await curriculaQuery.CountAsync();

            // Page slice
            var items = await curriculaQuery
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .Select(c => new
                        {
                            c.Id,
                            c.Title,
                            c.Description,
                            c.CoverImageUrl,
                            ModuleCount = c.SchoolModules != null ? c.SchoolModules.Count : 0,
                            c.Order,
                            c.LevelId
                        })
                        .ToListAsync();

            // Map and set access
            var curricula = items.Select(i => new CurriculumSummaryVm
            {
                Id = i.Id,
                Title = i.Title,
                Description = i.Description,
                CoverImageUrl = i.CoverImageUrl,
                ModuleCount = i.ModuleCount,
                Order = i.Order,
                IsAccessible = isAdmin || studentIsAllowed
            }).ToList();

            var vm = new SchoolIndexVm
            {
                AllLevels = allLevelVms,
                Curricula = curricula,
                SelectedLevelId = levelId,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
            vm.SchoolHero = await _heroService.GetHeroAsync(HeroPlacement.School);
            ViewData["Title"] = _L["School.Title"] ?? "School";
            return View(vm);
        }

        // GET: /School/List  (returns the partial for AJAX requests)
        [HttpGet]
        public async Task<IActionResult> List(int? levelId = null, int page = 1, int pageSize = 9, int ajax = 1)
        {
            // reuse same logic as Index to compute Curricula/pagination
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 9;

            var user = await _userManager.GetUserAsync(User);
            bool isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Admin");
            bool studentIsAllowed = false;
            if (user != null && !isAdmin)
            {
                var studentRecord = await _db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == user.Id);
                studentIsAllowed = studentRecord?.IsAllowed ?? false;
            }

            var curriculaQuery = _db.Curricula
                                   .AsNoTracking()
                                   .Include(c => c.SchoolModules)
                                   .OrderBy(c => c.Order)
                                   .AsQueryable();

            if (levelId.HasValue)
                curriculaQuery = curriculaQuery.Where(c => c.LevelId == levelId.Value);

            var totalCount = await curriculaQuery.CountAsync();

            var items = await curriculaQuery
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .Select(c => new
                        {
                            c.Id,
                            c.Title,
                            c.Description,
                            c.CoverImageUrl,
                            ModuleCount = c.SchoolModules != null ? c.SchoolModules.Count : 0,
                            c.Order
                        })
                        .ToListAsync();

            var curricula = items.Select(i => new Edu.Web.ViewModels.CurriculumSummaryVm
            {
                Id = i.Id,
                Title = i.Title,
                Description = i.Description,
                CoverImageUrl = i.CoverImageUrl,
                ModuleCount = i.ModuleCount,
                Order = i.Order,
                IsAccessible = isAdmin || studentIsAllowed
            }).ToList();

            var vm = new Edu.Web.ViewModels.SchoolIndexVm
            {
                Curricula = curricula,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                SelectedLevelId = levelId
            };
            vm.SchoolHero = await _heroService.GetHeroAsync(HeroPlacement.School);
            return PartialView("_CurriculaGridPartial", vm);
        }

        // GET: /School/Curriculum/5
        public async Task<IActionResult> Curriculum(int id)
        {
            var curriculum = await _db.Curricula
                                      .AsNoTracking()
                                      .Include(c => c.SchoolModules)
                                      .ThenInclude(m => m.SchoolLessons)
                                      .FirstOrDefaultAsync(c => c.Id == id);

            if (curriculum == null) return NotFound();

            // --- Authorization: only Admin or Students with Student.IsAllowed == true ---
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                // not authenticated -> ask to login
                return Challenge();
            }

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            if (!isAdmin)
            {
                // Not admin -> must be student with allowed flag
                var isStudent = await _userManager.IsInRoleAsync(user, "Student");
                if (!isStudent)
                {
                    // Not a student -> deny
                    TempData["Error"] = _L["School.AccessDenied"].Value ?? "You don't have access to view this curriculum.";
                    return RedirectToAction("Index");
                }

                // check student record and flag
                var studentRecord = await _db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == user.Id);
                if (studentRecord == null || !studentRecord.IsAllowed)
                {
                    TempData["Error"] = _L["School.NotAllowedMessage"].Value ?? "You are not allowed to view this curriculum.";
                    return RedirectToAction("Index");
                }
            }

            // --- Authorized: build VM as before ---
            var vm = new CurriculumDetailsVm
            {
                Id = curriculum.Id,
                Title = curriculum.Title,
                Description = curriculum.Description,
                CoverImageUrl = curriculum.CoverImageUrl,
                LevelId = curriculum.LevelId,
                Modules = curriculum.SchoolModules?
                            .OrderBy(m => m.Order)
                            .Select(m => new ModuleVm
                            {
                                Id = m.Id,
                                Title = m.Title,
                                Order = m.Order,
                                Lessons = m.SchoolLessons?
                                          .OrderBy(l => l.Order)
                                          .Select(l => new LessonVm
                                          {
                                              Id = l.Id,
                                              Title = l.Title,
                                              Description = l.Description,
                                              YouTubeVideoId = l.YouTubeVideoId,
                                              VideoUrl = l.VideoUrl,
                                              IsFree = l.IsFree,
                                              Order = l.Order,
                                              Files = new List<FileResourceVm>()
                                          }).ToList() ?? new List<LessonVm>()
                            }).ToList() ?? new List<ModuleVm>()
            };

            // batch-load files (unchanged)...
            var lessonIds = vm.Modules.SelectMany(m => m.Lessons).Select(l => l.Id).Where(i => i > 0).ToList();
            if (lessonIds.Any())
            {
                var files = await _db.FileResources
                                     .AsNoTracking()
                                     .Where(fr => fr.SchoolLessonId != null && lessonIds.Contains(fr.SchoolLessonId.Value))
                                     .ToListAsync();

                var storageKeyMap = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

                foreach (var f in files)
                {
                    var lessonId = f.SchoolLessonId ?? 0;
                    var fileVm = new FileResourceVm
                    {
                        Id = f.Id,
                        Name = f.Name ?? System.IO.Path.GetFileName(f.FileUrl ?? f.StorageKey ?? ""),
                        FileType = f.FileType
                    };

                    if (!string.IsNullOrEmpty(f.StorageKey))
                    {
                        if (!storageKeyMap.TryGetValue(f.StorageKey, out var publicUrl))
                        {
                            publicUrl = await _fileStorage.GetPublicUrlAsync(f.StorageKey);
                            storageKeyMap[f.StorageKey] = publicUrl;
                        }
                        fileVm.PublicUrl = storageKeyMap[f.StorageKey];
                    }
                    else if (!string.IsNullOrEmpty(f.FileUrl))
                    {
                        fileVm.PublicUrl = f.FileUrl;
                    }

                    var lessonVm = vm.Modules.SelectMany(m => m.Lessons).FirstOrDefault(x => x.Id == lessonId);
                    if (lessonVm != null) lessonVm.Files.Add(fileVm);
                }
            }
            vm.SchoolHero = await _heroService.GetHeroAsync(HeroPlacement.School);
            ViewData["Title"] = curriculum.Title;
            return View(vm);
        }


        // GET: /School/Lesson/123
        public async Task<IActionResult> Lesson(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

                // after retrieving studentRecord:
                if (await _userManager.IsInRoleAsync(user, "Student"))
                {
                    var studentRecord = await _db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == user.Id);
                    if (studentRecord == null || !studentRecord.IsAllowed)
                    {
                        TempData["Error"] = _L["School.NotAllowedMessage"].Value ?? "You are not allowed to view this content.";
                        return RedirectToAction("Index");
                    }
                }

            var lesson = await _db.SchoolLessons
                                  .AsNoTracking()
                                  .Include(l => l.SchoolModule)
                                  .ThenInclude(m => m.Curriculum)
                                  .FirstOrDefaultAsync(l => l.Id == id);

            if (lesson == null) return NotFound();

            // Resolve lesson files
            var files = await _db.FileResources.AsNoTracking().Where(f => f.SchoolLessonId == lesson.Id).ToListAsync();
            var fileVms = new List<FileResourceVm>();
            foreach (var f in files)
            {
                string? publicUrl = null;
                if (!string.IsNullOrEmpty(f.StorageKey))
                {
                    publicUrl = await _fileStorage.GetPublicUrlAsync(f.StorageKey);
                }
                else if (!string.IsNullOrEmpty(f.FileUrl))
                {
                    publicUrl = f.FileUrl;
                }

                fileVms.Add(new FileResourceVm
                {
                    Id = f.Id,
                    Name = f.Name ?? System.IO.Path.GetFileName(f.FileUrl ?? f.StorageKey ?? ""),
                    FileType = f.FileType,
                    PublicUrl = publicUrl
                });
            }

            var vm = new LessonDetailsVm
            {
                Id = lesson.Id,
                Title = lesson.Title,
                Description = lesson.Description,
                YouTubeVideoId = lesson.YouTubeVideoId,
                VideoUrl = lesson.VideoUrl,
                IsFree = lesson.IsFree,
                ModuleTitle = lesson.SchoolModule?.Title,
                CurriculumTitle = lesson.SchoolModule?.Curriculum?.Title,
                Files = fileVms
            };

            ViewData["Title"] = lesson.Title;
            return View(vm);
        }
    }
}


