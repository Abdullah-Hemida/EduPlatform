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
using System.Security.Claims;

namespace Edu.Web.Areas.Teacher.Controllers
{
    [Area("Teacher")]
    [Authorize(Roles = "Teacher")]
    public class ReactiveEnrollmentsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IFileStorageService _fileStorage;

        public ReactiveEnrollmentsController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IStringLocalizer<SharedResource> localizer,
            IFileStorageService fileStorage)
        {
            _db = db;
            _userManager = userManager;
            _localizer = localizer;
            _fileStorage = fileStorage;
        }

        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            var teacherId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (teacherId == null)
                return Unauthorized();

            // Base query: teacher’s own enrollments only
            var query = _db.ReactiveEnrollments
                .AsNoTracking()
                .Where(e => e.ReactiveCourse.TeacherId == teacherId)
                .Include(e => e.Student)
                    .ThenInclude(s => s.User)
                .Include(e => e.ReactiveCourse)
                .Include(e => e.MonthPayments)
                .AsSplitQuery(); // prevent cartesian explosion

            // Count total items for pagination
            var totalCount = await query.CountAsync();

            // Fetch paginated data with projection
            var enrollments = await query
                .OrderByDescending(e => e.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new TeacherEnrollmentViewModel
                {
                    StudentName = e.Student!.User!.FullName,
                    StudentPhone = e.Student!.User!.PhoneNumber,
                    CourseTitle = e.ReactiveCourse!.Title!,
                    CreatedAtUtc = e.CreatedAtUtc,
                    PaymentStatus = e.MonthPayments.All(m => m.Status == EnrollmentMonthPaymentStatus.Paid)
                        ? "Paid"
                        : (e.MonthPayments.Any(m => m.Status == EnrollmentMonthPaymentStatus.Paid)
                            ? "Partial"
                            : "Pending"),
                    PaidMonths = e.MonthPayments.Count(m => m.Status == EnrollmentMonthPaymentStatus.Paid),
                    TotalMonths = e.MonthPayments.Count()
                })
                .ToListAsync();

            // Build pagination data
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;
            ViewData["ActivePage"] = "ReactiveEnrollments";
            return View(enrollments);
        }

        // GET: Teacher/ReactiveEnrollments/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // teacher check
            var teacher = await _db.Teachers.AsNoTracking().FirstOrDefaultAsync(t => t.Id == user.Id);
            if (teacher == null) return Forbid();

            var enrollment = await _db.ReactiveEnrollments
                .AsNoTracking()
                .Include(e => e.ReactiveCourse)
                .Include(e => e.Student).ThenInclude(s => s.User)
                .Include(e => e.MonthPayments).ThenInclude(mp => mp.ReactiveCourseMonth)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (enrollment == null) return NotFound();

            // ensure this enrollment belongs to teacher's course
            if (enrollment.ReactiveCourse?.TeacherId != user.Id) return Forbid();

            // resolve cover url for the course (if any)
            string? coverUrl = null;
            var key = enrollment.ReactiveCourse?.CoverImageKey;
            if (!string.IsNullOrEmpty(key))
            {
                try
                {
                    coverUrl = await _fileStorage.GetPublicUrlAsync(key);
                }
                catch
                {
                    coverUrl = null;
                }
            }

            var payments = enrollment.MonthPayments
                .OrderBy(mp => mp.ReactiveCourseMonth?.MonthIndex)
                .Select(mp => new MonthPaymentVm
                {
                    Id = mp.Id,
                    ReactiveCourseMonthId = mp.ReactiveCourseMonthId,
                    MonthIndex = mp.ReactiveCourseMonth?.MonthIndex ?? 0,
                    Amount = mp.Amount,
                    AmountLabel = mp.Amount.ToEuro(),
                    Status = mp.Status,
                    AdminNote = mp.AdminNote,
                    PaymentReference = mp.PaymentReference,
                    CreatedAtUtc = mp.CreatedAtUtc,
                    PaidAtUtc = mp.PaidAtUtc
                }).ToList();

            var vm = new ReactiveEnrollmentDetailsVm
            {
                Id = enrollment.Id,
                ReactiveCourseId = enrollment.ReactiveCourseId,
                CourseTitle = enrollment.ReactiveCourse?.Title,
                CourseCoverUrl = coverUrl,
                StudentId = enrollment.StudentId,
                StudentName = enrollment.Student?.User?.FullName ?? enrollment.Student?.User?.UserName,
                StudentEmail = enrollment.Student?.User?.Email,
                CreatedAtUtc = enrollment.CreatedAtUtc,
                IsApproved = enrollment.IsApproved,
                IsPaid = enrollment.IsPaid,
                MonthPayments = payments,
                TotalPaid = payments.Where(p => p.Status == EnrollmentMonthPaymentStatus.Paid).Sum(p => p.Amount),
            };
            vm.TotalPaidLabel = vm.TotalPaid.ToEuro();

            ViewData["ActivePage"] = "ReactiveEnrollments";
            return View(vm);
        }
    }
}


