using System.Globalization;
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
        public async Task<IActionResult> Index(int? year)
        {
            var selectedYear = year ?? DateTime.UtcNow.Year;

            // fetch teachers and user profile
            var teachers = await _db.Teachers
                                    .AsNoTracking()
                                    .Include(t => t.User)
                                    .OrderBy(t => t.User.FullName)
                                    .ToListAsync();

            // prepare dictionary to accumulate sums per teacherId => month (1..12)
            var teacherMap = teachers.ToDictionary(t => t.Id, t => new TeacherEarningsVm
            {
                TeacherId = t.Id,
                FullName = t.User?.FullName ?? t.User?.UserName ?? "",
                PhotoUrl = t.User?.PhotoUrl,
                PhoneNumber = t.User?.PhoneNumber,
                Months = new decimal[12], // index 0 => Jan
                Total = 0m
            });

            // 1) Bookings: Paid bookings with Price not null. Group by TeacherId and month (RequestedDateUtc).
            // Bookings: use PaidAtUtc for month/year grouping
            var bookingGroups = await _db.Bookings
                .AsNoTracking()
                .Where(b => b.Status == BookingStatus.Paid
                            && b.Price != null
                            && b.TeacherId != null
                            && b.PaidAtUtc.HasValue
                            && b.PaidAtUtc.Value.Year == selectedYear)
                .GroupBy(b => new { b.TeacherId, Month = b.PaidAtUtc.Value.Month })
                .Select(g => new { TeacherId = g.Key.TeacherId!, Month = g.Key.Month, Sum = g.Sum(x => x.Price) })
                .ToListAsync();

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

            // 2) Reactive course month payments: payments with Status == Paid and PaidAtUtc in year
            // We'll join payments -> enrollments -> courses to get teacher id
            var reactivePayments = await (
                from p in _db.ReactiveEnrollmentMonthPayments.AsNoTracking()
                join e in _db.ReactiveEnrollments.AsNoTracking() on p.ReactiveEnrollmentId equals e.Id
                join c in _db.ReactiveCourses.AsNoTracking() on e.ReactiveCourseId equals c.Id
                where p.Status == EnrollmentMonthPaymentStatus.Paid
                      && p.PaidAtUtc.HasValue
                      && p.PaidAtUtc.Value.Year == selectedYear
                      && c.TeacherId != null
                select new
                {
                    TeacherId = c.TeacherId,
                    Month = p.PaidAtUtc.Value.Month,
                    Amount = p.Amount
                }).ToListAsync();

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

            // 3) Private course purchases (PurchaseRequests)
            // NOTE: your PurchaseRequest.Amount is string — parse safely. If you can migrate to decimal, do it later.
            var purchaseRequests = await _db.PurchaseRequests
                                           .AsNoTracking()
                                           .Include(pr => pr.PrivateCourse)
                                           .ThenInclude(pc => pc!.Teacher)
                                           .Where(pr => pr.Status == PurchaseStatus.Completed && pr.PrivateCourse != null && pr.RequestDateUtc.Year == selectedYear)
                                           .ToListAsync();

            // purchaseRequests is loaded from DB and now has decimal? Amount
            foreach (var pr in purchaseRequests)
            {
                var teacherId = pr.PrivateCourse?.TeacherId;
                if (string.IsNullOrEmpty(teacherId)) continue;
                if (!teacherMap.TryGetValue(teacherId, out var vm)) continue;

                    var amt = pr.Amount;
                    var month = pr.RequestDateUtc.Month;
                    var idx = month - 1;
                    vm.Months[idx] += amt;
                    vm.Total += amt;
            }


            // prepare final VM
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

