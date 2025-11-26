using Edu.Infrastructure.Data; // adjust
using Edu.Domain.Entities; // adjust
using Edu.Web.Areas.Admin.ViewModels; // adjust
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Edu.Infrastructure.Services;
using Edu.Infrastructure.Helpers;

namespace Edu.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ReactiveEnrollmentsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ReactiveEnrollmentsController> _logger;
        private readonly IEmailSender _emailSender;
        private readonly IStringLocalizer<ReactiveEnrollmentsController> _localizer;

        public ReactiveEnrollmentsController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ILogger<ReactiveEnrollmentsController> logger,
            IEmailSender emailSender,
            IStringLocalizer<ReactiveEnrollmentsController> localizer)
        {
            _db = db;
            _userManager = userManager;
            _logger = logger;
            _emailSender = emailSender;
            _localizer = localizer;
        }

        // GET: Admin/ReactiveEnrollments
        // supports ?q=search&page=1&pageSize=20
        [HttpGet]
        public async Task<IActionResult> Index(string? q, int page = 1, int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize <= 0) pageSize = 20;

            // base query joining ReactiveEnrollment -> ReactiveCourse -> teacher (ApplicationUser)
            var baseQuery = _db.ReactiveEnrollments
                .AsNoTracking()
                .Include(e => e.ReactiveCourse)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                // search across course title, student full name/email, teacher name
                baseQuery = baseQuery.Where(e =>
                    e.ReactiveCourse.Title.Contains(q) ||
                    e.StudentId != null && _db.Users.Any(u => u.Id == e.StudentId && (u.FullName.Contains(q) || u.Email.Contains(q))) ||
                    e.ReactiveCourse.TeacherId != null && _db.Users.Any(t => t.Id == e.ReactiveCourse.TeacherId && t.FullName.Contains(q))
                );
            }

            var totalCount = await baseQuery.CountAsync();

            var items = await baseQuery
                .OrderByDescending(e => e.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new
                {
                    e.Id,
                    e.CreatedAtUtc,
                    CourseId = e.ReactiveCourse.Id,
                    CourseTitle = e.ReactiveCourse.Title,
                    TeacherId = e.ReactiveCourse.TeacherId,
                    StudentId = e.StudentId,
                    PaidCount = e.MonthPayments.Count(m => m.Status == EnrollmentMonthPaymentStatus.Paid),
                    PendingCount = e.MonthPayments.Count(m => m.Status == EnrollmentMonthPaymentStatus.Pending)
                })
                .ToListAsync();

            // gather user details for students & teachers in one go
            var studentIds = items.Select(i => i.StudentId).Where(id => id != null).Distinct().ToList();
            var teacherIds = items.Select(i => i.TeacherId).Where(id => id != null).Distinct().ToList();

            var users = await _db.Users
                .AsNoTracking()
                .Where(u => (studentIds.Contains(u.Id)) || (teacherIds.Contains(u.Id)))
                .Select(u => new { u.Id, u.FullName, u.Email, u.PhoneNumber, u.PhotoUrl })
                .ToListAsync();

            var vm = new AdminEnrollmentIndexVm
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                Query = q ?? string.Empty,
                Items = items.Select(i =>
                {
                    var stu = users.FirstOrDefault(u => u.Id == i.StudentId);
                    var tch = users.FirstOrDefault(u => u.Id == i.TeacherId);
                    return new AdminEnrollmentListItemVm
                    {
                        EnrollmentId = i.Id,
                        CourseId = i.CourseId,
                        CourseTitle = i.CourseTitle,
                        StudentId = i.StudentId,
                        StudentFullName = stu?.FullName ?? i.StudentId,
                        StudentEmail = stu?.Email,
                        StudentPhone = stu?.PhoneNumber,
                        StudentPhotoUrl = stu?.PhotoUrl,
                        TeacherFullName = tch?.FullName ?? null,
                        CreatedAtUtc = i.CreatedAtUtc,
                        PaidMonthsCount = i.PaidCount,
                        PendingMonthsCount = i.PendingCount
                    };
                }).ToList()
            };
            ViewData["ActivePage"] = "ReactiveEnrollments";
            return View(vm);
        }

        // GET: Admin/ReactiveEnrollments/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var enrollment = await _db.ReactiveEnrollments
                .AsNoTracking()
                .Include(e => e.ReactiveCourse)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (enrollment == null) return NotFound();

            // load payments for this enrollment
            var payments = await _db.ReactiveEnrollmentMonthPayments
                .AsNoTracking()
                .Where(p => p.ReactiveEnrollmentId == id)
                .ToListAsync();

            // load months of course
            var months = await _db.ReactiveCourseMonths
                .AsNoTracking()
                .Where(m => m.ReactiveCourseId == enrollment.ReactiveCourseId)
                .OrderBy(m => m.MonthIndex)
                .ToListAsync();

            // load student and teacher full user rows
            var studentUser = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == enrollment.StudentId);
            ApplicationUser? teacherUser = null;
            if (!string.IsNullOrEmpty(enrollment.ReactiveCourse.TeacherId))
            {
                teacherUser = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == enrollment.ReactiveCourse.TeacherId);
            }

            var monthsVm = months.Select(m => new AdminEnrollmentMonthVm
            {
                MonthId = m.Id,
                MonthIndex = m.MonthIndex,
                LessonsCount = m.Lessons.Count,
                Payments = payments.Where(p => p.ReactiveCourseMonthId == m.Id)
                                   .OrderByDescending(p => p.CreatedAtUtc)
                                   .Select(p => new AdminEnrollmentPaymentVm
                                   {
                                       PaymentId = p.Id,
                                       MonthId = p.ReactiveCourseMonthId,
                                       AmountLabel = p.Amount.ToEuro(),
                                       Status = p.Status,
                                       CreatedAtUtc = p.CreatedAtUtc,
                                       PaidAtUtc = p.PaidAtUtc,
                                       AdminNote = p.AdminNote,
                                       StudentId = enrollment.StudentId
                                   }).ToList()
            }).ToList();

            var vm = new AdminEnrollmentDetailsVm
            {
                EnrollmentId = enrollment.Id,
                CourseId = enrollment.ReactiveCourseId,
                CourseTitle = enrollment.ReactiveCourse.Title,
                StudentId = enrollment.StudentId,
                StudentName = studentUser?.FullName ?? enrollment.StudentId,
                StudentEmail = studentUser?.Email,
                StudentPhone = studentUser?.PhoneNumber,
                StudentPhotoUrl = studentUser?.PhotoUrl,
                TeacherFullName = teacherUser?.FullName,
                CreatedAtUtc = enrollment.CreatedAtUtc,
                Months = monthsVm
            };
            ViewData["ActivePage"] = "ReactiveEnrollments";
            return View(vm);
        }

        // POST: Admin/ReactiveEnrollments/MarkPaymentPaid
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPaymentPaid([FromForm] int paymentId)
        {
            var p = await _db.ReactiveEnrollmentMonthPayments
                .Include(x => x.ReactiveEnrollment)
                .FirstOrDefaultAsync(x => x.Id == paymentId);

            if (p == null) return Json(new { success = false, message = _localizer["Admin.PaymentNotFound"].Value });

            if (p.Status == EnrollmentMonthPaymentStatus.Paid)
                return Json(new { success = false, message = _localizer["Admin.PaymentAlreadyPaid"].Value });

            p.Status = EnrollmentMonthPaymentStatus.Paid;
            p.PaidAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // log
            _db.ReactiveEnrollmentLogs.Add(new ReactiveEnrollmentLog
            {
                ReactiveCourseId = p.ReactiveEnrollment.ReactiveCourseId,
                EnrollmentId = p.ReactiveEnrollmentId,
                ActorId = User?.Identity?.Name,
                ActorName = User?.Identity?.Name,
                Action = "AdminMarkedPaymentPaid",
                Note = $"PaymentId={paymentId}",
                CreatedAtUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            // notify student (best-effort)
            try
            {
                var studentId = p.ReactiveEnrollment.StudentId;
                if (!string.IsNullOrEmpty(studentId))
                {
                    var subj = _localizer["Notify.PaymentMarkedPaid.Subject"].Value;
                    var body = string.Format(_localizer["Notify.PaymentMarkedPaid.Body"].Value, p.Amount.ToString("C"), p.ReactiveCourseMonthId);
                    var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == studentId);
                    if (user?.Email != null)
                    {
                        await _emailSender.SendEmailAsync(user.Email, subj, body);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Notify after MarkPaymentPaid failed for payment {PaymentId}", paymentId);
            }

            return Json(new { success = true, paymentId = paymentId, paidAt = p.PaidAtUtc });
        }

        // POST: Admin/ReactiveEnrollments/RejectPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectPayment([FromForm] int paymentId, [FromForm] string? adminNote)
        {
            var p = await _db.ReactiveEnrollmentMonthPayments
                .Include(x => x.ReactiveEnrollment)
                .FirstOrDefaultAsync(x => x.Id == paymentId);

            if (p == null) return Json(new { success = false, message = _localizer["Admin.PaymentNotFound"].Value });

            if (p.Status == EnrollmentMonthPaymentStatus.Paid)
                return Json(new { success = false, message = _localizer["Admin.CannotRejectPaid"].Value });

            p.Status = EnrollmentMonthPaymentStatus.Rejected;
            p.AdminNote = adminNote;
            await _db.SaveChangesAsync();

            // log
            _db.ReactiveEnrollmentLogs.Add(new ReactiveEnrollmentLog
            {
                ReactiveCourseId = p.ReactiveEnrollment.ReactiveCourseId,
                EnrollmentId = p.ReactiveEnrollmentId,
                ActorId = User?.Identity?.Name,
                ActorName = User?.Identity?.Name,
                Action = "AdminRejectedPayment",
                Note = $"PaymentId={paymentId}; Note={adminNote}",
                CreatedAtUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            // notify student (best-effort)
            try
            {
                var studentId = p.ReactiveEnrollment.StudentId;
                if (!string.IsNullOrEmpty(studentId))
                {
                    var subj = _localizer["Notify.PaymentRejected.Subject"].Value;
                    var body = string.Format(_localizer["Notify.PaymentRejected.Body"].Value, p.Amount.ToString("C"), adminNote ?? string.Empty);
                    var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == studentId);
                    if (user?.Email != null)
                    {
                        await _emailSender.SendEmailAsync(user.Email, subj, body);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Notify after RejectPayment failed for payment {PaymentId}", paymentId);
            }

            return Json(new { success = true, paymentId = paymentId });
        }

        // POST: Admin/ReactiveEnrollments/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete([FromForm] int enrollmentId)
        {
            var enr = await _db.ReactiveEnrollments
                .Include(e => e.MonthPayments)
                .FirstOrDefaultAsync(e => e.Id == enrollmentId);

            if (enr == null) return Json(new { success = false, message = _localizer["Admin.NotFound"].Value });

            try
            {
                // optional: notify student that admin deleted enrollment
                try
                {
                    var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == enr.StudentId);
                    if (user != null)
                    {
                        var subj = _localizer["Notify.EnrollmentDeleted.Subject"].Value;
                        var body = string.Format(_localizer["Notify.EnrollmentDeleted.Body"].Value, enr.ReactiveCourse?.Title ?? "");
                        if (!string.IsNullOrEmpty(user.Email))
                            await _emailSender.SendEmailAsync(user.Email, subj, body);
                    }
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Notification when deleting enrollment failed"); }

                _db.ReactiveEnrollments.Remove(enr);
                await _db.SaveChangesAsync();
                return Json(new { success = true, message = _localizer["Admin.DeleteSuccess"].Value });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delete enrollment {EnrollmentId} failed", enrollmentId);
                return StatusCode(500, new { success = false, message = _localizer["Admin.DeleteError"].Value });
            }
        }

        // POST: Admin/ReactiveEnrollments/DeleteOld
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOld([FromForm] DateTime olderThanUtc)
        {
            try
            {
                var toDelete = await _db.ReactiveEnrollments
                    .Where(e => e.CreatedAtUtc < olderThanUtc)
                    .Where(e => !e.MonthPayments.Any(p => p.Status == EnrollmentMonthPaymentStatus.Paid))
                    .ToListAsync();

                if (!toDelete.Any())
                    return Json(new { success = true, deleted = 0, message = _localizer["Admin.DeleteOldNoMatching"].Value });

                var count = toDelete.Count;
                _db.ReactiveEnrollments.RemoveRange(toDelete);
                await _db.SaveChangesAsync();

                return Json(new { success = true, deleted = count, message = _localizer["Admin.DeleteOldSuccess"].Value });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteOld failed olderThan={OlderThan}", olderThanUtc);
                return StatusCode(500, new { success = false, message = _localizer["Admin.DeleteError"].Value });
            }
        }
    }
}




