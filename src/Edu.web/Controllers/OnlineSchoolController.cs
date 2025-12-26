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

        public OnlineSchoolController(ApplicationDbContext db, IFileStorageService fileStorage, IStringLocalizer<SharedResource> L,  IHeroService heroService)
        {
            _db = db;
            _fileStorage = fileStorage;
            _L = L;
            _heroService = heroService;
        }

        // GET: /OnlineSchool
        public async Task<IActionResult> Index(int? levelId, int page = 1, int pageSize = 9, string ajax = null)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 6, 50);

            // levels for filter cards (ordered)
            var levels = await _db.Levels
                                  .OrderBy(l => l.Order)
                                  .Select(l => new OnlineSchoolLevelVm
                                  {
                                      Id = l.Id,
                                      Name = LocalizationHelpers.GetLocalizedLevelName(l),
                                      CourseCount = l.OnlineCourses != null ? l.OnlineCourses.Count : 0
                                  })
                                  .ToListAsync();

            // base query: only published courses
            var baseQuery = _db.OnlineCourses
                .AsNoTracking()
                .Where(c => c.IsPublished)
                .Include(c => c.Months)
                .Include(c => c.Lessons);

            if (levelId.HasValue)
            {
                baseQuery = baseQuery.Where(c => c.LevelId == levelId.Value)
                    .Include(c => c.Months)
                    .Include(c => c.Lessons);
            }

            var totalCount = await baseQuery.CountAsync();

            var courses = await baseQuery
                .OrderByDescending(c => c.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // map to cards and resolve cover urls
            var cardTasks = courses.Select(async c => new OnlineCourseCardVm
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description,
                CoverImageUrl = string.IsNullOrEmpty(c.CoverImageKey) ? null : await _fileStorage.GetPublicUrlAsync(c.CoverImageKey),
                PricePerMonthLabel = c.PricePerMonth.ToEuro(), // assumes extension method exists
                DurationMonths = c.DurationMonths,
                LevelId = c.LevelId,
                LessonCount = c.Lessons?.Count ?? 0,
                IsPublished = c.IsPublished
            }).ToList();

            var courseCards = await Task.WhenAll(cardTasks);

            var vm = new OnlineSchoolIndexVm
            {
                SelectedLevelId = levelId,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                AllLevels = levels,
                Courses = courseCards.ToList(),
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

            var vm = new OnlineCoursePublicDetailsVm
            {
                Id = course.Id,
                Title = course.Title,
                Description = course.Description,
                CoverImageUrl = string.IsNullOrEmpty(course.CoverImageKey) ? null : await _fileStorage.GetPublicUrlAsync(course.CoverImageKey),
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
                Lessons = course.Lessons.OrderBy(l => l.Order).Select(l => new OnlineCourseLessonPublicVm
                {
                    Id = l.Id,
                    Title = l.Title,
                    Notes = l.Notes,
                    MeetUrl = l.MeetUrl,
                    RecordedVideoUrl = l.RecordedVideoUrl,
                    ScheduledUtc = l.ScheduledUtc,
                    Order = l.Order,
                    Files = l.Files.Select(f => new OnlineCourseFilePublicVm
                    {
                        Id = f.Id,
                        FileName = f.Name ?? System.IO.Path.GetFileName(f.FileUrl ?? f.StorageKey ?? ""),
                        PublicUrl = !string.IsNullOrEmpty(f.StorageKey) ? _fileStorage.GetPublicUrlAsync(f.StorageKey).Result : (f.FileUrl ?? null)
                    }).ToList()
                }).ToList()
            };

            // optional: localize level name
            var level = await _db.Levels.FindAsync(course.LevelId);
            vm.LevelName = LocalizationHelpers.GetLocalizedLevelName(level);

            return View(vm);
        }
    }
}
