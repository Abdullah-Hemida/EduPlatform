using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Infrastructure.Services;
using Edu.Web.Areas.Student.ViewModels;
using Edu.Web.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Edu.Web.Areas.Student.Controllers
{
    [Area("Student")]
    [Authorize(Roles = "Student")]
    public class ReactiveEnrollmentsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileStorageService _fileStorage;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<ReactiveEnrollmentsController> _logger;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public ReactiveEnrollmentsController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IFileStorageService fileStorage,
            IEmailSender emailSender,
            ILogger<ReactiveEnrollmentsController> logger,
            IStringLocalizer<SharedResource> localizer)
        {
            _db = db;
            _userManager = userManager;
            _fileStorage = fileStorage;
            _emailSender = emailSender;
            _logger = logger;
            _localizer = localizer;
        }

        // GET: /Student/ReactiveCourses/Details/5
        [HttpGet("Student/ReactiveCourses/Details/{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            // 1. Load lightweight course + months (lessons count only) projection
            var course = await _db.ReactiveCourses
                .AsNoTracking()
                .Where(c => c.Id == id && !c.IsArchived)
                .Select(c => new
                {
                    c.Id,
                    c.Title,
                    c.Description,
                    c.CoverImageKey,
                    c.IntroVideoUrl,
                    c.PricePerMonth,
                    c.DurationMonths,
                    c.Capacity,
                    c.StartDate,
                    c.EndDate,
                    Months = c.Months.OrderBy(m => m.MonthIndex)
                        .Select(m => new
                        {
                            m.Id,
                            m.MonthIndex,
                            m.MonthStartUtc,
                            m.MonthEndUtc,
                            m.IsReadyForPayment,
                            LessonsCount = m.Lessons.Count()
                        }).ToList()
                })
                .FirstOrDefaultAsync();

            if (course == null) return NotFound();

            // 2. Resolve cover URL (best-effort)
            string? coverPublicUrl = null;
            if (!string.IsNullOrEmpty(course.CoverImageKey))
            {
                try
                {
                    coverPublicUrl = await _fileStorage.GetPublicUrlAsync(course.CoverImageKey);
                }
                catch
                {
                    coverPublicUrl = course.CoverImageKey;
                }
            }

            // 3. Current student
            var studentId = _userManager.GetUserId(User);

            // Build base months list (no payment state yet)
            var monthsBase = course.Months.Select(m => new StudentCourseMonthVm
            {
                Id = m.Id,
                MonthIndex = m.MonthIndex,
                MonthStartUtc = m.MonthStartUtc,
                MonthEndUtc = m.MonthEndUtc,
                IsReadyForPayment = m.IsReadyForPayment,
                LessonsCount = m.LessonsCount
            }).OrderBy(m => m.MonthIndex).ToList();

            // If not authenticated, return public VM
            if (string.IsNullOrEmpty(studentId))
            {
                var publicVm = new StudentReactiveCourseDetailsVm
                {
                    CourseId = course.Id,
                    Title = course.Title,
                    Description = course.Description,
                    CoverPublicUrl = coverPublicUrl,
                    IntroVideoUrl = course.IntroVideoUrl,
                    PricePerMonth = course.PricePerMonth,
                    PricePerMonthLabel = course.PricePerMonth.ToEuro(),
                    DurationMonths = course.DurationMonths,
                    Capacity = course.Capacity,
                    StartDateUtc = course.StartDate,
                    EndDateUtc = course.EndDate,
                    Months = monthsBase
                };
                publicVm.IntroYouTubeId = YouTubeHelper.ExtractYouTubeId(publicVm.IntroVideoUrl);
                return View(publicVm);
            }

            // 4. Load student's enrollment (if any)
            var enrollment = await _db.ReactiveEnrollments
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.ReactiveCourseId == course.Id && e.StudentId == studentId);

            int? enrollmentId = enrollment?.Id;

            // 5. Load payments for this enrollment (single query)
            var paymentList = new List<ReactiveEnrollmentMonthPayment>();
            if (enrollmentId.HasValue)
            {
                paymentList = await _db.ReactiveEnrollmentMonthPayments
                    .AsNoTracking()
                    .Where(p => p.ReactiveEnrollmentId == enrollmentId.Value)
                    .ToListAsync();
            }

            // 6. Build months VMs with per-student payment state + permissions
            const int allowRequestDayOfMonth = 15; // example gating fallback
            var utcNow = DateTime.UtcNow;
            var dayOfMonth = utcNow.Day;

            foreach (var m in monthsBase)
            {
                var payment = paymentList.FirstOrDefault(p => p.ReactiveCourseMonthId == m.Id);
                m.PaymentId = payment?.Id;
                m.MyPaymentStatus = payment?.Status;

                // Determine CanRequestPayment:
                bool canRequest = m.IsReadyForPayment && (payment == null);

                // optional: enforce midpoint gating if MonthStart/End provided
                if (m.MonthStartUtc != default && m.MonthEndUtc != default)
                {
                    var midpoint = m.MonthStartUtc + TimeSpan.FromTicks((m.MonthEndUtc - m.MonthStartUtc).Ticks / 2);
                    if (DateTime.UtcNow > midpoint) canRequest = false;
                }
                else
                {
                    if (dayOfMonth > allowRequestDayOfMonth) canRequest = false;
                }

                m.CanRequestPayment = canRequest;
                m.CanCancelPayment = (payment != null && payment.Status == EnrollmentMonthPaymentStatus.Pending);
                m.CanViewLessons = (payment != null && payment.Status == EnrollmentMonthPaymentStatus.Paid);
            }

            // 7. If any paid months -> load lessons only for paid months
            var paidMonthIds = monthsBase.Where(m => m.CanViewLessons).Select(m => m.Id).ToList();
            if (paidMonthIds.Any())
            {
                var lessons = await _db.ReactiveCourseLessons
                    .AsNoTracking()
                    .Where(l => paidMonthIds.Contains(l.ReactiveCourseMonthId))
                    .OrderBy(l => l.ScheduledUtc)
                    .Select(l => new StudentCourseLessonVm
                    {
                        Id = l.Id,
                        Title = l.Title,
                        ScheduledUtc = l.ScheduledUtc,
                        MeetUrl = l.MeetUrl,
                        Notes = l.Notes,
                        ReactiveCourseMonthId = l.ReactiveCourseMonthId
                    })
                    .ToListAsync();

                foreach (var monthVm in monthsBase.Where(m => paidMonthIds.Contains(m.Id)))
                {
                    monthVm.Lessons = lessons
                        .Where(x => x.ReactiveCourseMonthId == monthVm.Id)
                        .OrderBy(x => x.ScheduledUtc)
                        .ToList();
                }
            }

            // 8. Build final VM
            var vm = new StudentReactiveCourseDetailsVm
            {
                CourseId = course.Id,
                Title = course.Title,
                Description = course.Description,
                CoverPublicUrl = coverPublicUrl,
                IntroVideoUrl = course.IntroVideoUrl,
                PricePerMonth = course.PricePerMonth,
                PricePerMonthLabel = course.PricePerMonth.ToEuro(),
                DurationMonths = course.DurationMonths,
                Capacity = course.Capacity,
                StartDateUtc = course.StartDate,
                EndDateUtc = course.EndDate,
                Months = monthsBase,
                IsEnrolled = enrollment != null,
                HasPendingEnrollment = enrollment != null && paymentList.Any(p => p.Status == EnrollmentMonthPaymentStatus.Pending),
                HasAnyPaidMonth = enrollment != null && paymentList.Any(p => p.Status == EnrollmentMonthPaymentStatus.Paid),
                EnrollmentId = enrollmentId
            };

            vm.IntroYouTubeId = YouTubeHelper.ExtractYouTubeId(vm.IntroVideoUrl);
            ViewData["ActivePage"] = "MyEnrollments";
            return View(vm);
        }


        // POST: Student/ReactiveEnrollments/Enroll
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Enroll([FromForm] int courseId)
        {
            var studentId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(studentId)) return Challenge();

            string msgNotFound = _localizer["ReactiveCourse.NotFound"].Value;
            string msgCourseFull = _localizer["ReactiveCourse.CourseFull"].Value;
            string msgServerErr = _localizer["Admin.ServerError"].Value;

            var course = await _db.ReactiveCourses
                .Where(c => c.Id == courseId && !c.IsArchived)
                .Select(c => new { c.Id, c.Capacity, Enrolled = c.Enrollments.Count, c.TeacherId, c.Title })
                .FirstOrDefaultAsync();

            if (course == null) return Json(new { success = false, error = "NotFound", message = msgNotFound });

            if (course.Capacity > 0 && course.Enrolled >= course.Capacity)
                return Json(new { success = false, error = "CourseFull", message = msgCourseFull });

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var existing = await _db.ReactiveEnrollments.FirstOrDefaultAsync(e => e.ReactiveCourseId == courseId && e.StudentId == studentId);
                if (existing != null)
                {
                    return Json(new { success = true, alreadyEnrolled = true, enrollmentId = existing.Id });
                }

                var enr = new ReactiveEnrollment
                {
                    ReactiveCourseId = courseId,
                    StudentId = studentId,
                    CreatedAtUtc = DateTime.UtcNow,
                    IsApproved = false
                };
                _db.ReactiveEnrollments.Add(enr);
                await _db.SaveChangesAsync();

                _db.ReactiveEnrollmentLogs.Add(new ReactiveEnrollmentLog
                {
                    ReactiveCourseId = courseId,
                    EnrollmentId = enr.Id,
                    ActorId = studentId,
                    ActorName = User.Identity?.Name,
                    Action = "RequestedEnrollment",
                    CreatedAtUtc = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();

                await tx.CommitAsync();

                _ = NotifyTeacherOfEnrollmentAsync(course.TeacherId, course.Title, studentId);

                return Json(new { success = true, enrollmentId = enr.Id, message = _localizer["Enroll.Success"].Value });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Enroll error for course {CourseId} student {StudentId}", courseId, studentId);
                try { await tx.RollbackAsync(); } catch { }
                return StatusCode(500, new { success = false, error = "ServerError", message = msgServerErr });
            }
        }


        // POST: Student/ReactiveEnrollments/CancelEnroll
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelEnroll([FromForm] int courseId)
        {
            var studentId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(studentId)) return Challenge();

            var msgNotEnrolled = _localizer["ReactiveCourse.NotEnrolled"].Value;
            var msgHasPaid = _localizer["ReactiveCourse.HasPaidMonths"].Value;
            // new: localized message instructing user to cancel pending month payments first
            var msgHasPending = _localizer["ReactiveCourse.MustCancelPendingPaymentsFirst"].Value ?? "Please cancel pending month payments before cancelling enrollment.";
            var msgSuccess = _localizer["Cancel.Success"].Value;
            var msgError = _localizer["Cancel.Error"].Value;

            var enr = await _db.ReactiveEnrollments
                .Include(e => e.MonthPayments)
                .FirstOrDefaultAsync(e => e.ReactiveCourseId == courseId && e.StudentId == studentId);

            if (enr == null) return Json(new { success = false, error = "NotEnrolled", message = msgNotEnrolled });

            // Disallow cancel if any paid months exist
            if (enr.MonthPayments.Any(m => m.Status == EnrollmentMonthPaymentStatus.Paid))
                return Json(new { success = false, error = "HasPaidMonths", message = msgHasPaid });

            // NEW: Disallow cancel if any pending month payment exists (student must cancel those first)
            if (enr.MonthPayments.Any(m => m.Status == EnrollmentMonthPaymentStatus.Pending))
                return Json(new { success = false, error = "HasPendingPayments", message = msgHasPending });

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // Remove all non-paid month payments (Pending/Rejected/Cancelled) for this enrollment
                var nonPaid = enr.MonthPayments.Where(m => m.Status != EnrollmentMonthPaymentStatus.Paid).ToList();
                if (nonPaid.Any())
                {
                    _db.ReactiveEnrollmentMonthPayments.RemoveRange(nonPaid);
                    await _db.SaveChangesAsync();
                }

                // Log optionally
                _db.ReactiveEnrollmentLogs.Add(new ReactiveEnrollmentLog
                {
                    ReactiveCourseId = courseId,
                    EnrollmentId = enr.Id,
                    ActorId = studentId,
                    ActorName = User.Identity?.Name,
                    Action = "CancelledEnrollment",
                    Note = $"Student cancelled enrollment. Removed {nonPaid.Count} non-paid month payments.",
                    CreatedAtUtc = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();

                // Delete enrollment
                _db.ReactiveEnrollments.Remove(enr);
                await _db.SaveChangesAsync();

                await tx.CommitAsync();

                return Json(new { success = true, message = msgSuccess });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CancelEnroll error course {CourseId} student {StudentId}", courseId, studentId);
                try { await tx.RollbackAsync(); } catch { }
                return StatusCode(500, new { success = false, error = "ServerError", message = msgError });
            }
        }


        // POST: Student/ReactiveEnrollments/RequestMonthPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestMonthPayment([FromForm] int courseId, [FromForm] int monthId)
        {
            var studentId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(studentId)) return Challenge();

            var msgNotFound = _localizer["ReactiveCourse.NotFound"].Value;
            var msgMonthNotFound = _localizer["ReactiveCourse.MonthNotFound"].Value;
            var msgMonthNotReady = _localizer["ReactiveCourse.MonthNotReady"].Value;
            var msgAlreadyRequested = _localizer["ReactiveCourse.PaymentAlreadyRequested"].Value;
            var msgCourseFull = _localizer["ReactiveCourse.CourseFull"].Value;
            var msgServerErr = _localizer["Admin.ServerError"].Value;

            // fetch course minimal info
            var course = await _db.ReactiveCourses
                .AsNoTracking()
                .Where(c => c.Id == courseId && !c.IsArchived)
                .Select(c => new { c.Id, c.PricePerMonth, c.TeacherId, c.Title, c.Capacity })
                .FirstOrDefaultAsync();

            if (course == null) return Json(new { success = false, error = "NotFound", message = msgNotFound });

            var month = await _db.ReactiveCourseMonths
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == monthId && m.ReactiveCourseId == courseId);

            if (month == null) return Json(new { success = false, error = "MonthNotFound", message = msgMonthNotFound });
            if (!month.IsReadyForPayment) return Json(new { success = false, error = "MonthNotReady", message = msgMonthNotReady });

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var enrollment = await _db.ReactiveEnrollments
                    .FirstOrDefaultAsync(e => e.ReactiveCourseId == courseId && e.StudentId == studentId);

                if (enrollment == null)
                {
                    // capacity guard
                    var enrolledCount = await _db.ReactiveEnrollments.CountAsync(e => e.ReactiveCourseId == courseId);
                    if (course.Capacity > 0 && enrolledCount >= course.Capacity)
                    {
                        return Json(new { success = false, error = "CourseFull", message = msgCourseFull });
                    }

                    enrollment = new ReactiveEnrollment
                    {
                        ReactiveCourseId = courseId,
                        StudentId = studentId,
                        CreatedAtUtc = DateTime.UtcNow,
                        IsApproved = false
                    };
                    _db.ReactiveEnrollments.Add(enrollment);
                    await _db.SaveChangesAsync();

                    _db.ReactiveEnrollmentLogs.Add(new ReactiveEnrollmentLog
                    {
                        ReactiveCourseId = courseId,
                        EnrollmentId = enrollment.Id,
                        ActorId = studentId,
                        ActorName = User.Identity?.Name,
                        Action = "RequestedEnrollment",
                        CreatedAtUtc = DateTime.UtcNow
                    });
                    await _db.SaveChangesAsync();

                    _ = NotifyTeacherOfEnrollmentAsync(course.TeacherId, course.Title, studentId);
                }

                // duplicate pending guard (for this enrollment)
                var alreadyRequested = await _db.ReactiveEnrollmentMonthPayments
                    .AnyAsync(p => p.ReactiveEnrollmentId == enrollment.Id && p.ReactiveCourseMonthId == monthId && p.Status == EnrollmentMonthPaymentStatus.Pending);

                if (alreadyRequested)
                {
                    await tx.RollbackAsync();
                    return Json(new { success = false, error = "AlreadyRequested", message = msgAlreadyRequested });
                }

                var payment = new ReactiveEnrollmentMonthPayment
                {
                    ReactiveEnrollmentId = enrollment.Id,
                    ReactiveCourseMonthId = monthId,
                    Amount = course.PricePerMonth,
                    Status = EnrollmentMonthPaymentStatus.Pending,
                    CreatedAtUtc = DateTime.UtcNow
                };

                _db.ReactiveEnrollmentMonthPayments.Add(payment);
                await _db.SaveChangesAsync();

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

                await tx.CommitAsync();

                _ = NotifyTeacherOfPaymentRequestAsync(course.TeacherId, course.Title, month.MonthIndex, studentId);

                return Json(new { success = true, paymentId = payment.Id, enrollmentId = enrollment.Id, message = _localizer["ReactiveCourse.PaymentRequested"].Value });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RequestMonthPayment error course {CourseId} month {MonthId} student {StudentId}", courseId, monthId, studentId);
                try { await tx.RollbackAsync(); } catch { }
                return StatusCode(500, new { success = false, error = "ServerError", message = msgServerErr });
            }
        }


        // POST: Student/ReactiveEnrollments/CancelMonthPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelMonthPayment([FromForm] int paymentId)
        {
            var studentId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(studentId)) return Challenge();

            var msgNotFound = _localizer["ReactiveCourse.PaymentNotFound"].Value ?? "Payment not found";
            var msgInvalid = _localizer["ReactiveCourse.PaymentInvalidState"].Value ?? "Invalid payment state";
            var msgSuccess = _localizer["Cancel.Success"].Value;
            var msgError = _localizer["Cancel.Error"].Value;

            // Load payment with its enrollment (we need enrollment.StudentId)
            var payment = await _db.ReactiveEnrollmentMonthPayments
                .Include(p => p.ReactiveEnrollment)
                .FirstOrDefaultAsync(p => p.Id == paymentId && p.ReactiveEnrollment.StudentId == studentId);

            if (payment == null)
                return Json(new { success = false, error = "NotFound", message = msgNotFound });

            // Disallow cancelling a paid payment
            if (payment.Status == EnrollmentMonthPaymentStatus.Paid)
                return Json(new { success = false, error = "InvalidState", message = msgInvalid });

            try
            {
                // Remove the row entirely (student-cancel of a pending/rejected/cancelled request)
                _db.ReactiveEnrollmentMonthPayments.Remove(payment);
                await _db.SaveChangesAsync();

                // Log the cancellation (best-effort)
                try
                {
                    _db.ReactiveEnrollmentLogs.Add(new ReactiveEnrollmentLog
                    {
                        ReactiveCourseId = payment.ReactiveEnrollment.ReactiveCourseId,
                        EnrollmentId = payment.ReactiveEnrollmentId,
                        ActorId = studentId,
                        ActorName = User.Identity?.Name,
                        Action = "StudentCancelledMonthPayment",
                        Note = $"Deleted PaymentId={paymentId}",
                        CreatedAtUtc = DateTime.UtcNow
                    });
                    await _db.SaveChangesAsync();
                }
                catch
                {
                    // swallow logging error
                }

                return Json(new { success = true, message = msgSuccess });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CancelMonthPayment error payment {PaymentId} student {StudentId}", paymentId, studentId);
                return StatusCode(500, new { success = false, error = "ServerError", message = msgError });
            }
        }

        // sample fire-and-forget notification helpers (simple)
        private async Task NotifyTeacherOfEnrollmentAsync(string teacherId, string courseTitle, string studentId)
        {
            try
            {
                var teacher = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == teacherId);
                if (teacher != null && !string.IsNullOrEmpty(teacher.Email))
                {
                    var subj = _localizer["Email.Enrollment.Requested.Subject"].Value;
                    var body = string.Format(_localizer["Email.Enrollment.Requested.Body"].Value, courseTitle, User.Identity?.Name ?? studentId);
                    await _emailSender.SendEmailAsync(teacher.Email, subj, body);
                }
            }
            catch { }
        }

        private async Task NotifyTeacherOfPaymentRequestAsync(string teacherId, string courseTitle, int monthIndex, string studentId)
        {
            try
            {
                var teacher = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == teacherId);
                if (teacher != null && !string.IsNullOrEmpty(teacher.Email))
                {
                    var subj = _localizer["Email.Payment.Requested.Subject"].Value;
                    var body = string.Format(_localizer["Email.Payment.Requested.Body"].Value, courseTitle, monthIndex, User.Identity?.Name ?? studentId);
                    await _emailSender.SendEmailAsync(teacher.Email, subj, body);
                }
            }
            catch { }
        }

        // GET: Student/ReactiveEnrollments/MyEnrollments
        public async Task<IActionResult> MyEnrollments()
        {
            var studentId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(studentId)) return Challenge();

            // load enrollments with minimal needed fields to avoid heavy includes
            var enrollments = await _db.ReactiveEnrollments
                .Where(e => e.StudentId == studentId)
                .Select(e => new
                {
                    e.Id,
                    e.ReactiveCourseId,
                    CourseTitle = e.ReactiveCourse.Title,
                    Months = e.ReactiveCourse.Months.Select(m => new { m.Id, m.MonthIndex, m.IsReadyForPayment }).OrderBy(m => m.MonthIndex).ToList(),
                    MonthPayments = e.MonthPayments.Select(mp => new { mp.ReactiveCourseMonthId, mp.Status }).ToList()
                })
                .AsNoTracking()
                .ToListAsync();

            var vm = enrollments.Select(e => new MyEnrollmentVm
            {
                EnrollmentId = e.Id,
                CourseId = e.ReactiveCourseId,
                CourseTitle = e.CourseTitle,
                Months = e.Months.Select(m => new MyEnrollmentMonthVm
                {
                    MonthId = m.Id,
                    MonthIndex = m.MonthIndex,
                    IsReadyForPayment = m.IsReadyForPayment,
                    PaymentStatus = e.MonthPayments.FirstOrDefault(x => x.ReactiveCourseMonthId == m.Id) == null
                        ? (EnrollmentMonthPaymentStatus?)null
                        : e.MonthPayments.First(x => x.ReactiveCourseMonthId == m.Id).Status
                }).ToList()
            }).ToList();

            ViewData["ActivePage"] = "MyEnrollments";
            return View(vm);
        }
    }
}
