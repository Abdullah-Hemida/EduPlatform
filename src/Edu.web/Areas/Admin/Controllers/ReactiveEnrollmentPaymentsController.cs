using Edu.Infrastructure.Data;
using Edu.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Edu.Web.Areas.Admin.ViewModels;
using Edu.Application.IServices;
using Edu.Web.Resources;
using Edu.Infrastructure.Services;
using Edu.Infrastructure.Helpers;

namespace Edu.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ReactiveEnrollmentPaymentsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fileStorage;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IEmailSender _emailSender; // optional, implement or stub

        public ReactiveEnrollmentPaymentsController(
            ApplicationDbContext db,
            IFileStorageService fileStorage,
            UserManager<ApplicationUser> userManager,
            IStringLocalizer<SharedResource> localizer,
            IEmailSender emailSender)
        {
            _db = db;
            _fileStorage = fileStorage;
            _userManager = userManager;
            _localizer = localizer;
            _emailSender = emailSender;
        }

        // GET: Admin/ReactiveEnrollmentPayments/Pending
        public async Task<IActionResult> Pending(int page = 1, int pageSize = 20)
        {
            var query = _db.ReactiveEnrollmentMonthPayments
                .AsNoTracking()
                .Where(p => p.Status == EnrollmentMonthPaymentStatus.Pending)
                .Include(p => p.ReactiveEnrollment).ThenInclude(e => e.Student).ThenInclude(s => s.User)
                .Include(p => p.ReactiveCourseMonth).ThenInclude(m => m.ReactiveCourse)
                .OrderBy(p => p.CreatedAtUtc);

            var total = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var vm = new PendingPaymentsVm
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = total,
                Payments = new List<PendingPaymentItemVm>()
            };

            foreach (var p in items)
            {
                var course = p.ReactiveCourseMonth!.ReactiveCourse!;
                string? cover = null;
                if (!string.IsNullOrEmpty(course.CoverImageKey))
                    cover = await _fileStorage.GetPublicUrlAsync(course.CoverImageKey);

                vm.Payments.Add(new PendingPaymentItemVm
                {
                    Id = p.Id,
                    CourseId = course.Id,
                    CourseTitle = course.Title,
                    CourseCoverPublicUrl = cover,
                    MonthIndex = p.ReactiveCourseMonth!.MonthIndex,
                    StudentId = p.ReactiveEnrollment!.StudentId,
                    StudentDisplayName = p.ReactiveEnrollment.Student?.User?.FullName ?? p.ReactiveEnrollment.StudentId,
                    AmountLabel = p.Amount.ToEuro(),
                    CreatedAtUtc = p.CreatedAtUtc
                });
            }
            ViewData["ActivePage"] = "ReactiveEnrollments";
            return View(vm);
        }

        // GET: Admin/ReactiveEnrollmentPayments/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var p = await _db.ReactiveEnrollmentMonthPayments
                .Include(x => x.ReactiveEnrollment).ThenInclude(e => e.Student).ThenInclude(s => s.User)
                .Include(x => x.ReactiveCourseMonth).ThenInclude(m => m.ReactiveCourse)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (p == null) return NotFound();

            var course = p.ReactiveCourseMonth!.ReactiveCourse!;
            var cover = string.IsNullOrEmpty(course.CoverImageKey) ? null : await _fileStorage.GetPublicUrlAsync(course.CoverImageKey);

            var vm = new PaymentDetailsVm
            {
                Id = p.Id,
                CourseId = course.Id,
                CourseTitle = course.Title,
                CourseCoverPublicUrl = cover,
                MonthIndex = p.ReactiveCourseMonth.MonthIndex,
                StudentId = p.ReactiveEnrollment!.StudentId,
                StudentDisplayName = p.ReactiveEnrollment.Student?.User?.FullName ?? p.ReactiveEnrollment.StudentId,
                AmountLabel = p.Amount.ToEuro(),
                Status = p.Status,
                CreatedAtUtc = p.CreatedAtUtc,
                Notes = p.AdminNote
            };
            ViewData["ActivePage"] = "ReactiveEnrollments";
            return View(vm);
        }

        // POST: Admin/ReactiveEnrollmentPayments/MarkPaid
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPaid(MarkPaidInputModel input)
        {
            if (!ModelState.IsValid) return RedirectToAction(nameof(Details), new { id = input.PaymentId });

            var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var adminName = User.Identity?.Name;

            using var tx = await _db.Database.BeginTransactionAsync();
            var payment = await _db.ReactiveEnrollmentMonthPayments
                .Include(p => p.ReactiveEnrollment).ThenInclude(e => e.ReactiveCourse)
                .Include(p => p.ReactiveCourseMonth).ThenInclude(m => m.ReactiveCourse)
                .FirstOrDefaultAsync(p => p.Id == input.PaymentId);

            if (payment == null)
            {
                TempData["Error"] = _localizer["Admin.NotFound"].Value;
                return RedirectToAction(nameof(Pending));
            }

            if (payment.Status != EnrollmentMonthPaymentStatus.Pending)
            {
                TempData["Error"] = _localizer["ReactiveCourse.PaymentNotPending"].Value;
                return RedirectToAction(nameof(Details), new { id = input.PaymentId });
            }

            // capacity check (distinct paid enrollments for that course)
            var courseId = payment.ReactiveCourseMonth!.ReactiveCourseId;
            var paidCount = await _db.ReactiveEnrollmentMonthPayments
                .Where(x => x.ReactiveCourseMonth!.ReactiveCourseId == courseId && x.Status == EnrollmentMonthPaymentStatus.Paid)
                .Select(x => x.ReactiveEnrollmentId).Distinct().CountAsync();

            var course = payment.ReactiveCourseMonth.ReactiveCourse!;
            if (course.Capacity > 0 && paidCount >= course.Capacity)
            {
                TempData["Error"] = _localizer["ReactiveCourse.CapacityReached"];
                return RedirectToAction(nameof(Details), new { id = input.PaymentId });
            }

            payment.Status = EnrollmentMonthPaymentStatus.Paid;
            payment.PaidAtUtc = DateTime.UtcNow;
            payment.PaymentReference = input.PaymentReference;
            payment.Amount = input.Amount; // accept admin-entered amount (or keep original)

            _db.ReactiveEnrollmentMonthPayments.Update(payment);

            // add a log
            _db.ReactiveEnrollmentLogs.Add(new ReactiveEnrollmentLog
            {
                ReactiveCourseId = payment.ReactiveCourseMonth.ReactiveCourseId,
                EnrollmentId = payment.ReactiveEnrollmentId,
                ActorId = adminId,
                ActorName = adminName,
                Action = "MonthPaid",
                Note = $"MonthIndex={payment.ReactiveCourseMonth.MonthIndex}; Amount={payment.Amount}; Ref={payment.PaymentReference}",
                CreatedAtUtc = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            // Optional: email notify student & teacher (non-blocking)
            try
            {
                var studentEmail = payment.ReactiveEnrollment!.Student?.User?.Email;
                var teacher = await _userManager.FindByIdAsync(course.TeacherId);
                if (!string.IsNullOrEmpty(studentEmail))
                {
                    await _emailSender.SendEmailAsync(studentEmail, _localizer["Email.Payment.Confirmed.Subject"], string.Format(_localizer["Email.Payment.Confirmed.Body"], course.Title, payment.ReactiveCourseMonth.MonthIndex));
                }
                if (teacher != null)
                {
                    await _emailSender.SendEmailAsync(teacher.Email, _localizer["Email.Payment.Confirmed.Subject"], string.Format(_localizer["Email.Payment.Confirmed.Body.Teacher"], course.Title, payment.ReactiveEnrollment!.Student?.User?.FullName ?? payment.ReactiveEnrollment.StudentId));
                }
            }
            catch
            {
                // swallow: don't fail admin action if email fails
            }

            TempData["Success"] = _localizer["ReactiveCourse.PaymentMarkedPaid"];
            return RedirectToAction(nameof(Pending));
        }

        // POST: Admin/ReactiveEnrollmentPayments/Reject
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(RejectPaymentInputModel input)
        {
            if (!ModelState.IsValid) return RedirectToAction(nameof(Details), new { id = input.PaymentId });

            var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var adminName = User.Identity?.Name;

            var payment = await _db.ReactiveEnrollmentMonthPayments.Include(p => p.ReactiveCourseMonth).FirstOrDefaultAsync(p => p.Id == input.PaymentId);
            if (payment == null) return NotFound();

            if (payment.Status != EnrollmentMonthPaymentStatus.Pending)
            {
                TempData["Error"] = _localizer["ReactiveCourse.PaymentNotPending"].Value;
                return RedirectToAction(nameof(Details), new { id = input.PaymentId });
            }

            payment.Status = EnrollmentMonthPaymentStatus.Rejected;
            payment.AdminNote = input.Reason;
            _db.ReactiveEnrollmentMonthPayments.Update(payment);

            _db.ReactiveEnrollmentLogs.Add(new ReactiveEnrollmentLog
            {
                ReactiveCourseId = payment.ReactiveCourseMonth!.ReactiveCourseId,
                EnrollmentId = payment.ReactiveEnrollmentId,
                ActorId = adminId,
                ActorName = adminName,
                Action = "MonthPaymentRejected",
                Note = input.Reason,
                CreatedAtUtc = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            // notify student (best effort)
            try
            {
                var studentEmail = payment.ReactiveEnrollment!.Student?.User?.Email;
                if (!string.IsNullOrEmpty(studentEmail))
                {
                    await _emailSender.SendEmailAsync(studentEmail, _localizer["Email.Payment.Rejected.Subject"], string.Format(_localizer["Email.Payment.Rejected.Body"], input.Reason));
                }
            }
            catch { }

            TempData["Success"] = _localizer["ReactiveCourse.PaymentRejected"].Value;
            return RedirectToAction(nameof(Pending));
        }
    }
}


