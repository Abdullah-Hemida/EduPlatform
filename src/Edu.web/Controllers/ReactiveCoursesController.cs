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
            var query = _db.ReactiveCourses
                .AsNoTracking()
                .Include(c => c.Teacher).ThenInclude(t => t.User)
                .Where(c => c.EndDate >= DateTime.UtcNow && !c.IsArchived) // optional: show active/upcoming courses only
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var t = q.Trim();
                query = query.Where(c => EF.Functions.Like(c.Title!, $"%{t}%") ||
                                         EF.Functions.Like(c.Description!, $"%{t}%") ||
                                         (c.Teacher != null && EF.Functions.Like(c.Teacher.User!.FullName!, $"%{t}%")));
            }

            var list = await query.OrderByDescending(c => c.Id).ToListAsync();

            var vm = new ReactiveCourseIndexVm
            {
                Query = q,
                Courses = new List<ReactiveCourseListItemVm>()
            };

            foreach (var c in list)
            {
                var coverKeyOrUrl = !string.IsNullOrEmpty(c.CoverImageKey) ? c.CoverImageKey : c.CoverImageKey; // prefer Key (if stored); if you store URL in CoverImageKey adapt accordingly
                string? coverPublicUrl = null;
                if (!string.IsNullOrEmpty(c.CoverImageKey))
                {
                    coverPublicUrl = await _fileStorage.GetPublicUrlAsync(c.CoverImageKey);
                }
                // fallback to null (view will gracefully handle)
                vm.Courses.Add(new ReactiveCourseListItemVm
                {
                    Id = c.Id,
                    Title = c.Title,
                    ShortDescription = c.Description?.Length > 240 ? c.Description.Substring(0, 240) + "…" : c.Description,
                    CoverPublicUrl = coverPublicUrl,
                    TeacherName = c.Teacher?.User?.FullName,
                    DurationMonths = c.DurationMonths,
                    PricePerMonthLabel = c.PricePerMonth.ToEuro(),
                    Capacity = c.Capacity,
                    StartDateUtc = c.StartDate,
                    EndDateUtc = c.EndDate,
                    MonthsCount = c.DurationMonths,
                    IntroVideoUrl = c.IntroVideoUrl
                });
            }

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

            string? coverPublicUrl = null;
            if (!string.IsNullOrEmpty(course.CoverImageKey))
                coverPublicUrl = await _fileStorage.GetPublicUrlAsync(course.CoverImageKey);

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

            var monthsVm = course.Months
                .OrderBy(m => m.MonthIndex)
                .Select(m => new ReactiveCourseMonthVm
                {
                    Id = m.Id,
                    MonthIndex = m.MonthIndex,
                    MonthStartUtc = m.MonthStartUtc,
                    MonthEndUtc = m.MonthEndUtc,
                    IsReadyForPayment = m.IsReadyForPayment,
                    LessonsCount = m.Lessons?.Count ?? 0,
                    MonthPaymentsCount = m.MonthPayments?.Count ?? 0
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
                HasPendingEnrollment = hasPendingEnrollment
            };

            // **Use the helper here** to parse the YouTube id (null if none)
            vm.IntroYouTubeId = YouTubeHelper.ExtractYouTubeId(course.IntroVideoUrl);

            return View(vm);
        }
    }
}

