// File: Areas/Student/Controllers/ReactiveEnrollmentsController.cs
using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Web.Areas.Student.ViewModels;
using Edu.Web.Helpers;
using Edu.Web.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Edu.Web.Areas.Student.Controllers
{
    [Area("Student")]
    public class ReactiveEnrollmentsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileStorageService _fileStorage;
        private readonly ILogger<ReactiveEnrollmentsController> _logger;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IWebHostEnvironment _env;
        private readonly INotificationService _notifier;
        public ReactiveEnrollmentsController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IFileStorageService fileStorage,
            ILogger<ReactiveEnrollmentsController> logger,
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

        // GET: /Student/ReactiveCourses/Details/5 
        [HttpGet("Student/ReactiveCourses/Details/{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken = default)
        {
            // Load lightweight course + months summary
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
                .FirstOrDefaultAsync(cancellationToken);

            if (course == null) return NotFound();

            // Resolve cover URL (best-effort)
            string? coverPublicUrl = null;
            if (!string.IsNullOrEmpty(course.CoverImageKey))
            {
                try { coverPublicUrl = await _fileStorage.GetPublicUrlAsync(course.CoverImageKey); }
                catch { coverPublicUrl = course.CoverImageKey; }
            }

            // Only treat current user as a student when in Student role
            string? studentId = null;
            if (User?.Identity?.IsAuthenticated == true && User.IsInRole("Student"))
            {
                studentId = _userManager.GetUserId(User);
            }

            var monthsBase = course.Months
                .Select(m => new StudentCourseMonthVm
                {
                    Id = m.Id,
                    MonthIndex = m.MonthIndex,
                    MonthStartUtc = m.MonthStartUtc,
                    MonthEndUtc = m.MonthEndUtc,
                    IsReadyForPayment = m.IsReadyForPayment,
                    LessonsCount = m.LessonsCount
                })
                .OrderBy(m => m.MonthIndex)
                .ToList();

            // Public view (no payment/enrollment info)
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

            // load student's enrollment (if any)
            var enrollment = await _db.ReactiveEnrollments
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.ReactiveCourseId == course.Id && e.StudentId == studentId, cancellationToken);

            int? enrollmentId = enrollment?.Id;

            // payments for this enrollment
            var paymentList = new List<ReactiveEnrollmentMonthPayment>();
            if (enrollmentId.HasValue)
            {
                paymentList = await _db.ReactiveEnrollmentMonthPayments
                    .AsNoTracking()
                    .Where(p => p.ReactiveEnrollmentId == enrollmentId.Value)
                    .ToListAsync(cancellationToken);
            }

            // Build months with payment state
            foreach (var m in monthsBase)
            {
                var payment = paymentList.FirstOrDefault(p => p.ReactiveCourseMonthId == m.Id);
                m.PaymentId = payment?.Id;
                m.MyPaymentStatus = payment?.Status;

                m.HasPaidPayment = (payment != null && payment.Status == EnrollmentMonthPaymentStatus.Paid);
                m.CanRequestPayment = m.IsReadyForPayment && !m.HasPaidPayment && (payment == null);
                m.CanCancelPayment = (payment != null && payment.Status == EnrollmentMonthPaymentStatus.Pending);
                m.CanViewLessons = (payment != null && payment.Status == EnrollmentMonthPaymentStatus.Paid);
            }

            // If any paid months -> load lessons + attachments for those paid months
            var paidMonthIds = monthsBase.Where(m => m.CanViewLessons).Select(m => m.Id).ToList();
            var lessonVmList = new List<StudentCourseLessonVm>();
            if (paidMonthIds.Any())
            {
                // load lessons (light projection)
                var lessons = await _db.ReactiveCourseLessons
                    .AsNoTracking()
                    .Where(l => l.ReactiveCourseMonthId != null && paidMonthIds.Contains(l.ReactiveCourseMonthId))
                    .OrderBy(l => l.ScheduledUtc)
                    .Select(l => new
                    {
                        l.Id,
                        l.Title,
                        l.ScheduledUtc,
                        l.MeetUrl,
                        l.Notes,
                        l.RecordedVideoUrl,
                        ReactiveCourseMonthId = l.ReactiveCourseMonthId
                    })
                    .ToListAsync(cancellationToken);

                var lessonIds = lessons.Select(x => x.Id).ToList();

                // fetch file resources for these lessons
                var files = new List<FileResource>();
                if (lessonIds.Any())
                {
                    files = await _db.FileResources
                        .AsNoTracking()
                        .Where(fr => fr.ReactiveCourseLessonId != null && lessonIds.Contains(fr.ReactiveCourseLessonId.Value))
                        .ToListAsync(cancellationToken);
                }

                // prepare distinct keys to resolve public urls asynchronously (batch)
                var distinctKeys = files
                    .Select(fr => fr.StorageKey ?? fr.FileUrl)
                    .Where(k => !string.IsNullOrEmpty(k))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // helper: normalize provider returned url or stored FileUrl/key
                string? NormalizeUrl(string? u)
                {
                    if (string.IsNullOrWhiteSpace(u)) return null;
                    u = u.Trim().Trim('"', '\'');
                    if (u.StartsWith("~")) u = Url.Content(u);
                    if (Uri.TryCreate(u, UriKind.Absolute, out _)) return u;
                    if (!u.StartsWith("/")) u = "/" + u.TrimStart('/');
                    return u;
                }

                var keyToUrl = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                if (distinctKeys.Any())
                {
                    try
                    {
                        var urlTasks = distinctKeys.Select(k => _fileStorage.GetPublicUrlAsync(k!)).ToArray();
                        var urls = await Task.WhenAll(urlTasks);
                        keyToUrl = distinctKeys
                            .Select((k, i) => (Key: k!, Url: NormalizeUrl(urls[i]) ?? NormalizeUrl(k)))
                            .ToDictionary(x => x.Key, x => x.Url, StringComparer.OrdinalIgnoreCase);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Some file public URL lookups failed for reactive course {CourseId}", course.Id);
                        // fallback: keyToUrl left empty and we'll fallback to FileUrl / StorageKey
                    }
                }

                // map lessons -> VMs, using keyToUrl to fill PublicUrl and set DownloadUrl fallback
                lessonVmList = lessons.Select(l =>
                {
                    var attached = files
                        .Where(f => f.ReactiveCourseLessonId == l.Id)
                        .Select(ff =>
                        {
                            var keyOrUrl = ff.StorageKey ?? ff.FileUrl;
                            string? publicUrl = null;
                            if (!string.IsNullOrEmpty(keyOrUrl) && keyToUrl.TryGetValue(keyOrUrl, out var resolved) && !string.IsNullOrEmpty(resolved))
                                publicUrl = resolved;
                            else
                                publicUrl = NormalizeUrl(ff.FileUrl ?? ff.StorageKey);

                            return new ReactiveCourseLessonFileVm
                            {
                                Id = ff.Id,
                                FileName = ff.Name ?? System.IO.Path.GetFileName(ff.FileUrl ?? ff.StorageKey ?? ""),
                                PublicUrl = publicUrl,
                                // server download fallback (enforces auth/permissions; student role allowed)
                                DownloadUrl = Url.Action("Download", "FileResources", new { area = "Admin", id = ff.Id })
                            };
                        })
                        .ToList();

                    return new StudentCourseLessonVm
                    {
                        Id = l.Id,
                        Title = l.Title,
                        ScheduledUtc = l.ScheduledUtc,
                        MeetUrl = l.MeetUrl,
                        Notes = l.Notes,
                        RecordedVideoUrl = l.RecordedVideoUrl,
                        ReactiveCourseMonthId = l.ReactiveCourseMonthId,
                        Files = attached
                    };
                }).ToList();
            }

            // attach lessons to months
            foreach (var monthVm in monthsBase.Where(m => paidMonthIds.Contains(m.Id)))
            {
                monthVm.Lessons = lessonVmList
                    .Where(x => x.ReactiveCourseMonthId == monthVm.Id)
                    .OrderBy(x => x.ScheduledUtc)
                    .ToList();
            }

            // build final VM
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
            ViewData["ActivePage"] = "MyReactiveEnrollments";
            return View(vm);
        }


        // POST: Student/ReactiveEnrollments/Enroll
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Enroll([FromForm] int courseId)
        {
            var studentId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(studentId)) return Challenge();

            var msgNotFound = _localizer["ReactiveCourse.NotFound"].Value ?? "Course not found";
            var msgCourseFull = _localizer["ReactiveCourse.CourseFull"].Value ?? "Course is full";
            var msgServerErr = _localizer["Admin.ServerError"].Value ?? "Server error";

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

                await tx.CommitAsync();

                // notify teacher + admins (best-effort)
                await NotifyFireAndForgetAsync(async () =>
                {
                    var teacher = !string.IsNullOrEmpty(course.TeacherId) ? await _userManager.FindByIdAsync(course.TeacherId) : null;
                    if (teacher != null && !string.IsNullOrEmpty(teacher.Email))
                    {
                        await _notifier.SendLocalizedEmailAsync(
                            teacher,
                            "Email.Enrollment.Requested.Subject",
                            "Email.Enrollment.Requested.Body",
                            course.Title, User.Identity?.Name ?? studentId
                        );
                    }

                    await _notifier.NotifyAllAdminsAsync(
                        "Email.Admin.Enrollment.Requested.Subject",
                        "Email.Admin.Enrollment.Requested.Body",
                        async admin =>
                        {
                            var teacherDisplayName = teacher?.UserName ?? teacher?.Email ?? course.TeacherId ?? "Teacher";
                            return new object[] { course.Title, teacherDisplayName, User.Identity?.Name ?? studentId };
                        }
                    );
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

        // POST: Student/ReactiveEnrollments/CancelEnroll
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> CancelEnroll([FromForm] int courseId)
        {
            var studentId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(studentId)) return Challenge();

            var msgNotEnrolled = _localizer["ReactiveCourse.NotEnrolled"].Value ?? "Not enrolled";
            var msgHasPaid = _localizer["ReactiveCourse.HasPaidMonths"].Value ?? "You have paid months; cannot cancel";
            var msgHasPending = _localizer["ReactiveCourse.MustCancelPendingPaymentsFirst"].Value ?? "Please cancel pending month payments before cancelling enrollment.";
            var msgSuccess = _localizer["Cancel.Success"].Value ?? "Cancelled";
            var msgError = _localizer["Cancel.Error"].Value ?? "Error";

            var enr = await _db.ReactiveEnrollments
                .Include(e => e.MonthPayments)
                .FirstOrDefaultAsync(e => e.ReactiveCourseId == courseId && e.StudentId == studentId);

            if (enr == null) return Json(new { success = false, error = "NotEnrolled", message = msgNotEnrolled });

            if (enr.MonthPayments.Any(m => m.Status == EnrollmentMonthPaymentStatus.Paid))
                return Json(new { success = false, error = "HasPaidMonths", message = msgHasPaid });

            if (enr.MonthPayments.Any(m => m.Status == EnrollmentMonthPaymentStatus.Pending))
                return Json(new { success = false, error = "HasPendingPayments", message = msgHasPending });

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var nonPaid = enr.MonthPayments.Where(m => m.Status != EnrollmentMonthPaymentStatus.Paid).ToList();
                if (nonPaid.Any())
                {
                    _db.ReactiveEnrollmentMonthPayments.RemoveRange(nonPaid);
                    await _db.SaveChangesAsync();
                }

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
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> RequestMonthPayment([FromForm] int courseId, [FromForm] int monthId)
        {
            var studentId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(studentId)) return Challenge();

            var msgNotFound = _localizer["ReactiveCourse.NotFound"].Value ?? "Course not found";
            var msgMonthNotFound = _localizer["ReactiveCourse.MonthNotFound"].Value ?? "Month not found";
            var msgMonthNotReady = _localizer["ReactiveCourse.MonthNotReady"].Value ?? "Month not ready";
            var msgAlreadyRequested = _localizer["ReactiveCourse.PaymentAlreadyRequested"].Value ?? "Already requested";
            var msgCourseFull = _localizer["ReactiveCourse.CourseFull"].Value ?? "Course is full";
            var msgServerErr = _localizer["Admin.ServerError"].Value ?? "Server error";

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

                    // notify teacher + admins
                    await NotifyFireAndForgetAsync(() => NotifyTeacherOfEnrollmentAsync(course.TeacherId, course.Title, studentId));
                }

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

                await tx.CommitAsync();

                // notify teacher + admins about payment request
                await NotifyFireAndForgetAsync(() => NotifyTeacherOfPaymentRequestAsync(course.TeacherId, course.Title, month.MonthIndex, studentId));

                return Json(new { success = true, paymentId = payment.Id, enrollmentId = enrollment.Id, message = _localizer["ReactiveCourse.PaymentRequested"].Value ?? "Payment requested" });
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
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> CancelMonthPayment([FromForm] int paymentId)
        {
            var studentId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(studentId)) return Challenge();

            var msgNotFound = _localizer["ReactiveCourse.PaymentNotFound"].Value ?? "Payment not found";
            var msgInvalid = _localizer["ReactiveCourse.PaymentInvalidState"].Value ?? "Invalid payment state";
            var msgSuccess = _localizer["Cancel.Success"].Value ?? "Cancelled";
            var msgError = _localizer["Cancel.Error"].Value ?? "Error";

            var payment = await _db.ReactiveEnrollmentMonthPayments
                .Include(p => p.ReactiveEnrollment)
                .FirstOrDefaultAsync(p => p.Id == paymentId && p.ReactiveEnrollment.StudentId == studentId);

            if (payment == null)
                return Json(new { success = false, error = "NotFound", message = msgNotFound });

            if (payment.Status == EnrollmentMonthPaymentStatus.Paid)
                return Json(new { success = false, error = "InvalidState", message = msgInvalid });

            try
            {
                _db.ReactiveEnrollmentMonthPayments.Remove(payment);
                await _db.SaveChangesAsync();

                return Json(new { success = true, message = msgSuccess });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CancelMonthPayment error payment {PaymentId} student {StudentId}", paymentId, studentId);
                return StatusCode(500, new { success = false, error = "ServerError", message = msgError });
            }
        }

        // Notification helpers
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

        private async Task NotifyTeacherOfEnrollmentAsync(string teacherId, string courseTitle, string studentId)
        {
            try
            {
                var teacher = !string.IsNullOrEmpty(teacherId) ? await _userManager.FindByIdAsync(teacherId) : null;
                var student = !string.IsNullOrEmpty(studentId) ? await _userManager.FindByIdAsync(studentId) : null;
                var studentDisplayName = student?.UserName ?? student?.Email ?? studentId ?? "Student";

                if (teacher != null && !string.IsNullOrEmpty(teacher.Email))
                {
                    await _notifier.SendLocalizedEmailAsync(
                        teacher,
                        "Email.Enrollment.Requested.Subject",
                        "Email.Enrollment.Requested.Body",
                        courseTitle, studentDisplayName
                    );
                }

                await _notifier.NotifyAllAdminsAsync(
                    "Email.Admin.Enrollment.Requested.Subject",
                    "Email.Admin.Enrollment.Requested.Body",
                    async admin =>
                    {
                        var teacherDisplayName = teacher?.UserName ?? teacher?.Email ?? teacherId ?? "Teacher";
                        return new object[] { courseTitle, teacherDisplayName, studentDisplayName };
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NotifyTeacherOfEnrollmentAsync failed for teacher {TeacherId} student {StudentId}", teacherId, studentId);
            }
        }

        private async Task NotifyTeacherOfPaymentRequestAsync(string teacherId, string courseTitle, int monthIndex, string studentId)
        {
            try
            {
                var teacher = !string.IsNullOrEmpty(teacherId) ? await _userManager.FindByIdAsync(teacherId) : null;
                var student = !string.IsNullOrEmpty(studentId) ? await _userManager.FindByIdAsync(studentId) : null;
                var studentDisplayName = student?.UserName ?? student?.Email ?? studentId ?? "Student";

                if (teacher != null && !string.IsNullOrEmpty(teacher.Email))
                {
                    await _notifier.SendLocalizedEmailAsync(
                        teacher,
                        "Email.PaymentReactiveCourse.Requested.Subject",
                        "Email.PaymentReactiveCourse.Requested.Body",
                        courseTitle, monthIndex, studentDisplayName
                    );
                }

                await _notifier.NotifyAllAdminsAsync(
                    "Email.Admin.Payment.Requested.Subject",
                    "Email.Admin.Payment.Requested.Body",
                    async admin =>
                    {
                        var teacherDisplayName = teacher?.UserName ?? teacher?.Email ?? teacherId ?? "Teacher";
                        return new object[] { courseTitle, monthIndex, teacherDisplayName, studentDisplayName };
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NotifyTeacherOfPaymentRequestAsync failed for teacher {TeacherId} course {Course} month {MonthIndex} student {StudentId}", teacherId, courseTitle, monthIndex, studentId);
            }
        }

        // GET: Student/ReactiveEnrollments/MyEnrollments
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MyEnrollments()
        {
            var studentId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(studentId)) return Challenge();

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

            ViewData["ActivePage"] = "MyReactiveEnrollments";
            return View(vm);
        }
    }
}


