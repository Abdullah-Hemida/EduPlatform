using Edu.Infrastructure.Data;
using Edu.Domain.Entities;
using Edu.Web.Areas.Admin.ViewModels;
using Edu.Web.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Edu.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class EarningsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IStringLocalizer<SharedResource> _L;
        private readonly ILogger<EarningsController> _logger;

        public EarningsController(ApplicationDbContext db,
                                  IStringLocalizer<SharedResource> localizer,
                                  ILogger<EarningsController> logger)
        {
            _db = db;
            _L = localizer;
            _logger = logger;
        }

        // GET: Admin/Earnings?year=2025
        public async Task<IActionResult> Index(int? year, CancellationToken cancellationToken = default)
        {
            var selectedYear = year ?? DateTime.UtcNow.Year;

            // compute inclusive start, exclusive end for the selected year (UTC)
            var yearStart = new DateTime(selectedYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var yearEnd = yearStart.AddYears(1);

            // fetch teachers (basic projection) - you may prefer only teachers that have earnings, but keeping all is fine
            var teachers = await _db.Teachers
                .AsNoTracking()
                .Include(t => t.User) // small set: ok; if large, project instead
                .OrderBy(t => t.User.FullName)
                .ToListAsync(cancellationToken);

            var teacherMap = teachers.ToDictionary(t => t.Id, t => new TeacherEarningsVm
            {
                TeacherId = t.Id,
                FullName = t.User?.FullName ?? t.User?.UserName ?? "",
                PhotoUrl = t.User?.PhotoUrl,
                PhoneNumber = t.User?.PhoneNumber,
                Months = new decimal[12],
                Total = 0m
            });

            // 1) Bookings: group by teacherId and month based on PaidAtUtc range
            var bookingGroups = await _db.Bookings
                .AsNoTracking()
                .Where(b => b.Status == BookingStatus.Paid
                            && b.TeacherId != null
                            && b.PaidAtUtc >= yearStart && b.PaidAtUtc < yearEnd)
                .GroupBy(b => new { b.TeacherId, Month = b.PaidAtUtc.Value.Month })
                .Select(g => new { TeacherId = g.Key.TeacherId!, Month = g.Key.Month, Sum = g.Sum(x => x.Price) })
                .ToListAsync(cancellationToken);

            foreach (var g in bookingGroups)
            {
                if (!teacherMap.TryGetValue(g.TeacherId, out var vm)) continue;
                var idx = g.Month - 1;
                if (idx >= 0 && idx < 12)
                {
                    vm.Months[idx] += g.Sum;
                    vm.Total += g.Sum;
                }
            }

            // 2) Reactive course month payments: already joined in your version — keep join but use range filter
            var reactivePayments = await (
                from p in _db.ReactiveEnrollmentMonthPayments.AsNoTracking()
                join e in _db.ReactiveEnrollments.AsNoTracking() on p.ReactiveEnrollmentId equals e.Id
                join c in _db.ReactiveCourses.AsNoTracking() on e.ReactiveCourseId equals c.Id
                where p.Status == EnrollmentMonthPaymentStatus.Paid
                      && p.PaidAtUtc >= yearStart && p.PaidAtUtc < yearEnd
                      && c.TeacherId != null
                select new
                {
                    TeacherId = c.TeacherId,
                    Month = p.PaidAtUtc.Value.Month,
                    Amount = p.Amount
                }).ToListAsync(cancellationToken);

            foreach (var p in reactivePayments)
            {
                if (!teacherMap.TryGetValue(p.TeacherId!, out var vm)) continue;
                var idx = p.Month - 1;
                if (idx >= 0 && idx < 12)
                {
                    vm.Months[idx] += p.Amount;
                    vm.Total += p.Amount;
                }
            }

            // 3) Private course purchases: group in DB to avoid materializing all rows
            // join PurchaseRequests -> PrivateCourse to get teacherId
            var purchaseGroups = await (
                from pr in _db.PurchaseRequests.AsNoTracking()
                join pc in _db.PrivateCourses.AsNoTracking() on pr.PrivateCourseId equals pc.Id
                where pr.Status == PurchaseStatus.Completed
                      && pr.RequestDateUtc >= yearStart && pr.RequestDateUtc < yearEnd
                      && pc.TeacherId != null
                group pr by new { pc.TeacherId, Month = pr.RequestDateUtc.Month } into g
                select new
                {
                    TeacherId = g.Key.TeacherId!,
                    Month = g.Key.Month,
                    Sum = g.Sum(x => x.Amount)
                }).ToListAsync(cancellationToken);

            foreach (var g in purchaseGroups)
            {
                if (!teacherMap.TryGetValue(g.TeacherId, out var vm)) continue;
                var idx = g.Month - 1;
                if (idx >= 0 && idx < 12)
                {
                    vm.Months[idx] += g.Sum;
                    vm.Total += g.Sum;
                }
            }

            var result = new EarningsIndexVm
            {
                Year = selectedYear,
                Teachers = teacherMap.Values.OrderByDescending(t => t.Total).ToList()
            };

            ViewData["ActivePage"] = "Earnings";
            return View(result);
        }
    }
}

