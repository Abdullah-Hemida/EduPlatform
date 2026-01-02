using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Web.Helpers;
using Edu.Web.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.EntityFrameworkCore;
using Edu.Web.Areas.Student.ViewModels;

namespace Edu.Web.Areas.Student.Controllers
{
    [Area("Student")]
    public class OnlineSchoolEnrollmentsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileStorageService _fileStorage;
        private readonly ILogger<OnlineSchoolEnrollmentsController> _logger;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IWebHostEnvironment _env;
        private readonly INotificationService _notifier;

        public OnlineSchoolEnrollmentsController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IFileStorageService fileStorage,
            ILogger<OnlineSchoolEnrollmentsController> logger,
            IStringLocalizer<SharedResource> localizer,
            IWebHostEnvironment env,
            INotificationService notifier)
        {
            _db = db;
            _userManager = userManager;
            _fileStorage = fileStorage;
            _logger = logger;
            _localizer = localizer;
            _env = env;
            _notifier = notifier;
        }

        // GET: Student/OnlineSchool/Details/5
        [HttpGet("Student/OnlineSchool/Details/{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> Details(int id)
        {
            // 1) load lightweight course + months (lessons count only)
            var course = await _db.OnlineCourses
                .AsNoTracking()
                .Where(c => c.Id == id && c.IsPublished)
                .Select(c => new
                {
                    c.Id,
                    c.Title,
                    c.Description,
                    c.CoverImageKey,
                    c.IntroductionVideoUrl,
                    c.PricePerMonth,
                    c.DurationMonths,
                    c.LevelId,
                    c.TeacherName,
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

            // 2) Resolve cover URL (best-effort)
            string? coverPublicUrl = null;
            if (!string.IsNullOrEmpty(course.CoverImageKey))
            {
                try
                {
                    coverPublicUrl = await _fileStorage.GetPublicUrlAsync(course.CoverImageKey);
                }
                catch
                {
                    // fallback to stored key if storage fails
                    coverPublicUrl = course.CoverImageKey;
                }
            }

            // 3) Only treat the current user as a student if they are in Student role
            string? studentId = null;
            if (User?.Identity?.IsAuthenticated == true && User.IsInRole("Student"))
            {
                studentId = _userManager.GetUserId(User);
            }

            // Build base months list (no payment state yet)
            var monthsBase = course.Months.Select(m => new OnlineStudentCourseMonthVm
            {
                Id = m.Id,
                MonthIndex = m.MonthIndex,
                MonthStartUtc = m.MonthStartUtc,
                MonthEndUtc = m.MonthEndUtc,
                IsReadyForPayment = m.IsReadyForPayment,
                LessonsCount = m.LessonsCount
            }).OrderBy(m => m.MonthIndex).ToList();

            // Public view when not authenticated as Student
            if (string.IsNullOrEmpty(studentId))
            {
                var publicVm = new OnlineStudentCourseDetailsVm
                {
                    CourseId = course.Id,
                    Title = course.Title,
                    Description = course.Description,
                    CoverPublicUrl = coverPublicUrl,
                    IntroVideoUrl = course.IntroductionVideoUrl,
                    PricePerMonth = course.PricePerMonth,
                    PricePerMonthLabel = course.PricePerMonth.ToEuro(),
                    DurationMonths = course.DurationMonths,
                    Months = monthsBase
                };
                publicVm.IntroYouTubeId = YouTubeHelper.ExtractYouTubeId(publicVm.IntroVideoUrl);
                return View(publicVm);
            }

            // 4) Load student's enrollment (if any)
            var enrollment = await _db.OnlineEnrollments
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.OnlineCourseId == course.Id && e.StudentId == studentId);

            int? enrollmentId = enrollment?.Id;

            // 5) Load payments for this enrollment (single query)
            var paymentList = new List<OnlineEnrollmentMonthPayment>();
            if (enrollmentId.HasValue)
            {
                paymentList = await _db.OnlineEnrollmentMonthPayments
                    .AsNoTracking()
                    .Where(p => p.OnlineEnrollmentId == enrollmentId.Value)
                    .ToListAsync();
            }

            // 6) Build months VMs with per-student payment state + permissions
            foreach (var m in monthsBase)
            {
                var payment = paymentList.FirstOrDefault(p => p.OnlineCourseMonthId == m.Id);
                m.PaymentId = payment?.Id;
                m.MyPaymentStatus = payment?.Status;

                // mark explicit boolean for convenience (Paid means student already paid for this month)
                m.HasPaidPayment = (payment != null && payment.Status == OnlineEnrollmentMonthPaymentStatus.Paid);

                // Allow request only when admin marked ready AND the student doesn't already have a paid payment for that month.
                // (If a Pending or Rejected payment exists, request button will be controlled by CanRequestPayment logic - here we only block when Paid)
                m.CanRequestPayment = m.IsReadyForPayment && !m.HasPaidPayment && (payment == null);

                m.CanCancelPayment = (payment != null && payment.Status == OnlineEnrollmentMonthPaymentStatus.Pending);
                m.CanViewLessons = (payment != null && payment.Status == OnlineEnrollmentMonthPaymentStatus.Paid);
            }

            // 7) If any paid months -> load lessons only for paid months
            var paidMonthIds = monthsBase.Where(m => m.CanViewLessons).Select(m => m.Id).ToList();
            if (paidMonthIds.Any())
            {
                var lessons = await _db.OnlineCourseLessons
                    .AsNoTracking()
                    .Where(l => l.OnlineCourseMonthId != null && paidMonthIds.Contains(l.OnlineCourseMonthId.Value))
                    .OrderBy(l => l.ScheduledUtc)
                    .Select(l => new
                    {
                        l.Id,
                        l.OnlineCourseMonthId,
                        l.Title,
                        l.ScheduledUtc,
                        l.MeetUrl,
                        l.RecordedVideoUrl,
                        l.Notes,
                        Files = l.Files.Select(f => new { f.Id, f.Name, f.FileUrl, f.StorageKey }).ToList()
                    })
                    .ToListAsync();

                // map lessons -> month VMs
                foreach (var monthVm in monthsBase.Where(m => paidMonthIds.Contains(m.Id)))
                {
                    var lessonsForMonth = lessons
                        .Where(x => x.OnlineCourseMonthId == monthVm.Id)
                        .OrderBy(x => x.ScheduledUtc)
                        .Select(x => new OnlineStudentCourseLessonVm
                        {
                            Id = x.Id,
                            Title = x.Title,
                            ScheduledUtc = x.ScheduledUtc,
                            MeetUrl = x.MeetUrl,
                            RecordedVideoUrl = x.RecordedVideoUrl,
                            Notes = x.Notes,
                            OnlineCourseMonthId = x.OnlineCourseMonthId,
                            Files = x.Files.Select(ff => new OnlineStudentCourseFileVm
                            {
                                Id = ff.Id,
                                FileName = ff.Name ?? System.IO.Path.GetFileName(ff.FileUrl ?? ff.StorageKey ?? ""),
                                PublicUrl = string.IsNullOrEmpty(ff.StorageKey) ? ff.FileUrl : null // resolve below async if necessary
                            }).ToList()
                        }).ToList();

                    // resolve file storage urls where needed (avoid blocking EF)
                    foreach (var lessonVm in lessonsForMonth)
                    {
                        foreach (var f in lessonVm.Files)
                        {
                            if (string.IsNullOrEmpty(f.PublicUrl))
                            {
                                try
                                {
                                    var fr = _db.FileResources.AsNoTracking().FirstOrDefault(x => x.Id == f.Id);
                                    if (fr != null && !string.IsNullOrEmpty(fr.StorageKey))
                                    {
                                        try
                                        {
                                            f.PublicUrl = _fileStorage.GetPublicUrlAsync(fr.StorageKey).GetAwaiter().GetResult();
                                        }
                                        catch
                                        {
                                            f.PublicUrl = fr.FileUrl ?? fr.StorageKey;
                                        }
                                    }
                                }
                                catch
                                {
                                    // swallow - file link will be null or whatever DB has
                                }
                            }
                        }
                    }

                    monthVm.Lessons = lessonsForMonth;
                }
            }

            // 8) Build final VM
            var vm = new OnlineStudentCourseDetailsVm
            {
                CourseId = course.Id,
                Title = course.Title,
                Description = course.Description,
                CoverPublicUrl = coverPublicUrl,
                IntroVideoUrl = course.IntroductionVideoUrl,
                PricePerMonth = course.PricePerMonth,
                PricePerMonthLabel = course.PricePerMonth.ToEuro(),
                DurationMonths = course.DurationMonths,
                Months = monthsBase,
                IsEnrolled = enrollment != null,
                HasPendingEnrollment = enrollment != null && paymentList.Any(p => p.Status == OnlineEnrollmentMonthPaymentStatus.Pending),
                HasAnyPaidMonth = enrollment != null && paymentList.Any(p => p.Status == OnlineEnrollmentMonthPaymentStatus.Paid),
                EnrollmentId = enrollmentId
            };

            vm.IntroYouTubeId = YouTubeHelper.ExtractYouTubeId(vm.IntroVideoUrl);

            ViewData["ActivePage"] = "MyOnlineSchoolEnrollments";
            return View(vm);
        }

        // POST: Student/OnlineSchoolEnrollments/Enroll
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Enroll([FromForm] int courseId)
        {
            var studentId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(studentId)) return Challenge();

            var msgNotFound = _localizer["OnlineCourse.NotFound"].Value ?? "Course not found";
            var msgServerErr = _localizer["Admin.ServerError"].Value ?? "Server error";

            // load minimal course
            var course = await _db.OnlineCourses
                .AsNoTracking()
                .Where(c => c.Id == courseId && c.IsPublished)
                .Select(c => new { c.Id, c.Title })
                .FirstOrDefaultAsync();

            if (course == null) return Json(new { success = false, error = "NotFound", message = msgNotFound });

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var existing = await _db.OnlineEnrollments.FirstOrDefaultAsync(e => e.OnlineCourseId == courseId && e.StudentId == studentId);
                if (existing != null)
                {
                    return Json(new { success = true, alreadyEnrolled = true, enrollmentId = existing.Id });
                }

                var enr = new OnlineEnrollment
                {
                    OnlineCourseId = courseId,
                    StudentId = studentId,
                    CreatedAtUtc = DateTime.UtcNow,
                    IsApproved = false
                };
                _db.OnlineEnrollments.Add(enr);
                await _db.SaveChangesAsync();

                // Optional: add Enrollment log table if you have one (not included in entities snippet)
                // _db.OnlineEnrollmentLogs.Add(...)

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                // Notify admins that a new enrollment was requested
                await NotifyFireAndForgetAsync(async () =>
                {
                    await NotifyAdminsOfEnrollmentRequestAsync(course.Title, studentId);
                });

                return Json(new { success = true, enrollmentId = enr.Id, message = _localizer["Enroll.Success"].Value ?? "Enrollment requested" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Enroll error for course {CourseId} student {StudentId}", courseId, studentId);
                try { await tx.RollbackAsync(); } catch { }
                return StatusCode(500, new { success = false, error = "ServerError", message = msgServerErr });
            }
        }

        // POST: Student/OnlineSchoolEnrollments/CancelEnroll
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> CancelEnroll([FromForm] int courseId)
        {
            var studentId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(studentId)) return Challenge();

            var msgNotEnrolled = _localizer["OnlineCourse.NotEnrolled"].Value ?? "Not enrolled";
            var msgHasPaid = _localizer["OnlineCourse.HasPaidMonths"].Value ?? "You have paid months; cannot cancel";
            var msgHasPending = _localizer["OnlineCourse.MustCancelPendingPaymentsFirst"].Value ?? "Please cancel pending month payments first.";
            var msgSuccess = _localizer["Cancel.Success"].Value ?? "Cancelled";
            var msgError = _localizer["Cancel.Error"].Value ?? "Error";

            var enr = await _db.OnlineEnrollments
                .Include(e => e.MonthPayments)
                .FirstOrDefaultAsync(e => e.OnlineCourseId == courseId && e.StudentId == studentId);

            if (enr == null) return Json(new { success = false, error = "NotEnrolled", message = msgNotEnrolled });

            // Disallow cancel if paid months exist
            if (enr.MonthPayments.Any(m => m.Status == OnlineEnrollmentMonthPaymentStatus.Paid))
                return Json(new { success = false, error = "HasPaidMonths", message = msgHasPaid });

            // Disallow cancel if pending month payments exist
            if (enr.MonthPayments.Any(m => m.Status == OnlineEnrollmentMonthPaymentStatus.Pending))
                return Json(new { success = false, error = "HasPendingPayments", message = msgHasPending });

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // Remove non-paid month payments
                var nonPaid = enr.MonthPayments.Where(m => m.Status != OnlineEnrollmentMonthPaymentStatus.Paid).ToList();
                if (nonPaid.Any())
                {
                    _db.OnlineEnrollmentMonthPayments.RemoveRange(nonPaid);
                    await _db.SaveChangesAsync();
                }

                // Remove enrollment
                _db.OnlineEnrollments.Remove(enr);
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

        // POST: Student/OnlineSchoolEnrollments/RequestMonthPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> RequestMonthPayment([FromForm] int courseId, [FromForm] int monthId)
        {
            var studentId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(studentId)) return Challenge();

            var msgNotFound = _localizer["OnlineCourse.NotFound"].Value ?? "Course not found";
            var msgMonthNotFound = _localizer["OnlineCourse.MonthNotFound"].Value ?? "Month not found";
            var msgMonthNotReady = _localizer["OnlineCourse.MonthNotReady"].Value ?? "Month not ready";
            var msgAlreadyRequested = _localizer["OnlineCourse.PaymentAlreadyRequested"].Value ?? "Already requested";
            var msgServerErr = _localizer["Admin.ServerError"].Value ?? "Server error";

            // fetch course minimal info
            var course = await _db.OnlineCourses
                .AsNoTracking()
                .Where(c => c.Id == courseId && c.IsPublished)
                .Select(c => new { c.Id, c.PricePerMonth, c.Title })
                .FirstOrDefaultAsync();

            if (course == null) return Json(new { success = false, error = "NotFound", message = msgNotFound });

            var month = await _db.OnlineCourseMonths
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == monthId && m.OnlineCourseId == courseId);

            if (month == null) return Json(new { success = false, error = "MonthNotFound", message = msgMonthNotFound });
            if (!month.IsReadyForPayment) return Json(new { success = false, error = "MonthNotReady", message = msgMonthNotReady });

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var enrollment = await _db.OnlineEnrollments.FirstOrDefaultAsync(e => e.OnlineCourseId == courseId && e.StudentId == studentId);

                if (enrollment == null)
                {
                    // create enrollment
                    enrollment = new OnlineEnrollment
                    {
                        OnlineCourseId = courseId,
                        StudentId = studentId,
                        CreatedAtUtc = DateTime.UtcNow,
                        IsApproved = false
                    };
                    _db.OnlineEnrollments.Add(enrollment);
                    await _db.SaveChangesAsync();

                    // optional: log
                }

                // duplicate pending guard
                var alreadyRequested = await _db.OnlineEnrollmentMonthPayments
                    .AnyAsync(p => p.OnlineEnrollmentId == enrollment.Id && p.OnlineCourseMonthId == monthId && p.Status == OnlineEnrollmentMonthPaymentStatus.Pending);

                if (alreadyRequested)
                {
                    await tx.RollbackAsync();
                    return Json(new { success = false, error = "AlreadyRequested", message = msgAlreadyRequested });
                }

                var payment = new OnlineEnrollmentMonthPayment
                {
                    OnlineEnrollmentId = enrollment.Id,
                    OnlineCourseMonthId = monthId,
                    Amount = course.PricePerMonth,
                    Status = OnlineEnrollmentMonthPaymentStatus.Pending,
                    CreatedAtUtc = DateTime.UtcNow
                };

                _db.OnlineEnrollmentMonthPayments.Add(payment);
                await _db.SaveChangesAsync();

                // optional: log entry

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                // notify admins about payment request
                await NotifyFireAndForgetAsync(async () =>
                {
                    await NotifyAdminsOfPaymentRequestAsync(course.Title, month.MonthIndex, studentId);
                });

                return Json(new { success = true, paymentId = payment.Id, enrollmentId = enrollment.Id, message = _localizer["OnlineCourse.PaymentRequested"].Value ?? "Payment requested" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RequestMonthPayment error course {CourseId} month {MonthId} student {StudentId}", courseId, monthId, studentId);
                try { await tx.RollbackAsync(); } catch { }
                return StatusCode(500, new { success = false, error = "ServerError", message = msgServerErr });
            }
        }

        // POST: Student/OnlineSchoolEnrollments/CancelMonthPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> CancelMonthPayment([FromForm] int paymentId)
        {
            var studentId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(studentId)) return Challenge();

            var msgNotFound = _localizer["OnlineCourse.PaymentNotFound"].Value ?? "Payment not found";
            var msgInvalid = _localizer["OnlineCourse.PaymentInvalidState"].Value ?? "Invalid payment state";
            var msgSuccess = _localizer["Cancel.Success"].Value ?? "Cancelled";
            var msgError = _localizer["Cancel.Error"].Value ?? "Error";

            var payment = await _db.OnlineEnrollmentMonthPayments
                .Include(p => p.OnlineEnrollment)
                .FirstOrDefaultAsync(p => p.Id == paymentId && p.OnlineEnrollment.StudentId == studentId);

            if (payment == null)
                return Json(new { success = false, error = "NotFound", message = msgNotFound });

            if (payment.Status == OnlineEnrollmentMonthPaymentStatus.Paid)
                return Json(new { success = false, error = "InvalidState", message = msgInvalid });

            try
            {
                _db.OnlineEnrollmentMonthPayments.Remove(payment);
                await _db.SaveChangesAsync();

                // optional: log
                return Json(new { success = true, message = msgSuccess });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CancelMonthPayment error payment {PaymentId} student {StudentId}", paymentId, studentId);
                return StatusCode(500, new { success = false, error = "ServerError", message = msgError });
            }
        }

        // GET: Student/OnlineSchoolEnrollments/MyEnrollments
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MyEnrollments()
        {
            var studentId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(studentId)) return Challenge();

            // load enrollments with minimal needed fields
            var enrollments = await _db.OnlineEnrollments
                .Where(e => e.StudentId == studentId)
                .Select(e => new
                {
                    e.Id,
                    e.OnlineCourseId,
                    CourseTitle = e.OnlineCourse.Title,
                    Months = e.OnlineCourse.Months.Select(m => new { m.Id, m.MonthIndex, m.IsReadyForPayment }).OrderBy(m => m.MonthIndex).ToList(),
                    MonthPayments = e.MonthPayments.Select(mp => new { mp.OnlineCourseMonthId, mp.Status }).ToList()
                })
                .AsNoTracking()
                .ToListAsync();

            var vm = enrollments.Select(e => new OnlineMyEnrollmentVm
            {
                EnrollmentId = e.Id,
                CourseId = e.OnlineCourseId,
                CourseTitle = e.CourseTitle,
                Months = e.Months.Select(m => new OnlineMyEnrollmentMonthVm
                {
                    MonthId = m.Id,
                    MonthIndex = m.MonthIndex,
                    IsReadyForPayment = m.IsReadyForPayment,
                    PaymentStatus = e.MonthPayments.FirstOrDefault(x => x.OnlineCourseMonthId == m.Id) == null
                        ? (OnlineEnrollmentMonthPaymentStatus?)null
                        : e.MonthPayments.First(x => x.OnlineCourseMonthId == m.Id).Status
                }).ToList()
            }).ToList();

            ViewData["ActivePage"] = "MyOnlineSchoolEnrollments";
            return View(vm);
        }

        // --- Notification helpers using INotificationService (centralized) ---
        private async Task NotifyFireAndForgetAsync(Func<Task> work)
        {
            if (work == null) return;
            try
            {
                if (_env?.IsDevelopment() == true)
                {
                    await work();
                }
                else
                {
                    _ = Task.Run(work);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NotifyFireAndForgetAsync failed executing notification work");
            }
        }

        private async Task NotifyAdminsOfEnrollmentRequestAsync(string courseTitle, string studentId)
        {
            try
            {
                await _notifier.NotifyAllAdminsAsync(
                    "Email.Admin.Enrollment.Requested.Subject",
                    "Email.Admin.Enrollment.Requested.Body",
                    async admin =>
                    {
                        var student = !string.IsNullOrEmpty(studentId) ? await _userManager.FindByIdAsync(studentId) : null;
                        var studentDisplay = student?.UserName ?? student?.Email ?? studentId;
                        return new object[] { courseTitle, studentDisplay };
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NotifyAdminsOfEnrollmentRequestAsync failed for course {Course} student {StudentId}", courseTitle, studentId);
            }
        }

        private async Task NotifyAdminsOfPaymentRequestAsync(string courseTitle, int monthIndex, string studentId)
        {
            try
            {
                await _notifier.NotifyAllAdminsAsync(
                    "Email.Admin.Payment.Requested.Subject",
                    "Email.Admin.Payment.Requested.Body",
                    async admin =>
                    {
                        var student = !string.IsNullOrEmpty(studentId) ? await _userManager.FindByIdAsync(studentId) : null;
                        var studentDisplay = student?.UserName ?? student?.Email ?? studentId;
                        return new object[] { courseTitle, monthIndex, studentDisplay };
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NotifyAdminsOfPaymentRequestAsync failed for course {Course} month {MonthIndex} student {StudentId}", courseTitle, monthIndex, studentId);
            }
        }
    }
}
