using Edu.Infrastructure.Data;
using Edu.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Edu.Web.Areas.Student.ViewModels;
using Edu.Application.IServices;
using Edu.Web.Resources;
using Edu.Infrastructure.Services;

namespace Edu.Web.Areas.Student.Controllers
{
    [Area("Student")]
    [Authorize(Roles = "Student")]
    public class ReactiveEnrollmentsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IFileStorageService _fileStorage;
        private readonly IEmailSender _emailSender;

        public ReactiveEnrollmentsController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IStringLocalizer<SharedResource> localizer,
            IFileStorageService fileStorage,
            IEmailSender emailSender)
        {
            _db = db;
            _userManager = userManager;
            _localizer = localizer;
            _fileStorage = fileStorage;
            _emailSender = emailSender;
        }

        // POST: Student/ReactiveEnrollments/RequestEnrollment
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestEnrollment(int courseId)
        {
            var studentId = _userManager.GetUserId(User);
            var course = await _db.ReactiveCourses.FindAsync(courseId);
            if (course == null) return NotFound();

            var existing = await _db.ReactiveEnrollments.FirstOrDefaultAsync(e => e.ReactiveCourseId == courseId && e.StudentId == studentId);
            if (existing != null)
            {
                TempData["Info"] = _localizer["ReactiveCourse.AlreadyEnrolled"];
                return RedirectToAction("Details", "ReactiveCourses", new { area = "Student", id = courseId });
            }

            var enrollment = new ReactiveEnrollment
            {
                ReactiveCourseId = courseId,
                StudentId = studentId,
                CreatedAtUtc = DateTime.UtcNow,
                IsApproved = false // Admin approves enrollment if you want; keep false or true per policy
            };

            _db.ReactiveEnrollments.Add(enrollment);
            await _db.SaveChangesAsync();

            // log
            _db.ReactiveEnrollmentLogs.Add(new ReactiveEnrollmentLog
            {
                ReactiveCourseId = courseId,
                EnrollmentId = enrollment.Id,
                ActorId = studentId,
                ActorName = User.Identity?.Name,
                Action = "RequestedEnrollment",
                Note = null,
                CreatedAtUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            // notify admin/teacher
            try
            {
                var teacher = await _db.Users.FirstOrDefaultAsync(u => u.Id == course.TeacherId);
                var admins = await _db.Users.Where(u => _db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == "Admin")).ToListAsync(); // lighter: notify teacher only
                if (teacher != null && !string.IsNullOrEmpty(teacher.Email))
                {
                    await _emailSender.SendEmailAsync(teacher.Email,
                        _localizer["Email.Enrollment.Requested.Subject"],
                        string.Format(_localizer["Email.Enrollment.Requested.Body"], course.Title, User.Identity?.Name ?? studentId));
                }
            }
            catch { }

            TempData["Success"] = _localizer["ReactiveCourse.EnrollmentRequested"];
            return RedirectToAction("Details", "ReactiveCourses", new { area = "Student", id = courseId });
        }

        // POST: Student/ReactiveEnrollments/RequestMonthPayment
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestMonthPayment(int courseId, int monthId)
        {
            var studentId = _userManager.GetUserId(User);
            var course = await _db.ReactiveCourses.Include(c => c.Months).FirstOrDefaultAsync(c => c.Id == courseId);
            if (course == null) return NotFound();

            var month = await _db.ReactiveCourseMonths.FirstOrDefaultAsync(m => m.Id == monthId && m.ReactiveCourseId == courseId);
            if (month == null) return NotFound();

            if (!month.IsReadyForPayment)
            {
                TempData["Error"] = _localizer["ReactiveCourse.MonthNotReady"];
                return RedirectToAction("Details", "ReactiveCourses", new { area = "Student", id = courseId });
            }

            // create enrollment if missing
            var enrollment = await _db.ReactiveEnrollments.FirstOrDefaultAsync(e => e.ReactiveCourseId == courseId && e.StudentId == studentId);
            if (enrollment == null)
            {
                enrollment = new ReactiveEnrollment
                {
                    ReactiveCourseId = courseId,
                    StudentId = studentId,
                    CreatedAtUtc = DateTime.UtcNow,
                    IsApproved = false
                };
                _db.ReactiveEnrollments.Add(enrollment);
                await _db.SaveChangesAsync();
            }

            // check duplicate request
            var exists = await _db.ReactiveEnrollmentMonthPayments
                .AnyAsync(p => p.ReactiveEnrollmentId == enrollment.Id && p.ReactiveCourseMonthId == monthId && p.Status == EnrollmentMonthPaymentStatus.Pending);

            if (exists)
            {
                TempData["Info"] = _localizer["ReactiveCourse.PaymentAlreadyRequested"];
                return RedirectToAction("Details", "ReactiveCourses", new { area = "Student", id = courseId });
            }

            var payment = new ReactiveEnrollmentMonthPayment
            {
                ReactiveEnrollmentId = enrollment.Id,
                ReactiveCourseMonthId = month.Id,
                Amount = course.PricePerMonth,
                Status = EnrollmentMonthPaymentStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow
            };

            _db.ReactiveEnrollmentMonthPayments.Add(payment);
            await _db.SaveChangesAsync();

            // log
            _db.ReactiveEnrollmentLogs.Add(new ReactiveEnrollmentLog
            {
                ReactiveCourseId = courseId,
                EnrollmentId = enrollment.Id,
                ActorId = studentId,
                ActorName = User.Identity?.Name,
                Action = "RequestedMonthPayment",
                Note = $"MonthIndex={month.MonthIndex}; Amount={payment.Amount}",
                CreatedAtUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            // notify admin & teacher
            try
            {
                var teacher = await _db.Users.FirstOrDefaultAsync(u => u.Id == course.TeacherId);
                var adminEmails = await _db.Users
                    .Where(u => _db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == "Admin"))
                    .Select(u => u.Email)
                    .ToListAsync();

                var body = string.Format(_localizer["Email.Payment.Requested.Body"], course.Title, month.MonthIndex, User.Identity?.Name ?? studentId);
                var subject = _localizer["Email.Payment.Requested.Subject"];

                if (teacher != null && !string.IsNullOrEmpty(teacher.Email))
                    await _emailSender.SendEmailAsync(teacher.Email, subject, body);

                foreach (var e in adminEmails.Where(e => !string.IsNullOrEmpty(e)))
                    await _emailSender.SendEmailAsync(e, subject, body);
            }
            catch { }

            TempData["Success"] = _localizer["ReactiveCourse.PaymentRequested"];
            return RedirectToAction("Details", "ReactiveCourses", new { area = "Student", id = courseId });
        }

        // GET: Student/ReactiveEnrollments/MyEnrollments
        public async Task<IActionResult> MyEnrollments()
        {
            var studentId = _userManager.GetUserId(User);
            var enrollments = await _db.ReactiveEnrollments
                .Where(e => e.StudentId == studentId)
                .Include(e => e.ReactiveCourse).ThenInclude(c => c.Months)
                .Include(e => e.MonthPayments).ThenInclude(mp => mp.ReactiveCourseMonth)
                .ToListAsync();

            var vm = enrollments.Select(e => new MyEnrollmentVm
            {
                EnrollmentId = e.Id,
                CourseId = e.ReactiveCourseId,
                CourseTitle = e.ReactiveCourse!.Title,
                CoverPublicUrl = string.IsNullOrEmpty(e.ReactiveCourse.CoverImageKey) ? null : _fileStorage.GetPublicUrlAsync(e.ReactiveCourse.CoverImageKey).Result,
                Months = e.ReactiveCourse!.Months.OrderBy(m => m.MonthIndex).Select(m => new MyEnrollmentMonthVm
                {
                    MonthId = m.Id,
                    MonthIndex = m.MonthIndex,
                    IsReadyForPayment = m.IsReadyForPayment,
                    PaymentStatus = e.MonthPayments.FirstOrDefault(mp => mp.ReactiveCourseMonthId == m.Id)?.Status
                }).ToList()
            }).ToList();
            ViewData["ActivePage"] = "MyEnrollments";
            return View(vm);
        }

        // GET: Student/ReactiveEnrollments/ViewMonth/5
        public async Task<IActionResult> ViewMonth(int monthId)
        {
            var studentId = _userManager.GetUserId(User);
            var month = await _db.ReactiveCourseMonths.Include(m => m.ReactiveCourse).FirstOrDefaultAsync(m => m.Id == monthId);
            if (month == null) return NotFound();

            var enrollment = await _db.ReactiveEnrollments
                .Include(e => e.MonthPayments)
                .FirstOrDefaultAsync(e => e.ReactiveCourseId == month.ReactiveCourseId && e.StudentId == studentId);

            if (enrollment == null)
            {
                TempData["Error"] = _localizer["ReactiveCourse.NotEnrolled"];
                return RedirectToAction("Details", "ReactiveCourses", new { area = "Student", id = month.ReactiveCourseId });
            }

            var payment = enrollment.MonthPayments.FirstOrDefault(mp => mp.ReactiveCourseMonthId == monthId);
            if (payment == null || payment.Status != EnrollmentMonthPaymentStatus.Paid)
            {
                TempData["Error"] = _localizer["ReactiveCourse.MonthNotPaid"];
                return RedirectToAction("Details", "ReactiveCourses", new { area = "Student", id = month.ReactiveCourseId });
            }

            // paid => show lessons & meet urls
            var lessons = await _db.ReactiveCourseLessons.Where(l => l.ReactiveCourseMonthId == monthId).OrderBy(l => l.ScheduledUtc).ToListAsync();
            var lessonVms = lessons.Select(l => new StudentCourseLessonVm
            {
                Id = l.Id,
                Title = l.Title,
                ScheduledUtc = l.ScheduledUtc,
                MeetUrl = l.MeetUrl,
                Notes = l.Notes
            }).ToList();

            var vm = new ViewMonthVm
            {
                CourseId = month.ReactiveCourseId,
                CourseTitle = month.ReactiveCourse!.Title,
                MonthIndex = month.MonthIndex,
                Lessons = lessonVms
            };
            ViewData["ActivePage"] = "MyEnrollments";
            return View(vm);
        }
    }
}

