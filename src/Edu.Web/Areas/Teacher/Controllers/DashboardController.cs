
using Edu.Application.IServices;
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
        private readonly IFileStorageService _fileStorage;

        public DashboardController(ApplicationDbContext db,
                                   UserManager<ApplicationUser> userManager,
                                   IStringLocalizer<SharedResource> localizer,
                                   IFileStorageService fileStorage)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            _fileStorage = fileStorage ?? throw new ArgumentNullException(nameof(fileStorage));
        }

        // GET: Teacher/Dashboard
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // basic teacher check
            var teacherExists = await _db.Teachers.AsNoTracking().AnyAsync(t => t.Id == user.Id);
            if (!teacherExists) return Forbid();

            var vm = new TeacherDashboardVm();

            // ---------------------------
            // 1) top private courses (no parallel DB calls)
            // ---------------------------
            var topCoursesRaw = await _db.PrivateCourses
                .AsNoTracking()
                .Where(c => c.TeacherId == user.Id)
                .OrderByDescending(c => c.Id)
                .Take(8)
                .Select(c => new
                {
                    c.Id,
                    c.Title,
                    c.CoverImageKey,
                    c.IsPublished,
                    c.Price,
                    // lesson count via subquery
                    LessonCount = _db.PrivateLessons.Count(l => l.PrivateCourseId == c.Id),
                    CategoryNameEn = c.Category != null ? c.Category.NameEn : null,
                    CategoryNameIt = c.Category != null ? c.Category.NameIt : null,
                    CategoryNameAr = c.Category != null ? c.Category.NameAr : null
                })
                .ToListAsync();

            // ---------------------------
            // 2) simple scalar counts (sequential)
            // ---------------------------
            var totalPrivateCourses = await _db.PrivateCourses.AsNoTracking().CountAsync(c => c.TeacherId == user.Id);
            var totalReactiveCourses = await _db.ReactiveCourses.AsNoTracking().CountAsync(c => c.TeacherId == user.Id);
            var privatePublishedCourses = await _db.PrivateCourses.AsNoTracking().CountAsync(c => c.TeacherId == user.Id && c.IsPublished);

            // ---------------------------
            // 3) upcoming bookings (list + count)
            // ---------------------------
            var now = DateTime.UtcNow;
            var bookingsQuery = _db.Bookings
                .AsNoTracking()
                .Where(b => b.TeacherId == user.Id && b.RequestedDateUtc >= now.AddDays(-7));

            var upcomingBookings = await bookingsQuery
                .OrderBy(b => b.RequestedDateUtc)
                .Take(6)
                .Select(b => new BookingSummaryVm
                {
                    Id = b.Id,
                    RequestedDateUtc = b.RequestedDateUtc,
                    StudentName = b.Student != null && b.Student.User != null ? b.Student.User.FullName : (b.StudentId ?? ""),
                    StudentEmail = b.Student != null && b.Student.User != null ? b.Student.User.Email : null,
                    Status = b.Status,
                    MeetUrl = b.MeetUrl,
                    Price = b.Price,
                    PriceLabel = b.Price.ToEuro()
                })
                .ToListAsync();

            var upcomingBookingsCount = await bookingsQuery.CountAsync();

            // ---------------------------
            // 4) purchase requests (based on teacher's private courses)
            // ---------------------------
            var teacherCourseIds = await _db.PrivateCourses
                .AsNoTracking()
                .Where(c => c.TeacherId == user.Id)
                .Select(c => c.Id)
                .ToListAsync();

            var prBaseQuery = _db.PurchaseRequests
                                 .AsNoTracking()
                                 .Where(pr => teacherCourseIds.Contains(pr.PrivateCourseId));

            var pendingPurchaseRequests = await prBaseQuery.CountAsync(pr => pr.Status == PurchaseStatus.Pending);

            var recentPr = await prBaseQuery
                .OrderByDescending(pr => pr.RequestDateUtc)
                .Take(6)
                .Select(pr => new PurchaseRequestSummaryVm
                {
                    Id = pr.Id,
                    PrivateCourseId = pr.PrivateCourseId,
                    CourseTitle = pr.PrivateCourse != null ? pr.PrivateCourse.Title : null,
                    StudentName = pr.Student != null && pr.Student.User != null ? pr.Student.User.FullName : (pr.StudentId ?? ""),
                    RequestDateUtc = pr.RequestDateUtc,
                    Status = pr.Status,
                    Amount = pr.Amount,
                    AmountLabel = pr.Amount.ToEuro()
                })
                .ToListAsync();

            // ---------------------------
            // 5) reactive courses top summaries
            // ---------------------------
            var reactiveCoursesRaw = await _db.ReactiveCourses
                .AsNoTracking()
                .Where(rc => rc.TeacherId == user.Id)
                .OrderByDescending(rc => rc.Id)
                .Take(8)
                .Select(rc => new
                {
                    rc.Id,
                    rc.Title,
                    rc.CoverImageKey,
                    rc.DurationMonths,
                    rc.PricePerMonth,
                    EnrollmentsCount = _db.ReactiveEnrollments.Count(e => e.ReactiveCourseId == rc.Id),
                    MonthsReadyCount = _db.ReactiveCourseMonths.Count(m => m.ReactiveCourseId == rc.Id && m.IsReadyForPayment)
                })
                .ToListAsync();

            // ---------------------------
            // 6) earnings sums (sequential)
            // ---------------------------
            var reactivePaidSum = await _db.ReactiveEnrollmentMonthPayments
                .AsNoTracking()
                .Where(p => p.Status == EnrollmentMonthPaymentStatus.Paid &&
                            p.ReactiveCourseMonth != null &&
                            p.ReactiveCourseMonth.ReactiveCourse != null &&
                            p.ReactiveCourseMonth.ReactiveCourse.TeacherId == user.Id)
                .SumAsync(p => (decimal?)p.Amount);

            var bookingsPaidSum = await _db.Bookings
                .AsNoTracking()
                .Where(b => b.TeacherId == user.Id && b.Status == BookingStatus.Paid)
                .Select(b => (decimal?)b.Price)
                .SumAsync();

            var purchasesCompletedSum = await _db.PurchaseRequests
                .AsNoTracking()
                .Where(pr => teacherCourseIds.Contains(pr.PrivateCourseId) && pr.Status == PurchaseStatus.Completed)
                .Select(pr => (decimal?)pr.Amount)
                .SumAsync();

            // ---------------------------
            // Resolve cover images in parallel (file storage is safe to call concurrently)
            // ---------------------------
            var allKeys = topCoursesRaw.Select(t => t.CoverImageKey)
                           .Concat(reactiveCoursesRaw.Select(r => r.CoverImageKey))
                           .Where(k => !string.IsNullOrEmpty(k))
                           .Distinct(StringComparer.OrdinalIgnoreCase)
                           .ToList();

            var resolved = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            if (allKeys.Any())
            {
                var tasks = allKeys.Select(async k =>
                {
                    try { return (Key: k, Url: await _fileStorage.GetPublicUrlAsync(k)); }
                    catch { return (Key: k, Url: (string?)null); }
                }).ToArray();

                var finished = await Task.WhenAll(tasks);
                foreach (var f in finished) resolved[f.Key] = f.Url;
            }

            // ---------------------------
            // map to VM objects
            // ---------------------------
            var topCourses = topCoursesRaw.Select(t =>
            {
                var category = new Category
                {
                    NameEn = t.CategoryNameEn ?? string.Empty,
                    NameIt = t.CategoryNameIt ?? string.Empty,
                    NameAr = t.CategoryNameAr ?? string.Empty
                };

                return new CourseSummaryVm
                {
                    Id = t.Id,
                    Title = t.Title,
                    CoverImageUrl = !string.IsNullOrEmpty(t.CoverImageKey) && resolved.TryGetValue(t.CoverImageKey, out var url) ? url : null,
                    IsPublished = t.IsPublished,
                    Price = t.Price,
                    PriceLabel = t.Price.ToEuro(),
                    LessonCount = t.LessonCount,
                    CategoryName = LocalizationHelpers.GetLocalizedCategoryName(category)
                };
            }).ToList();

            var reactiveCourses = reactiveCoursesRaw.Select(rc => new ReactiveCourseSummaryVm
            {
                Id = rc.Id,
                Title = rc.Title,
                CoverPublicUrl = !string.IsNullOrEmpty(rc.CoverImageKey) && resolved.TryGetValue(rc.CoverImageKey, out var url) ? url : null,
                DurationMonths = rc.DurationMonths,
                PricePerMonth = rc.PricePerMonth,
                PricePerMonthLabel = rc.PricePerMonth.ToEuro(),
                EnrollmentsCount = rc.EnrollmentsCount,
                MonthsReadyCount = rc.MonthsReadyCount
            }).ToList();

            // ---------- populate VM ----------
            vm.TotalCourses = totalPrivateCourses + totalReactiveCourses;
            vm.PublishedCourses = privatePublishedCourses + totalReactiveCourses;

            vm.Courses = topCourses;

            vm.UpcomingBookingsCount = upcomingBookingsCount;
            vm.UpcomingBookings = upcomingBookings;

            vm.PendingPurchaseRequests = pendingPurchaseRequests;
            vm.RecentPurchaseRequests = recentPr;

            vm.ReactiveCourses = reactiveCourses;

            // earnings totals
            vm.TotalEarnings = (reactivePaidSum ?? 0m) + (bookingsPaidSum ?? 0m) + (purchasesCompletedSum ?? 0m);

            // monthly breakdown last N months
            int monthsBack = 6;
            var nowUtc = DateTime.UtcNow;
            var periodStart = new DateTime(nowUtc.Year, nowUtc.Month, 1).AddMonths(-(monthsBack - 1));

            var reactiveInRange = await _db.ReactiveEnrollmentMonthPayments
                .AsNoTracking()
                .Where(p => p.Status == EnrollmentMonthPaymentStatus.Paid &&
                            p.ReactiveCourseMonth != null &&
                            p.ReactiveCourseMonth.ReactiveCourse != null &&
                            p.ReactiveCourseMonth.ReactiveCourse.TeacherId == user.Id &&
                            (p.PaidAtUtc ?? p.CreatedAtUtc) >= periodStart)
                .Select(p => new { Amount = (decimal?)p.Amount, Date = (DateTime?)(p.PaidAtUtc ?? p.CreatedAtUtc) })
                .ToListAsync();

            var bookingsInRange = await _db.Bookings
                .AsNoTracking()
                .Where(b => b.TeacherId == user.Id && b.Status == BookingStatus.Paid)
                .Select(b => new { Amount = (decimal?)b.Price, Date = (DateTime?)(b.PaidAtUtc ?? b.RequestedDateUtc) })
                .Where(x => x.Date >= periodStart)
                .ToListAsync();

            var purchasesInRange = await _db.PurchaseRequests
                .AsNoTracking()
                .Where(pr => teacherCourseIds.Contains(pr.PrivateCourseId) && pr.Status == PurchaseStatus.Completed && pr.RequestDateUtc >= periodStart)
                .Select(pr => new { Amount = (decimal?)pr.Amount, Date = (DateTime?)pr.RequestDateUtc })
                .ToListAsync();

            var combined = new List<(decimal Amount, DateTime Date)>();
            combined.AddRange(reactiveInRange.Where(x => x.Date.HasValue).Select(x => (Amount: x.Amount ?? 0m, Date: x.Date!.Value)));
            combined.AddRange(bookingsInRange.Where(x => x.Date.HasValue).Select(x => (Amount: x.Amount ?? 0m, Date: x.Date!.Value)));
            combined.AddRange(purchasesInRange.Where(x => x.Date.HasValue).Select(x => (Amount: x.Amount ?? 0m, Date: x.Date!.Value)));

            var grouped = combined.GroupBy(x => (Year: x.Date.Year, Month: x.Date.Month))
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





