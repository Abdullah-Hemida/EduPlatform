
using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Infrastructure.Services;
using Edu.Web.Areas.Admin.ViewModels;
using Edu.Web.Helpers;
using Edu.Web.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.Globalization;

namespace Edu.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ReactiveEnrollmentsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ReactiveEnrollmentsController> _logger;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IUserCultureProvider _userCultureProvider;
        private readonly INotificationService _notifier;
        private readonly IWebHostEnvironment _env;
        private readonly IFileStorageService _fileStorage;

        public ReactiveEnrollmentsController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ILogger<ReactiveEnrollmentsController> logger,
            IStringLocalizer<SharedResource> localizer,
            IUserCultureProvider userCultureProvider,
            INotificationService notifier,
            IWebHostEnvironment env,
            IFileStorageService fileStorage)
        {
            _db = db;
            _userManager = userManager;
            _logger = logger;
            _localizer = localizer;
            _userCultureProvider = userCultureProvider;
            _notifier = notifier;
            _env = env;
            _fileStorage = fileStorage;
        }

        // GET: Admin/ReactiveEnrollments
        // supports ?q=search&page=1&pageSize=20
        [HttpGet]
        public async Task<IActionResult> Index(string? q, int page = 1, int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize <= 0) pageSize = 20;

            var baseQuery = _db.ReactiveEnrollments
                .AsNoTracking()
                .Include(e => e.ReactiveCourse)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                baseQuery = baseQuery.Where(e =>
                    EF.Functions.Like(e.ReactiveCourse.Title, $"%{q}%") ||
                    (e.StudentId != null && _db.Users.Any(u => u.Id == e.StudentId && (EF.Functions.Like(u.FullName, $"%{q}%") || EF.Functions.Like(u.Email, $"%{q}%")))) ||
                    (e.ReactiveCourse.TeacherId != null && _db.Users.Any(t => t.Id == e.ReactiveCourse.TeacherId && EF.Functions.Like(t.FullName, $"%{q}%")))
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

            var studentIds = items.Select(i => i.StudentId).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
            var teacherIds = items.Select(i => i.TeacherId).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();

            var users = await _db.Users
                .AsNoTracking()
                .Where(u => studentIds.Contains(u.Id) || teacherIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FullName, u.Email, u.PhoneNumber, u.PhotoStorageKey, u.PhotoUrl })
                .ToListAsync();

            var students = await _db.Students
                .AsNoTracking()
                .Where(s => studentIds.Contains(s.Id))
                .Select(s => new { s.Id, s.GuardianPhoneNumber })
                .ToListAsync();
            // compute default region from admin UI culture (fallback to IT)
            string defaultRegion = "IT";
            try
            {
                var regionInfo = new RegionInfo(CultureInfo.CurrentUICulture.Name);
                if (!string.IsNullOrEmpty(regionInfo.TwoLetterISORegionName))
                    defaultRegion = regionInfo.TwoLetterISORegionName;
            }
            catch
            {
                defaultRegion = "IT";
            }
            // Build DTOs first, collect keys
            var list = items.Select(i =>
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
                    PhoneWhatsapp = PhoneHelpers.ToWhatsappDigits(stu?.PhoneNumber, defaultRegion),
                    GuardianPhoneNumber = students.FirstOrDefault(s => s.Id == i.StudentId)?.GuardianPhoneNumber,
                    PhotoStorageKey = stu?.PhotoStorageKey,
                    PhotoFileUrlFallback = stu?.PhotoUrl,
                    TeacherFullName = tch?.FullName,
                    CreatedAtUtc = i.CreatedAtUtc,
                    PaidMonthsCount = i.PaidCount,
                    PendingMonthsCount = i.PendingCount
                };
            }).ToList();

            // Resolve unique keys
            var keys = list.Select(x => x.PhotoStorageKey).Where(k => !string.IsNullOrEmpty(k)).Distinct().ToList();
            var keyToUrl = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            if (keys.Any())
            {
                var tasks = keys.Select(async key =>
                {
                    try
                    {
                        var url = await _fileStorage.GetPublicUrlAsync(key!);
                        keyToUrl[key!] = url;
                    }
                    catch
                    {
                        keyToUrl[key!] = null;
                    }
                });
                await Task.WhenAll(tasks);
            }

            // Apply resolved urls
            foreach (var it in list)
            {
                if (!string.IsNullOrEmpty(it.PhotoStorageKey) && keyToUrl.TryGetValue(it.PhotoStorageKey!, out var publicUrl))
                {
                    it.StudentPhotoUrl = publicUrl ?? it.PhotoFileUrlFallback ?? it.PhotoStorageKey;
                }
                else
                {
                    it.StudentPhotoUrl = it.PhotoFileUrlFallback;
                }
            }

            var vm = new AdminEnrollmentIndexVm
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                Query = q ?? string.Empty,
                Items = list
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

            if (enrollment == null)
            {
                _logger.LogWarning("ReactiveEnrollment {Id} not found", id);
                return NotFound();
            }

            // 1) load payments
            var payments = await _db.ReactiveEnrollmentMonthPayments
                .AsNoTracking()
                .Where(p => p.ReactiveEnrollmentId == id)
                .ToListAsync();
            // 2) load months (id + index)
            var months = await _db.ReactiveCourseMonths
                .AsNoTracking()
                .Where(m => m.ReactiveCourseId == enrollment.ReactiveCourseId)
                .OrderBy(m => m.MonthIndex)
                .Select(m => new { m.Id, m.MonthIndex })
                .ToListAsync();

            var monthIds = months.Select(m => m.Id).ToList();
            // 3) compute lesson counts grouped by month id
            Dictionary<int, int> lessonCounts = new();
            if (monthIds.Any())
            {
                var grouped = await _db.ReactiveCourseLessons
                    .AsNoTracking()
                    .Where(l => monthIds.Contains(l.ReactiveCourseMonthId))
                    .GroupBy(l => l.ReactiveCourseMonthId)
                    .Select(g => new { MonthId = g.Key, Count = g.Count() })
                    .ToListAsync();

                lessonCounts = grouped.ToDictionary(x => x.MonthId, x => x.Count);
            }
            else
            {
                _logger.LogDebug("No months found for course {CourseId}", enrollment.ReactiveCourseId);
            }

            // 4) load student/user records
            ApplicationUser? studentUser = null;
            Domain.Entities.Student? student = null;
            if (!string.IsNullOrEmpty(enrollment.StudentId))
            {
                studentUser = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == enrollment.StudentId);
                student = await _db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == enrollment.StudentId);
            }

            // 5) teacher
            ApplicationUser? teacherUser = null;
            if (!string.IsNullOrEmpty(enrollment.ReactiveCourse?.TeacherId))
            {
                teacherUser = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == enrollment.ReactiveCourse.TeacherId);
            }

            // 6) build month VMs (explicitly set LessonsCount from the grouped dictionary)
            var monthsVm = months.Select(m => new AdminEnrollmentMonthVm
            {
                MonthId = m.Id,
                MonthIndex = m.MonthIndex,
                LessonsCount = lessonCounts.TryGetValue(m.Id, out var c) ? c : 0,
                Payments = payments
                    .Where(p => p.ReactiveCourseMonthId == m.Id)
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

            // 8) resolve student photo URL (best-effort)
            string? studentPhotoUrl = null;
            if (studentUser != null && !string.IsNullOrEmpty(studentUser.PhotoStorageKey))
            {
                try { studentPhotoUrl = await _fileStorage.GetPublicUrlAsync(studentUser.PhotoStorageKey); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed resolving student photo storage key"); studentPhotoUrl = studentUser.PhotoUrl ?? studentUser.PhotoStorageKey; }
            }
            else studentPhotoUrl = studentUser?.PhotoUrl;
            // compute default region from admin UI culture (fallback to IT)
            string defaultRegion = "IT";
            try
            {
                var regionInfo = new RegionInfo(CultureInfo.CurrentUICulture.Name);
                if (!string.IsNullOrEmpty(regionInfo.TwoLetterISORegionName))
                    defaultRegion = regionInfo.TwoLetterISORegionName;
            }
            catch
            {
                defaultRegion = "IT";
            }
            // 9) build VM
            var vm = new AdminEnrollmentDetailsVm
            {
                EnrollmentId = enrollment.Id,
                CourseId = enrollment.ReactiveCourseId,
                CourseTitle = enrollment.ReactiveCourse?.Title ?? string.Empty,
                StudentId = enrollment.StudentId,
                StudentName = studentUser?.FullName ?? enrollment.StudentId,
                StudentEmail = studentUser?.Email,
                StudentPhone = studentUser?.PhoneNumber,
                PhoneWhatsapp = PhoneHelpers.ToWhatsappDigits(studentUser?.PhoneNumber, defaultRegion),
                GuardianPhoneNumber = student?.GuardianPhoneNumber,
                GuardianWhatsapp = PhoneHelpers.ToWhatsappDigits(student?.GuardianPhoneNumber, defaultRegion),
                PhotoStorageKey = studentUser?.PhotoStorageKey,
                StudentPhotoUrl = studentPhotoUrl,
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

            try
            {
                var studentId = p.ReactiveEnrollment.StudentId;
                if (!string.IsNullOrEmpty(studentId))
                {
                    var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == studentId);
                    if (user?.Email != null)
                    {
                        // placeholders: {0}=amount, {1}=monthIndex
                        await NotifyFireAndForgetAsync(async () =>
                            await _notifier.SendLocalizedEmailAsync(
                                user,
                                "Notify.PaymentMarkedPaid.Subject",
                                "Notify.PaymentMarkedPaid.Body",
                                p.Amount.ToString("C"),
                                (object)(await TryGetMonthIndexLabelAsync(p.ReactiveCourseMonthId) ?? p.ReactiveCourseMonthId.ToString())
                            )
                        );
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

            try
            {
                var studentId = p.ReactiveEnrollment.StudentId;
                if (!string.IsNullOrEmpty(studentId))
                {
                    var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == studentId);
                    if (user?.Email != null)
                    {
                        // placeholders: {0}=amount, {1}=adminNote
                        await NotifyFireAndForgetAsync(async () =>
                            await _notifier.SendLocalizedEmailAsync(
                                user,
                                "Notify.PaymentRejected.Subject",
                                "Notify.PaymentRejected.Body",
                                p.Amount.ToString("C"),
                                adminNote ?? string.Empty
                            )
                        );
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
                // optionally notify student
                try
                {
                    var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == enr.StudentId);
                    if (user != null && !string.IsNullOrEmpty(user.Email))
                    {
                        await NotifyFireAndForgetAsync(async () =>
                            await _notifier.SendLocalizedEmailAsync(
                                user,
                                "Notify.EnrollmentDeleted.Subject",
                                "Notify.EnrollmentDeleted.Body",
                                enr.ReactiveCourse?.Title ?? ""
                            )
                        );
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

        // attempt to get MonthIndex for a given monthId (returns string or null)
        private async Task<string?> TryGetMonthIndexLabelAsync(int reactiveCourseMonthId)
        {
            try
            {
                var month = await _db.ReactiveCourseMonths.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == reactiveCourseMonthId);
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






