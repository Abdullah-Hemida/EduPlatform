using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Web.Resources;
using Edu.Web.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Edu.Web.Controllers
{
    [Route("[controller]")]
    public class ReactiveCoursesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fileStorage;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public ReactiveCoursesController(
            ApplicationDbContext db,
            IFileStorageService fileStorage,
            UserManager<ApplicationUser> userManager,
            IStringLocalizer<SharedResource> localizer)
        {
            _db = db;
            _fileStorage = fileStorage;
            _userManager = userManager;
            _localizer = localizer;
        }

        // GET: /ReactiveCourses
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index(string? q = null)
        {
            // base query: active/upcoming, not archived
            var baseQuery = _db.ReactiveCourses
                .AsNoTracking()
                .Where(c => c.EndDate >= DateTime.UtcNow && !c.IsArchived);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                // project only what we need (avoid loading related graph)
                baseQuery = baseQuery.Where(c =>
                    EF.Functions.Like(c.Title ?? "", $"%{term}%") ||
                    EF.Functions.Like(c.Description ?? "", $"%{term}%") ||
                    // teacher's full name may be null; use left join projection below
                    false
                );
            }

            // Project to a light shape that includes teacher name to avoid Include() heavy loads
            var projected = baseQuery
                .Select(c => new
                {
                    c.Id,
                    c.Title,
                    c.Description,
                    c.CoverImageKey,
                    IntroVideoUrl = c.IntroVideoUrl,
                    c.DurationMonths,
                    c.PricePerMonth,
                    c.Capacity,
                    c.StartDate,
                    c.EndDate,
                    TeacherName = c.Teacher != null && c.Teacher.User != null ? c.Teacher.User.FullName : null
                })
                .OrderByDescending(c => c.Id);

            var list = await projected.ToListAsync();

            // Resolve distinct cover keys in batch (best-effort)
            var keys = list.Select(x => x.CoverImageKey).Where(k => !string.IsNullOrEmpty(k)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var coverMap = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            if (keys.Any())
            {
                var resolveTasks = keys.Select(async k =>
                {
                    try
                    {
                        var url = await _fileStorage.GetPublicUrlAsync(k!);
                        return (Key: k!, Url: string.IsNullOrEmpty(url) ? null : url);
                    }
                    catch
                    {
                        return (Key: k!, Url: (string?)null);
                    }
                }).ToArray();

                var results = await Task.WhenAll(resolveTasks);
                foreach (var r in results)
                {
                    if (!coverMap.ContainsKey(r.Key))
                        coverMap[r.Key] = r.Url;
                }
            }

            var vm = new ReactiveCourseIndexVm
            {
                Query = q,
                Courses = list.Select(c => new ReactiveCourseListItemVm
                {
                    Id = c.Id,
                    Title = c.Title,
                    ShortDescription = string.IsNullOrEmpty(c.Description) ? null :
                        (c.Description.Length > 240 ? c.Description.Substring(0, 240) + "…" : c.Description),
                    CoverPublicUrl = !string.IsNullOrEmpty(c.CoverImageKey) && coverMap.TryGetValue(c.CoverImageKey!, out var pub) ? pub : null,
                    TeacherName = c.TeacherName,
                    DurationMonths = c.DurationMonths,
                    PricePerMonthLabel = c.PricePerMonth.ToEuro(),
                    Capacity = c.Capacity,
                    StartDateUtc = c.StartDate,
                    EndDateUtc = c.EndDate,
                    MonthsCount = c.DurationMonths,
                    IntroVideoUrl = c.IntroVideoUrl
                }).ToList()
            };

            return View(vm);
        }

        // GET: /ReactiveCourses/Details/5
        [HttpGet("Details/{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            var course = await _db.ReactiveCourses
                .AsNoTracking()
                .Include(c => c.Teacher).ThenInclude(t => t.User)
                .Include(c => c.Months).ThenInclude(m => m.Lessons)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null) return NotFound();

            // resolve cover public url (best-effort)
            string? coverPublicUrl = null;
            if (!string.IsNullOrEmpty(course.CoverImageKey))
            {
                try
                {
                    coverPublicUrl = await _fileStorage.GetPublicUrlAsync(course.CoverImageKey);
                }
                catch
                {
                    coverPublicUrl = null;
                }
            }

            // student / enrollment info
            var userId = _userManager.GetUserId(User);
            bool isEnrolled = false;
            bool isPaidEnrollment = false;
            bool hasPendingEnrollment = false;

            if (!string.IsNullOrEmpty(userId))
            {
                var enrollment = await _db.ReactiveEnrollments
                    .AsNoTracking()
                    .Include(e => e.MonthPayments)
                    .FirstOrDefaultAsync(e => e.ReactiveCourseId == id && e.StudentId == userId);

                if (enrollment != null)
                {
                    isEnrolled = true;
                    isPaidEnrollment = enrollment.IsPaid;
                    hasPendingEnrollment = enrollment.MonthPayments.Any(m => m.Status == EnrollmentMonthPaymentStatus.Pending);
                }
            }

            // prepare months VMs
            var months = course.Months.OrderBy(m => m.MonthIndex).ToList();
            var monthIds = months.Select(m => m.Id).Where(i => i > 0).ToList();

            // Get payments count per month in one query (avoid N+1)
            var paymentsCountMap = new Dictionary<int, int>();
            if (monthIds.Any())
            {
                var grouped = await _db.ReactiveEnrollmentMonthPayments
                    .AsNoTracking()
                    .Where(pm => monthIds.Contains(pm.ReactiveCourseMonthId))
                    .GroupBy(pm => pm.ReactiveCourseMonthId)
                    .Select(g => new { MonthId = g.Key, Count = g.Count() })
                    .ToListAsync();

                paymentsCountMap = grouped.ToDictionary(x => x.MonthId, x => x.Count);
            }

            var monthsVm = months.Select(m => new ReactiveCourseMonthVm
            {
                Id = m.Id,
                MonthIndex = m.MonthIndex,
                MonthStartUtc = m.MonthStartUtc,
                MonthEndUtc = m.MonthEndUtc,
                IsReadyForPayment = m.IsReadyForPayment,
                LessonsCount = m.Lessons?.Count ?? 0,
                MonthPaymentsCount = paymentsCountMap.TryGetValue(m.Id, out var ct) ? ct : 0
            }).ToList();

            var vm = new ReactiveCourseDetailsVm
            {
                Id = course.Id,
                Title = course.Title,
                Description = course.Description,
                CoverPublicUrl = coverPublicUrl,
                IntroVideoUrl = course.IntroVideoUrl,
                TeacherName = course.Teacher?.User?.FullName,
                PricePerMonthLabel = course.PricePerMonth.ToEuro(),
                DurationMonths = course.DurationMonths,
                Capacity = course.Capacity,
                StartDateUtc = course.StartDate,
                EndDateUtc = course.EndDate,
                Months = monthsVm,
                IsEnrolled = isEnrolled,
                IsPaidEnrollment = isPaidEnrollment,
                HasPendingEnrollment = hasPendingEnrollment,
                IntroYouTubeId = YouTubeHelper.ExtractYouTubeId(course.IntroVideoUrl)
            };

            return View(vm);
        }
    }
}


