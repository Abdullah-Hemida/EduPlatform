using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Web.Resources;
using Edu.Web.ViewModels;
using Edu.Web.Views.Shared.Components.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Edu.Web.Controllers
{
    public class OnlineSchoolController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fileStorage;
        private readonly IStringLocalizer<SharedResource> _L;
        private readonly IHeroService _heroService;

        public OnlineSchoolController(ApplicationDbContext db, IFileStorageService fileStorage, IStringLocalizer<SharedResource> L, IHeroService heroService)
        {
            _db = db;
            _fileStorage = fileStorage;
            _L = L;
            _heroService = heroService;
        }

        // GET: /OnlineSchool
        public async Task<IActionResult> Index(int? levelId, int page = 1, int pageSize = 9, string? ajax = null)
        {
            page = Math.Max(1, page);
            // keep pageSize within reasonable bounds but allow small pages
            pageSize = Math.Clamp(pageSize, 1, 50);

            // 1) Load levels (ordered) and compute localized names
            var levels = await _db.Levels
                                  .AsNoTracking()
                                  .OrderBy(l => l.Order)
                                  .ToListAsync();

            var levelVms = levels.Select(l => new OnlineSchoolLevelVm
            {
                Id = l.Id,
                Name = LocalizationHelpers.GetLocalizedLevelName(l),
                CourseCount = 0 // fill below
            }).ToList();

            // 2) Fill course counts per level in one query to avoid N+1
            var levelIds = levelVms.Select(l => l.Id).ToList();
            if (levelIds.Any())
            {
                var counts = await _db.OnlineCourses
                                     .AsNoTracking()
                                     .Where(c => c.IsPublished && levelIds.Contains(c.LevelId))
                                     .GroupBy(c => c.LevelId)
                                     .Select(g => new { LevelId = g.Key, Count = g.Count() })
                                     .ToListAsync();

                var countsMap = counts.ToDictionary(x => x.LevelId, x => x.Count);
                foreach (var lv in levelVms)
                {
                    if (countsMap.TryGetValue(lv.Id, out var c)) lv.CourseCount = c;
                }
            }

            // 3) Base query: only published courses
            var baseQuery = _db.OnlineCourses
                .AsNoTracking()
                .Where(c => c.IsPublished);

            if (levelId.HasValue)
            {
                baseQuery = baseQuery.Where(c => c.LevelId == levelId.Value);
            }

            var totalCount = await baseQuery.CountAsync();

            var courses = await baseQuery
                .OrderByDescending(c => c.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                // don't include Files/Months/Lessons here to keep this query light; we only need counts
                .ToListAsync();

            // 4) Resolve distinct cover keys in parallel (best-effort)
            var distinctKeys = courses
                .Select(c => c.CoverImageKey)
                .Where(k => !string.IsNullOrEmpty(k))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var coverMap = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            if (distinctKeys.Any())
            {
                var tasks = distinctKeys.Select(async key =>
                {
                    try
                    {
                        var url = await _fileStorage.GetPublicUrlAsync(key!);
                        return (Key: key!, Url: string.IsNullOrEmpty(url) ? null : url);
                    }
                    catch
                    {
                        return (Key: key!, Url: (string?)null);
                    }
                }).ToArray();

                var results = await Task.WhenAll(tasks);
                foreach (var r in results)
                {
                    if (!coverMap.ContainsKey(r.Key))
                        coverMap[r.Key] = r.Url;
                }
            }

            // 5) Map to card VMs using resolved cover urls
            var courseCards = courses.Select(c => new OnlineCourseCardVm
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description,
                CoverImageUrl = !string.IsNullOrEmpty(c.CoverImageKey) && coverMap.TryGetValue(c.CoverImageKey!, out var found) ? found : null,
                PricePerMonthLabel = c.PricePerMonth.ToEuro(),
                DurationMonths = c.DurationMonths,
                LevelId = c.LevelId,
                LessonCount = c.Lessons?.Count ?? 0,
                IsPublished = c.IsPublished
            }).ToList();

            var vm = new OnlineSchoolIndexVm
            {
                SelectedLevelId = levelId,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                AllLevels = levelVms,
                Courses = courseCards,
                SchoolHero = await _heroService.GetHeroAsync(HeroPlacement.School)
            };

            // If AJAX request (partial reload), return partial only
            if (!string.IsNullOrEmpty(ajax) && ajax == "1")
            {
                return PartialView("_CoursesGridPartial", vm);
            }

            return View(vm);
        }

        // GET: /OnlineSchool/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var course = await _db.OnlineCourses
                .AsNoTracking()
                .Include(c => c.Months)
                .Include(c => c.Lessons)
                    .ThenInclude(l => l.Files)
                .FirstOrDefaultAsync(c => c.Id == id && c.IsPublished);

            if (course == null) return NotFound();

            // Prepare VM (map basic fields)
            var vm = new OnlineCoursePublicDetailsVm
            {
                Id = course.Id,
                Title = course.Title,
                Description = course.Description,
                CoverImageUrl = null, // fill next
                IntroductionVideoUrl = course.IntroductionVideoUrl,
                PricePerMonthLabel = course.PricePerMonth.ToEuro(),
                DurationMonths = course.DurationMonths,
                LevelId = course.LevelId,
                TeacherName = course.TeacherName,
                Months = course.Months.OrderBy(m => m.MonthIndex).Select(m => new OnlineCourseMonthPublicVm
                {
                    Id = m.Id,
                    MonthIndex = m.MonthIndex,
                    MonthStartUtc = m.MonthStartUtc,
                    MonthEndUtc = m.MonthEndUtc,
                    IsReadyForPayment = m.IsReadyForPayment
                }).ToList(),
                Lessons = new List<OnlineCourseLessonPublicVm>()
            };

            // Resolve cover image url (best-effort)
            if (!string.IsNullOrEmpty(course.CoverImageKey))
            {
                try
                {
                    vm.CoverImageUrl = await _fileStorage.GetPublicUrlAsync(course.CoverImageKey);
                }
                catch
                {
                    vm.CoverImageUrl = null;
                }
            }

            // Map lessons & resolve file public urls in batch to avoid per-file blocking calls
            var lessons = course.Lessons.OrderBy(l => l.Order).ToList();
            var fileResources = lessons.SelectMany(l => l.Files ?? Enumerable.Empty<FileResource>()).ToList();

            // distinct keys to resolve
            var keys = fileResources
                        .Where(f => !string.IsNullOrEmpty(f.StorageKey))
                        .Select(f => f.StorageKey!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

            var filesMap = new Dictionary<int, string?>();
            if (keys.Any())
            {
                // resolve keys in parallel
                var resolveTasks = keys.Select(async k =>
                {
                    try
                    {
                        var url = await _fileStorage.GetPublicUrlAsync(k);
                        return (Key: k, Url: string.IsNullOrEmpty(url) ? null : url);
                    }
                    catch
                    {
                        return (Key: k, Url: (string?)null);
                    }
                }).ToArray();

                var resolved = await Task.WhenAll(resolveTasks);
                var urlMap = resolved.ToDictionary(x => x.Key, x => x.Url, StringComparer.OrdinalIgnoreCase);

                // assign public url per file record id
                foreach (var fr in fileResources)
                {
                    string? publicUrl = null;
                    if (!string.IsNullOrEmpty(fr.StorageKey) && urlMap.TryGetValue(fr.StorageKey, out var pu))
                        publicUrl = pu;
                    else if (!string.IsNullOrEmpty(fr.FileUrl))
                        publicUrl = fr.FileUrl;

                    filesMap[fr.Id] = publicUrl;
                }
            }
            else
            {
                // no storage keys, fall back to FileUrl values
                foreach (var fr in fileResources)
                {
                    filesMap[fr.Id] = string.IsNullOrEmpty(fr.FileUrl) ? null : fr.FileUrl;
                }
            }

            // Build lesson VMs
            vm.Lessons = lessons.Select(l => new OnlineCourseLessonPublicVm
            {
                Id = l.Id,
                Title = l.Title,
                Notes = l.Notes,
                MeetUrl = l.MeetUrl,
                RecordedVideoUrl = l.RecordedVideoUrl,
                ScheduledUtc = l.ScheduledUtc,
                Order = l.Order,
                Files = (l.Files ?? Enumerable.Empty<FileResource>()).Select(f => new OnlineCourseFilePublicVm
                {
                    Id = f.Id,
                    FileName = f.Name ?? System.IO.Path.GetFileName(f.FileUrl ?? f.StorageKey ?? ""),
                    PublicUrl = filesMap.TryGetValue(f.Id, out var pu) ? pu : (f.FileUrl ?? null)
                }).ToList()
            }).ToList();

            // optional: localize level name
            var level = await _db.Levels.FindAsync(course.LevelId);
            vm.LevelName = LocalizationHelpers.GetLocalizedLevelName(level);

            return View(vm);
        }
    }
}

