using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Web.Areas.Admin.ViewModels;
using Edu.Web.Helpers;
using Edu.Web.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Edu.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class SchoolEnrollmentsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<SchoolEnrollmentsController> _logger;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly INotificationService _notifier;
        private readonly IWebHostEnvironment _env;
        private readonly IFileStorageService _fileStorage;

        public SchoolEnrollmentsController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ILogger<SchoolEnrollmentsController> logger,
            IStringLocalizer<SharedResource> localizer,
            INotificationService notifier,
            IWebHostEnvironment env,
            IFileStorageService fileStorage)
        {
            _db = db;
            _userManager = userManager;
            _logger = logger;
            _localizer = localizer;
            _notifier = notifier;
            _env = env;
            _fileStorage = fileStorage;
        }

        // GET: Admin/OnlineEnrollments
        // supports ?q=search&page=1&pageSize=20
        [HttpGet]
        public async Task<IActionResult> Index(string? q, int page = 1, int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize <= 0) pageSize = 20;

            var baseQuery = _db.OnlineEnrollments
                .AsNoTracking()
                .Include(e => e.OnlineCourse)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                baseQuery = baseQuery.Where(e =>
                    EF.Functions.Like(e.OnlineCourse.Title, $"%{q}%") ||
                    (e.StudentId != null && _db.Users.Any(u => u.Id == e.StudentId && (EF.Functions.Like(u.FullName, $"%{q}%") || EF.Functions.Like(u.Email, $"%{q}%")))) ||
                    (e.OnlineCourse != null && e.OnlineCourse.TeacherName != null && EF.Functions.Like(e.OnlineCourse.TeacherName, $"%{q}%"))
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
                    CourseId = e.OnlineCourse.Id,
                    CourseTitle = e.OnlineCourse.Title,
                    TeacherName = e.OnlineCourse.TeacherName,
                    StudentId = e.StudentId,
                    PaidCount = e.MonthPayments.Count(m => m.Status == OnlineEnrollmentMonthPaymentStatus.Paid),
                    PendingCount = e.MonthPayments.Count(m => m.Status == OnlineEnrollmentMonthPaymentStatus.Pending)
                })
                .ToListAsync();

            var studentIds = items.Select(i => i.StudentId).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();

            var users = await _db.Users
                .AsNoTracking()
                .Where(u => studentIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FullName, u.Email, u.PhoneNumber, u.PhotoStorageKey, u.PhotoUrl })
                .ToListAsync();

            var students = await _db.Students
                .AsNoTracking()
                .Where(s => studentIds.Contains(s.Id))
                .Select(s => new { s.Id, s.GuardianPhoneNumber })
                .ToListAsync();

            var list = items.Select(i =>
            {
                var stu = users.FirstOrDefault(u => u.Id == i.StudentId);
                return new SchoolEnrollmentListItemVm
                {
                    EnrollmentId = i.Id,
                    CourseId = i.CourseId,
                    CourseTitle = i.CourseTitle,
                    StudentId = i.StudentId,
                    StudentFullName = stu?.FullName ?? i.StudentId,
                    StudentEmail = stu?.Email,
                    StudentPhone = stu?.PhoneNumber,
                    GuardianPhoneNumber = students.FirstOrDefault(s => s.Id == i.StudentId)?.GuardianPhoneNumber,
                    PhotoStorageKey = stu?.PhotoStorageKey,
                    TeacherFullName = i.TeacherName,
                    CreatedAtUtc = i.CreatedAtUtc,
                    PaidMonthsCount = i.PaidCount,
                    PendingMonthsCount = i.PendingCount
                };
            }).ToList();

            var keys = list.Select(x => x.PhotoStorageKey).Where(k => !string.IsNullOrEmpty(k)).Distinct().ToList();
            var keyToUrl = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            if (keys.Any())
            {
                var tasks = keys.Select(async key =>
                {
                    try { keyToUrl[key!] = await _fileStorage.GetPublicUrlAsync(key!); }
                    catch { keyToUrl[key!] = null; }
                });
                await Task.WhenAll(tasks);
            }

            foreach (var it in list)
            {
                if (!string.IsNullOrEmpty(it.PhotoStorageKey) && keyToUrl.TryGetValue(it.PhotoStorageKey!, out var url))
                    it.StudentPhotoUrl = url ?? it.PhotoStorageKey;
            }

            var vm = new SchoolEnrollmentIndexVm
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                Query = q ?? string.Empty,
                Items = list
            };

            ViewData["ActivePage"] = "SchoolEnrollments";
            return View(vm);
        }

        // GET: Admin/OnlineEnrollments/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var enrollment = await _db.OnlineEnrollments
                .AsNoTracking()
                .Include(e => e.OnlineCourse)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (enrollment == null)
            {
                _logger.LogWarning("Online enrollment {Id} not found", id);
                return NotFound();
            }

            var payments = await _db.OnlineEnrollmentMonthPayments
                .AsNoTracking()
                .Where(p => p.OnlineEnrollmentId == id)
                .ToListAsync();

            var months = await _db.OnlineCourseMonths
                .AsNoTracking()
                .Where(m => m.OnlineCourseId == enrollment.OnlineCourseId)
                .OrderBy(m => m.MonthIndex)
                .Select(m => new { m.Id, m.MonthIndex })
                .ToListAsync();

            var monthIds = months.Select(m => m.Id).ToList();
            Dictionary<int, int> lessonCounts = new();
            if (monthIds.Any())
            {
                var grouped = await _db.OnlineCourseLessons
                    .AsNoTracking()
                    .Where(l => l.OnlineCourseMonthId != null && monthIds.Contains(l.OnlineCourseMonthId.Value))
                    .GroupBy(l => l.OnlineCourseMonthId!.Value)
                    .Select(g => new { MonthId = g.Key, Count = g.Count() })
                    .ToListAsync();

                lessonCounts = grouped.ToDictionary(x => x.MonthId, x => x.Count);
                _logger.LogDebug("Online lesson groups: {Groups}", string.Join(" | ", grouped.Select(x => $"{x.MonthId}:{x.Count}")));
            }

            ApplicationUser? studentUser = null;
            Domain.Entities.Student? student = null;
            if (!string.IsNullOrEmpty(enrollment.StudentId))
            {
                studentUser = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == enrollment.StudentId);
                student = await _db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == enrollment.StudentId);
            }

            string? teacherFullName = null;
            var teacherIdProp = enrollment.OnlineCourse?.GetType().GetProperty("TeacherId");
            if (teacherIdProp != null)
            {
                var teacherId = teacherIdProp.GetValue(enrollment.OnlineCourse) as string;
                if (!string.IsNullOrEmpty(teacherId))
                {
                    var teacherUser = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == teacherId);
                    teacherFullName = teacherUser?.FullName;
                }
            }
            if (string.IsNullOrEmpty(teacherFullName) && !string.IsNullOrEmpty(enrollment.OnlineCourse?.TeacherName))
                teacherFullName = enrollment.OnlineCourse!.TeacherName;

            var monthsVm = months.Select(m => new SchoolEnrollmentMonthVm
            {
                MonthId = m.Id,
                MonthIndex = m.MonthIndex,
                LessonsCount = lessonCounts.TryGetValue(m.Id, out var c) ? c : 0,
                Payments = payments
                    .Where(p => p.OnlineCourseMonthId == m.Id)
                    .OrderByDescending(p => p.CreatedAtUtc)
                    .Select(p => new SchoolEnrollmentPaymentVm
                    {
                        PaymentId = p.Id,
                        MonthId = p.OnlineCourseMonthId,
                        Amount = p.Amount,
                        AmountLabel = p.Amount.ToEuro(),
                        Status = p.Status,
                        CreatedAtUtc = p.CreatedAtUtc,
                        PaidAtUtc = p.PaidAtUtc,
                        AdminNote = p.AdminNote,
                        StudentId = enrollment.StudentId
                    }).ToList()
            }).ToList();

            string? studentPhotoUrl = null;
            if (studentUser != null && !string.IsNullOrEmpty(studentUser.PhotoStorageKey))
            {
                try { studentPhotoUrl = await _fileStorage.GetPublicUrlAsync(studentUser.PhotoStorageKey); }
                catch { studentPhotoUrl = studentUser.PhotoUrl ?? studentUser.PhotoStorageKey; }
            }
            else studentPhotoUrl = studentUser?.PhotoUrl;

            var vm = new SchoolEnrollmentDetailsVm
            {
                EnrollmentId = enrollment.Id,
                CourseId = enrollment.OnlineCourseId,
                CourseTitle = enrollment.OnlineCourse?.Title ?? string.Empty,
                StudentId = enrollment.StudentId,
                StudentName = studentUser?.FullName ?? enrollment.StudentId,
                StudentEmail = studentUser?.Email,
                StudentPhone = studentUser?.PhoneNumber,
                GuardianPhoneNumber = student?.GuardianPhoneNumber,
                PhotoStorageKey = studentUser?.PhotoStorageKey,
                StudentPhotoUrl = studentPhotoUrl,
                TeacherFullName = teacherFullName,
                CreatedAtUtc = enrollment.CreatedAtUtc,
                Months = monthsVm
            };

            ViewData["ActivePage"] = "SchoolEnrollments";
            return View(vm);
        }

        // POST: Admin/OnlineEnrollments/MarkPaymentPaid
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPaymentPaid([FromForm] int paymentId)
        {
            var p = await _db.OnlineEnrollmentMonthPayments
                .Include(x => x.OnlineEnrollment)
                .FirstOrDefaultAsync(x => x.Id == paymentId);

            if (p == null) return Json(new { success = false, message = _localizer["Admin.PaymentNotFound"].Value ?? "Payment not found" });

            // if your enum types are different, adapt here
            if (p.Status == OnlineEnrollmentMonthPaymentStatus.Paid)
                return Json(new { success = false, message = _localizer["Admin.PaymentAlreadyPaid"].Value ?? "Payment already marked as paid" });

            p.Status = OnlineEnrollmentMonthPaymentStatus.Paid;
            p.PaidAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // optional log table (if you have it)
            // _db.OnlineEnrollmentLogs.Add(...)
            // await _db.SaveChangesAsync();

            try
            {
                var studentId = p.OnlineEnrollment?.StudentId;
                if (!string.IsNullOrEmpty(studentId))
                {
                    var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == studentId);
                    if (user?.Email != null)
                    {
                        await NotifyFireAndForgetAsync(async () =>
                        {
                            await _notifier.SendLocalizedEmailAsync(
                                user,
                                "Notify.PaymentMarkedPaid.Subject",
                                "Notify.PaymentMarkedPaid.Body",
                                p.Amount.ToString("C"),
                                (object)(await TryGetMonthIndexLabelAsync(p.OnlineCourseMonthId) ?? p.OnlineCourseMonthId.ToString())
                            );
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Notify after MarkPaymentPaid failed for payment {PaymentId}", paymentId);
            }

            return Json(new { success = true, paymentId = paymentId, paidAt = p.PaidAtUtc });
        }

        // POST: Admin/OnlineEnrollments/RejectPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectPayment([FromForm] int paymentId, [FromForm] string? adminNote)
        {
            var p = await _db.OnlineEnrollmentMonthPayments
                .Include(x => x.OnlineEnrollment)
                .FirstOrDefaultAsync(x => x.Id == paymentId);

            if (p == null) return Json(new { success = false, message = _localizer["Admin.PaymentNotFound"].Value ?? "Payment not found" });

            if (p.Status == OnlineEnrollmentMonthPaymentStatus.Paid)
                return Json(new { success = false, message = _localizer["Admin.CannotRejectPaid"].Value ?? "Cannot reject a paid payment" });

            p.Status = OnlineEnrollmentMonthPaymentStatus.Rejected;
            p.AdminNote = adminNote;
            await _db.SaveChangesAsync();

            try
            {
                var studentId = p.OnlineEnrollment?.StudentId;
                if (!string.IsNullOrEmpty(studentId))
                {
                    var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == studentId);
                    if (user?.Email != null)
                    {
                        await NotifyFireAndForgetAsync(async () =>
                        {
                            await _notifier.SendLocalizedEmailAsync(
                                user,
                                "Notify.PaymentRejected.Subject",
                                "Notify.PaymentRejected.Body",
                                p.Amount.ToString("C"),
                                adminNote ?? string.Empty
                            );
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Notify after RejectPayment failed for payment {PaymentId}", paymentId);
            }

            return Json(new { success = true, paymentId = paymentId });
        }

        // POST: Admin/OnlineEnrollments/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete([FromForm] int enrollmentId)
        {
            var enr = await _db.OnlineEnrollments
                .Include(e => e.MonthPayments)
                .FirstOrDefaultAsync(e => e.Id == enrollmentId);

            if (enr == null) return Json(new { success = false, message = _localizer["Admin.NotFound"].Value ?? "Not found" });

            try
            {
                // Optional: notify student
                try
                {
                    var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == enr.StudentId);
                    if (user != null && !string.IsNullOrEmpty(user.Email))
                    {
                        await NotifyFireAndForgetAsync(async () =>
                        {
                            await _notifier.SendLocalizedEmailAsync(
                                user,
                                "Notify.EnrollmentDeleted.Subject",
                                "Notify.EnrollmentDeleted.Body",
                                enr.OnlineCourse?.Title ?? ""
                            );
                        });
                    }
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Notification when deleting enrollment failed"); }

                _db.OnlineEnrollments.Remove(enr);
                await _db.SaveChangesAsync();
                return Json(new { success = true, message = _localizer["Admin.DeleteSuccess"].Value ?? "Deleted" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delete enrollment {EnrollmentId} failed", enrollmentId);
                return StatusCode(500, new { success = false, message = _localizer["Admin.DeleteError"].Value ?? "Error deleting" });
            }
        }

        // POST: Admin/OnlineEnrollments/DeleteOld
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOld([FromForm] DateTime olderThanUtc)
        {
            try
            {
                var toDelete = await _db.OnlineEnrollments
                    .Where(e => e.CreatedAtUtc < olderThanUtc)
                    .Where(e => !e.MonthPayments.Any(p => p.Status == OnlineEnrollmentMonthPaymentStatus.Paid))
                    .ToListAsync();

                if (!toDelete.Any())
                    return Json(new { success = true, deleted = 0, message = _localizer["Admin.DeleteOldNoMatching"].Value ?? "No matching enrollments" });

                var count = toDelete.Count;
                _db.OnlineEnrollments.RemoveRange(toDelete);
                await _db.SaveChangesAsync();

                return Json(new { success = true, deleted = count, message = _localizer["Admin.DeleteOldSuccess"].Value ?? "Deleted old enrollments" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteOld failed olderThan={OlderThan}", olderThanUtc);
                return StatusCode(500, new { success = false, message = _localizer["Admin.DeleteError"].Value ?? "Error" });
            }
        }

        // try to get MonthIndex string for a given OnlineCourseMonthId
        private async Task<string?> TryGetMonthIndexLabelAsync(int onlineCourseMonthId)
        {
            try
            {
                var month = await _db.OnlineCourseMonths.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == onlineCourseMonthId);
                if (month != null) return month.MonthIndex.ToString();
            }
            catch
            {
                // ignore
            }
            return null;
        }

        // run notifications awaited in Development or fire-and-forget in other envs
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
    }
}

