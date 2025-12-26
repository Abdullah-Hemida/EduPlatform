using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
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
                                IHeroService heroService)
        {
            _db = db;
            _fileStorage = fileStorage;
            _userManager = userManager;
            _L = localizer;
            _cache = memoryCache;
            _heroService = heroService;
        }

        // GET: /School
        // Renders full page (includes hero, levels filter and initial curricula grid)
        public async Task<IActionResult> Index(int? levelId = null, int page = 1, int pageSize = 9)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 9;

            // Prepare AllLevels (localized name) and pass to view together with curricula page
            var allLevels = await GetAllLevelsLocalizedAsync();

            // compute user access flags (admin or allowed student)
            var user = await _userManager.GetUserAsync(User);
            bool isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Admin");
            bool studentIsAllowed = false;
            List<int> assignedCurriculumIds = new();

            if (user != null && !isAdmin)
            {
                var studentRecord = await _db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == user.Id);
                studentIsAllowed = studentRecord?.IsAllowed ?? false;

                if (studentIsAllowed)
                {
                    assignedCurriculumIds = await _db.StudentCurricula
                        .AsNoTracking()
                        .Where(sc => sc.StudentId == user.Id)
                        .Select(sc => sc.CurriculumId)
                        .ToListAsync();
                }
            }

            // Build Pageable curricula and other metadata via the shared helper ListInternalAsync
            var (curricula, totalCount) = await ListInternalAsync(levelId, page, pageSize, isAdmin, studentIsAllowed, assignedCurriculumIds);

            var vm = new SchoolIndexVm
            {
                AllLevels = allLevels,
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

        // GET: /School/List
        // Returns partial grid for curricula (used by AJAX). If requested without ajax flag, returns the same partial.
        [HttpGet]
        public async Task<IActionResult> List(int? levelId = null, int page = 1, int pageSize = 9, string? ajax = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 9;

            // compute user access flags
            var user = await _userManager.GetUserAsync(User);
            bool isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Admin");
            bool studentIsAllowed = false;
            List<int> assignedCurriculumIds = new();

            if (user != null && !isAdmin)
            {
                var studentRecord = await _db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == user.Id);
                studentIsAllowed = studentRecord?.IsAllowed ?? false;

                if (studentIsAllowed)
                {
                    assignedCurriculumIds = await _db.StudentCurricula
                        .AsNoTracking()
                        .Where(sc => sc.StudentId == user.Id)
                        .Select(sc => sc.CurriculumId)
                        .ToListAsync();
                }
            }

            var allLevels = await GetAllLevelsLocalizedAsync();

            var (curricula, totalCount) = await ListInternalAsync(levelId, page, pageSize, isAdmin, studentIsAllowed, assignedCurriculumIds);

            var vm = new SchoolIndexVm
            {
                AllLevels = allLevels,
                Curricula = curricula,
                SelectedLevelId = levelId,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            // if AJAX request (or explicit ajax flag), return partial only
            var isAjaxRequest = ajax == "1" ||
                                HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            if (isAjaxRequest)
            {
                // return only the grid partial used by the client
                return PartialView("_CurriculaGridPartial", vm);
            }

            // otherwise fallback to full Index view (this keeps safe fallback)
            vm.SchoolHero = await _heroService.GetHeroAsync(HeroPlacement.School);
            ViewData["Title"] = _L["School.Title"] ?? "School";
            return View("Index", vm);
        }

        // Helper to build localized levels once (cached)
        private async Task<List<SchoolLevelVm>> GetAllLevelsLocalizedAsync()
        {
            // Try cache first (we cache the raw Level objects so we can re-evaluate localization per request)
            if (_cache.TryGetValue<List<Level>>(CACHE_KEY_ALL_LEVELS, out var cachedLevels) && cachedLevels != null)
            {
                return cachedLevels.Select(l => new SchoolLevelVm
                {
                    Id = l.Id,
                    Order = l.Order,
                    // use your helper to get the best localized name for current UI culture
                    Name = LocalizationHelpers.GetLocalizedLevelName(l),
                    CurriculaCountText = (l.Curricula?.Count ?? 0) == 1
                        ? _L["School.Curricula.One"].Value ?? "1 curriculum"
                        : string.Format(_L["School.Curricula.Many"].Value ?? "{0} curricula", (l.Curricula?.Count ?? 0))
                }).ToList();
            }

            // load from DB and cache
            var levels = await _db.Levels
                                 .AsNoTracking()
                                 .Include(l => l.Curricula)
                                 .OrderBy(l => l.Order)
                                 .ToListAsync();

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(30))
                .SetSlidingExpiration(TimeSpan.FromMinutes(5));

            _cache.Set(CACHE_KEY_ALL_LEVELS, levels, cacheOptions);

            return levels.Select(l => new SchoolLevelVm
            {
                Id = l.Id,
                Order = l.Order,
                Name = LocalizationHelpers.GetLocalizedLevelName(l),
                CurriculaCountText = (l.Curricula?.Count ?? 0) == 1
                    ? _L["School.Curricula.One"].Value ?? "1 curriculum"
                    : string.Format(_L["School.Curricula.Many"].Value ?? "{0} curricula", (l.Curricula?.Count ?? 0))
            }).ToList();
        }

        // Internal helper that returns page of curricula and totalCount
        // The curricula returned are mapped to CurriculumSummaryVm and respect the access flags
        private async Task<(List<CurriculumSummaryVm> Items, int TotalCount)> ListInternalAsync(
            int? levelId, int page, int pageSize, bool isAdmin, bool studentIsAllowed, List<int> assignedCurriculumIds)
        {
            var curriculaQuery = _db.Curricula
                                   .AsNoTracking()
                                   .Include(c => c.SchoolModules)
                                   .OrderBy(c => c.Order)
                                   .AsQueryable();

            if (levelId.HasValue)
                curriculaQuery = curriculaQuery.Where(c => c.LevelId == levelId.Value);

            var totalCount = await curriculaQuery.CountAsync();

            var slice = await curriculaQuery
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .Select(c => new
                        {
                            c.Id,
                            c.Title,
                            c.Description,
                            c.CoverImageKey,
                            ModuleCount = c.SchoolModules != null ? c.SchoolModules.Count : 0,
                            c.Order,
                            c.LevelId
                        })
                        .ToListAsync();

            var curricula = new List<CurriculumSummaryVm>();

            foreach (var i in slice)
            {
                string? publicUrl = null;

                if (!string.IsNullOrWhiteSpace(i.CoverImageKey))
                {
                    try
                    {
                        publicUrl = await _fileStorage.GetPublicUrlAsync(i.CoverImageKey);
                    }
                    catch
                    {
                        publicUrl = null; // or a placeholder
                    }
                }

                var isAccessible = isAdmin || (studentIsAllowed && assignedCurriculumIds != null && assignedCurriculumIds.Contains(i.Id));

                curricula.Add(new CurriculumSummaryVm
                {
                    Id = i.Id,
                    Title = i.Title,
                    Description = i.Description,
                    CoverImageKey = i.CoverImageKey,
                    CoverImageUrl = publicUrl,
                    ModuleCount = i.ModuleCount,
                    Order = i.Order,
                    IsAccessible = isAccessible
                });
            }
            return (curricula, totalCount);
        }

        // using System.Globalization; (if not already present)
        public async Task<IActionResult> Curriculum(int id)
        {
            var curriculum = await _db.Curricula
                                      .AsNoTracking()
                                      .Include(c => c.SchoolModules)
                                          .ThenInclude(m => m.SchoolLessons)
                                      .FirstOrDefaultAsync(c => c.Id == id);

            if (curriculum == null) return NotFound();

            // --- Authorization: only Admin or Students with Student.IsAllowed == true AND assigned to this curriculum ---
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                // not authenticated -> ask to login
                return Challenge();
            }

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            if (!isAdmin)
            {
                var isStudent = await _userManager.IsInRoleAsync(user, "Student");
                if (!isStudent)
                {
                    TempData["Error"] = _L["School.AccessDenied"].Value ?? "You don't have access to view this curriculum.";
                    return RedirectToAction("Index");
                }

                // student path: must have Student record and IsAllowed true
                var studentRecord = await _db.Students
                                             .AsNoTracking()
                                             .FirstOrDefaultAsync(s => s.Id == user.Id);

                if (studentRecord == null || !studentRecord.IsAllowed)
                {
                    TempData["Error"] = _L["School.NotAllowedMessage"].Value ?? "You are not allowed to view this curriculum.";
                    return RedirectToAction("Index");
                }

                // AND must be explicitly assigned this curriculum via StudentCurricula join table
                var assigned = await _db.StudentCurricula
                                        .AsNoTracking()
                                        .AnyAsync(sc => sc.StudentId == user.Id && sc.CurriculumId == id);

                if (!assigned)
                {
                    // Not assigned -> deny
                    TempData["Error"] = _L["School.NotAssignedToCurriculum"].Value ?? "You don't have access to this curriculum.";
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

            // batch-load files (unchanged)
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



