
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Web.Areas.Teacher.ViewModels;
using Edu.Web.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Edu.Web.Areas.Teacher.Controllers
{
    [Area("Teacher")]
    [Authorize(Roles = "Teacher")]
    public class DashboardController : TeacherBaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public DashboardController(ApplicationDbContext db,
                                   UserManager<ApplicationUser> userManager,
                                   IStringLocalizer<SharedResource> localizer)
        {
            _db = db;
            _userManager = userManager;
            _localizer = localizer;
        }

        // GET: Teacher/Dashboard
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // basic teacher check (Teacher table stores additional info)
            var teacher = await _db.Teachers.AsNoTracking().FirstOrDefaultAsync(t => t.Id == user.Id);
            if (teacher == null) return Forbid();

            var vm = new TeacherDashboardVm();

            //
            // Private courses summary (existing)
            //
            var coursesQuery = _db.PrivateCourses
                                  .AsNoTracking()
                                  .Where(c => c.TeacherId == user.Id)
                                  .Include(c => c.Category)
                                  .Include(c => c.PrivateLessons);

            var courses = await coursesQuery
                            .OrderByDescending(c => c.Id)
                            .Take(8)
                            .ToListAsync();
            int TotalPriveteCourses = await _db.PrivateCourses.CountAsync(c => c.TeacherId == user.Id);
            int TotalReactiveCourses = await _db.ReactiveCourses.CountAsync(c => c.TeacherId == user.Id);
            int PrivatePublishedCourses = await _db.PrivateCourses.CountAsync(c => c.TeacherId == user.Id && c.IsPublished);
            vm.TotalCourses = TotalPriveteCourses + TotalReactiveCourses;
            vm.PublishedCourses = PrivatePublishedCourses + TotalReactiveCourses;

            vm.Courses = courses.Select(c => new CourseSummaryVm
            {
                Id = c.Id,
                Title = c.Title,
                CoverImageUrl = c.CoverImageUrl,
                IsPublished = c.IsPublished,
                Price = c.Price,
                PriceLabel = c.Price.ToEuro(),
                LessonCount = c.PrivateLessons?.Count ?? 0,
                CategoryName = c.Category?.Name
            }).ToList();

            //
            // Upcoming bookings (requested date in future or recent pending/accepted)
            //
            var now = DateTime.UtcNow;
            var bookings = await _db.Bookings
                                    .AsNoTracking()
                                    .Where(b => b.TeacherId == user.Id &&
                                                (b.Status == BookingStatus.Pending) &&
                                                b.RequestedDateUtc >= now.AddDays(-7))
                                    .Include(b => b.Student).ThenInclude(s => s.User)
                                    .OrderBy(b => b.RequestedDateUtc)
                                    .Take(6)
                                    .ToListAsync();

            vm.UpcomingBookingsCount = await _db.Bookings.CountAsync(b => b.TeacherId == user.Id &&
                                                                           (b.Status == BookingStatus.Pending) &&
                                                                           b.RequestedDateUtc >= now.AddDays(-7));

            vm.UpcomingBookings = bookings.Select(b => new BookingSummaryVm
            {
                Id = b.Id,
                RequestedDateUtc = b.RequestedDateUtc,
                StudentName = b.Student?.User?.FullName ?? b.Student?.User?.UserName,
                StudentEmail = b.Student?.User?.Email,
                Status = b.Status.ToString(),
                MeetUrl = b.MeetUrl,
                Price = b.Price,
                PriceLabel = b.Price.ToEuro()
            }).ToList();

            //
            // Purchase requests
            //
            var prQuery = _db.PurchaseRequests
                              .AsNoTracking()
                              .Where(pr => _db.PrivateCourses.Where(c => c.TeacherId == user.Id).Select(c => c.Id).Contains(pr.PrivateCourseId))
                              .Include(pr => pr.PrivateCourse)
                              .Include(pr => pr.Student).ThenInclude(s => s.User);

            vm.PendingPurchaseRequests = await prQuery.CountAsync(pr => pr.Status == PurchaseStatus.Pending);

            var recentPr = await prQuery.OrderByDescending(pr => pr.RequestDateUtc).Take(6).ToListAsync();
            vm.RecentPurchaseRequests = recentPr.Select(pr => new PurchaseRequestSummaryVm
            {
                Id = pr.Id,
                PrivateCourseId = pr.PrivateCourseId,
                CourseTitle = pr.PrivateCourse?.Title,
                StudentName = pr.Student?.User?.FullName ?? pr.Student?.User?.UserName,
                RequestDateUtc = pr.RequestDateUtc,
                Status = pr.Status.ToString(),
                Amount = pr.Amount,
                AmountLabel = pr.Amount.ToEuro()
            }).ToList();

            //
            // Reactive courses summary (teacher's reactive courses)
            //
            var reactiveQuery = _db.ReactiveCourses
                                  .AsNoTracking()
                                  .Where(rc => rc.TeacherId == user.Id)
                                  .Include(rc => rc.Months).ThenInclude(m => m.MonthPayments)
                                  .Include(rc => rc.Enrollments);

            var reactiveCourses = await reactiveQuery
                                    .OrderByDescending(rc => rc.Id)
                                    .Take(8)
                                    .ToListAsync();

            vm.ReactiveCourses = reactiveCourses.Select(rc => new ReactiveCourseSummaryVm
            {
                Id = rc.Id,
                Title = rc.Title,
                CoverPublicUrl = string.IsNullOrEmpty(rc.CoverImageKey) ? null : rc.CoverImageKey,
                DurationMonths = rc.DurationMonths,
                PricePerMonth = rc.PricePerMonth,
                PricePerMonthLabel = rc.PricePerMonth.ToEuro(),
                EnrollmentsCount = rc.Enrollments?.Count ?? 0,
                MonthsReadyCount = rc.Months?.Count(m => m.IsReadyForPayment) ?? 0
            }).ToList();

            //
            // ------------------ COMBINED EARNINGS ------------------
            // Sources:
            //  - ReactiveEnrollmentMonthPayments (Status == Paid)
            //  - Bookings (Status == Paid)
            //  - PurchaseRequests (Status == Completed)
            //

            // reactive month payments (paid) for this teacher
            var reactivePaidQuery = _db.ReactiveEnrollmentMonthPayments
                .AsNoTracking()
                .Where(p => p.Status == EnrollmentMonthPaymentStatus.Paid &&
                            p.ReactiveCourseMonth != null &&
                            p.ReactiveCourseMonth.ReactiveCourse != null &&
                            p.ReactiveCourseMonth.ReactiveCourse.TeacherId == user.Id)
                .Include(p => p.ReactiveCourseMonth).ThenInclude(m => m.ReactiveCourse);

            // bookings that were paid
            var bookingsPaidQuery = _db.Bookings
                .AsNoTracking()
                .Where(b => b.TeacherId == user.Id && b.Status == BookingStatus.Paid)
                .Select(b => new
                {
                    Amount = (decimal?)b.Price,
                    Date = b.PaidAtUtc ?? (DateTime?)b.RequestedDateUtc
                });

            // purchase requests completed for this teacher's private courses
            var teacherCourseIds = await _db.PrivateCourses
                .AsNoTracking()
                .Where(c => c.TeacherId == user.Id)
                .Select(c => c.Id)
                .ToListAsync();

            var purchasesCompletedQuery = _db.PurchaseRequests
                .AsNoTracking()
                .Where(pr => teacherCourseIds.Contains(pr.PrivateCourseId) && pr.Status == PurchaseStatus.Completed)
                .Select(pr => new
                {
                    Amount = (decimal?)pr.Amount,
                    Date = (DateTime?)pr.RequestDateUtc
                });

            var reactiveTotal = await reactivePaidQuery.SumAsync(p => (decimal?)p.Amount) ?? 0m;
            var bookingsTotal = await bookingsPaidQuery.SumAsync(x => x.Amount) ?? 0m;
            var purchasesTotal = await purchasesCompletedQuery.SumAsync(x => x.Amount) ?? 0m;

            vm.TotalEarnings = reactiveTotal + bookingsTotal + purchasesTotal;

            // monthly breakdown for last N months (including current month)
            int monthsBack = 6;
            var nowUtc = DateTime.UtcNow;
            var periodStart = new DateTime(nowUtc.Year, nowUtc.Month, 1).AddMonths(-(monthsBack - 1));

            // reactive payments in range
            var reactiveInRange = await reactivePaidQuery
                .Where(p => (p.PaidAtUtc ?? p.CreatedAtUtc) >= periodStart)
                .Select(p => new { Amount = p.Amount, Date = (DateTime?)(p.PaidAtUtc ?? p.CreatedAtUtc) })
                .ToListAsync();

            // bookings in range
            var bookingsInRange = await bookingsPaidQuery
                .Where(b => b.Date >= periodStart)
                .ToListAsync();

            // purchases in range
            var purchasesInRange = await purchasesCompletedQuery
                .Where(pr => pr.Date >= periodStart)
                .ToListAsync();

            // combine
            var combined = new List<(decimal Amount, DateTime Date)>();

            combined.AddRange(reactiveInRange
                .Where(x => x.Date.HasValue)
                .Select(x => (Amount: x.Amount, Date: x.Date!.Value)));

            combined.AddRange(bookingsInRange
                .Where(x => x.Date.HasValue)
                .Select(x => (Amount: x.Amount ?? 0m, Date: x.Date!.Value)));

            combined.AddRange(purchasesInRange
                .Where(x => x.Date.HasValue)
                .Select(x => (Amount: x.Amount ?? 0m, Date: x.Date!.Value)));

            var grouped = combined
                .GroupBy(x => (Year: x.Date.Year, Month: x.Date.Month))
                .ToDictionary(g => (g.Key.Year, g.Key.Month), g => g.Sum(i => i.Amount));

            var monthlyList = new List<MonthEarningVm>();
            for (int i = monthsBack - 1; i >= 0; i--)
            {
                var dt = nowUtc.AddMonths(-i);
                var y = dt.Year;
                var m = dt.Month;
                grouped.TryGetValue((y, m), out var sum);
                monthlyList.Add(new MonthEarningVm
                {
                    Year = y,
                    Month = m,
                    Amount = sum
                });
            }

            vm.MonthlyEarnings = monthlyList;
            vm.PrepareLabels();

            ViewData["ActivePage"] = "TeacherDashboard";
            return View(vm);
        }
    }
}


