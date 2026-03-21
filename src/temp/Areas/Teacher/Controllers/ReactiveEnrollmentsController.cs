using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Web.Areas.Admin.ViewModels;
using Edu.Web.Areas.Teacher.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;

namespace Edu.Web.Areas.Teacher.Controllers
{
    [Area("Teacher")]
    [Authorize(Roles = "Teacher")]
    public class ReactiveEnrollmentsController : TeacherBaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fileStorage;
        private const string DefaultAvatar = "~/uploads/images/default-avatar.jpg";

        public ReactiveEnrollmentsController(
            ApplicationDbContext db,
            IFileStorageService fileStorage)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _fileStorage = fileStorage ?? throw new ArgumentNullException(nameof(fileStorage));
        }

        // GET: Teacher/ReactiveEnrollments
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        {
            var teacherId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (teacherId == null) return Unauthorized();

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            cancellationToken = cancellationToken == default ? HttpContext.RequestAborted : cancellationToken;

            // Count separately (lightweight scalar)
            var totalCount = await _db.ReactiveEnrollments
                .AsNoTracking()
                .Where(e => e.ReactiveCourse!.TeacherId == teacherId)
                .CountAsync(cancellationToken);

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

            // Project only EF-translatable fields
            var query = _db.ReactiveEnrollments
                .AsNoTracking()
                .Where(e => e.ReactiveCourse!.TeacherId == teacherId)
                .OrderByDescending(e => e.CreatedAtUtc)
                .Select(e => new
                {
                    e.Id,
                    StudentFullName = e.Student!.User!.FullName,
                    StudentUserName = e.Student!.User!.UserName,
                    StudentPhone = e.Student!.User!.PhoneNumber,
                    StudentImageKey = e.Student!.User!.PhotoStorageKey,
                    CourseTitle = e.ReactiveCourse!.Title,
                    CreatedAtUtc = e.CreatedAtUtc,
                    PaidMonths = e.MonthPayments.Count(m => m.Status == EnrollmentMonthPaymentStatus.Paid),
                    TotalMonths = e.MonthPayments.Count()
                });

            var pageItems = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            // Map to view model and compute derived fields (outside EF)
            var paged = pageItems.Select(i =>
            {
                var studentName = !string.IsNullOrWhiteSpace(i.StudentFullName)
                    ? i.StudentFullName
                    : (!string.IsNullOrWhiteSpace(i.StudentUserName) ? i.StudentUserName : string.Empty);

                var paymentStatus = i.PaidMonths == 0
                    ? "Pending"
                    : (i.PaidMonths == i.TotalMonths ? "Paid" : "Partial");

                return new TeacherEnrollmentViewModel
                {
                    Id = i.Id,
                    StudentName = studentName,
                    StudentPhone = i.StudentPhone,
                    PhoneWhatsapp = PhoneHelpers.ToWhatsappDigits(i.StudentPhone, defaultRegion),
                    StudentImageKey = i.StudentImageKey,
                    CourseTitle = i.CourseTitle ?? string.Empty,
                    CreatedAtUtc = i.CreatedAtUtc,
                    PaymentStatus = paymentStatus,
                    PaidMonths = i.PaidMonths,
                    TotalMonths = i.TotalMonths
                };
            }).ToList();

            // Resolve image keys -> urls in parallel (only unique keys)
            var imageKeys = paged
                .Where(x => !string.IsNullOrWhiteSpace(x.StudentImageKey))
                .Select(x => x.StudentImageKey!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var imageUrlMap = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            if (imageKeys.Count > 0)
            {
                try
                {
                    var urlTasks = imageKeys.Select(k => _fileStorage.GetPublicUrlAsync(k, TimeSpan.FromHours(1))).ToArray();
                    var urls = await Task.WhenAll(urlTasks);
                    imageUrlMap = imageKeys.Zip(urls, (k, u) => (k, u)).ToDictionary(x => x.k, x => x.u, StringComparer.OrdinalIgnoreCase);
                }
                catch
                {
                    // if any storage lookup fails, we'll fall back to default avatar for those keys
                }
            }

            // assign resolved urls or fallback
            var fallbackUrl = Url.Content(DefaultAvatar);
            foreach (var item in paged)
            {
                if (!string.IsNullOrWhiteSpace(item.StudentImageKey)
                    && imageUrlMap.TryGetValue(item.StudentImageKey!, out var url)
                    && !string.IsNullOrWhiteSpace(url))
                {
                    item.StudentImageUrl = url;
                }
                else
                {
                    item.StudentImageUrl = fallbackUrl;
                }
            }

            // Pagination metadata
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            ViewData["ActivePage"] = "ReactiveEnrollments";
            return View(paged);
        }

        // GET: Teacher/ReactiveEnrollments/Details/5
        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken = default)
        {
            var teacherId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(teacherId)) return Unauthorized();

            cancellationToken = cancellationToken == default ? HttpContext.RequestAborted : cancellationToken;

            // 1) load enrollment and verify the course belongs to this teacher (single DB op)
            var enrollment = await _db.ReactiveEnrollments
                .AsNoTracking()
                .Where(e => e.Id == id && e.ReactiveCourse != null && e.ReactiveCourse.TeacherId == teacherId)
                .Select(e => new
                {
                    e.Id,
                    e.ReactiveCourseId,
                    CourseTitle = e.ReactiveCourse!.Title,
                    CourseCoverKey = e.ReactiveCourse!.CoverImageKey,
                    StudentId = e.StudentId,
                    e.CreatedAtUtc,
                    e.IsApproved,
                    e.IsPaid
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (enrollment == null) return NotFound();

            // 2) load payments for this enrollment
            var paymentsTask = _db.ReactiveEnrollmentMonthPayments
                .AsNoTracking()
                .Where(p => p.ReactiveEnrollmentId == id)
                .ToListAsync(cancellationToken);

            // 3) load months for course
            var monthsTask = _db.ReactiveCourseMonths
                .AsNoTracking()
                .Where(m => m.ReactiveCourseId == enrollment.ReactiveCourseId)
                .OrderBy(m => m.MonthIndex)
                .Select(m => new { m.Id, m.MonthIndex })
                .ToListAsync(cancellationToken);

            await Task.WhenAll(paymentsTask, monthsTask);

            var payments = paymentsTask.Result;
            var months = monthsTask.Result;
            var monthIds = months.Select(m => m.Id).ToList();

            // 4) compute lesson counts grouped by month id
            Dictionary<int, int> lessonCounts = new();
            if (monthIds.Any())
            {
                var grouped = await _db.ReactiveCourseLessons
                    .AsNoTracking()
                    .Where(l => monthIds.Contains(l.ReactiveCourseMonthId))
                    .GroupBy(l => l.ReactiveCourseMonthId)
                    .Select(g => new { MonthId = g.Key, Count = g.Count() })
                    .ToListAsync(cancellationToken);

                lessonCounts = grouped.ToDictionary(x => x.MonthId, x => x.Count);
            }

            // 5) load student user and student entity in parallel
            ApplicationUser? studentUser = null;
            Domain.Entities.Student? student = null;
            if (!string.IsNullOrEmpty(enrollment.StudentId))
            {
                var userTask = _db.Users
                    .AsNoTracking()
                    .Where(u => u.Id == enrollment.StudentId)
                    .Select(u => new { u.Id, u.FullName, u.Email, u.PhoneNumber, u.PhotoStorageKey, u.PhotoUrl })
                    .FirstOrDefaultAsync(cancellationToken);

                var studentTask = _db.Students
                    .AsNoTracking()
                    .Where(s => s.Id == enrollment.StudentId)
                    .Select(s => new { s.Id, s.GuardianPhoneNumber })
                    .FirstOrDefaultAsync(cancellationToken);

                await Task.WhenAll(userTask, studentTask);

                var userRes = userTask.Result;
                var studentRes = studentTask.Result;

                if (userRes != null)
                {
                    studentUser = new ApplicationUser
                    {
                        Id = userRes.Id,
                        FullName = userRes.FullName ?? string.Empty,
                        Email = userRes.Email,
                        PhoneNumber = userRes.PhoneNumber,
                        PhotoStorageKey = userRes.PhotoStorageKey,
                        PhotoUrl = userRes.PhotoUrl
                    };
                }

                if (studentRes != null)
                {
                    student = new Domain.Entities.Student
                    {
                        Id = studentRes.Id,
                        GuardianPhoneNumber = studentRes.GuardianPhoneNumber ?? string.Empty
                    };
                }
            }

            // 6) resolve student photo URL and course cover URL (parallel)
            string? studentPhotoUrl = null;
            string? courseCoverUrl = null;
            var tasks = new List<Task>();
            if (studentUser != null && !string.IsNullOrEmpty(studentUser.PhotoStorageKey))
            {
                tasks.Add(Task.Run(async () =>
                {
                    try { studentPhotoUrl = await _fileStorage.GetPublicUrlAsync(studentUser.PhotoStorageKey); }
                    catch { studentPhotoUrl = studentUser.PhotoUrl ?? studentUser.PhotoStorageKey; }
                }));
            }

            if (!string.IsNullOrEmpty(enrollment.CourseCoverKey))
            {
                tasks.Add(Task.Run(async () =>
                {
                    try { courseCoverUrl = await _fileStorage.GetPublicUrlAsync(enrollment.CourseCoverKey); }
                    catch { courseCoverUrl = null; }
                }));
            }

            if (tasks.Count > 0) await Task.WhenAll(tasks);

            // compute default region for PhoneHelpers conversion (fallback IT)
            string defaultRegion = "IT";
            try
            {
                var regionInfo = new RegionInfo(CultureInfo.CurrentUICulture.Name);
                if (!string.IsNullOrEmpty(regionInfo.TwoLetterISORegionName))
                    defaultRegion = regionInfo.TwoLetterISORegionName;
            }
            catch { defaultRegion = "IT"; }

            // 7) build months view models (group payments by month)
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
                        Amount = p.Amount,
                        Status = p.Status,
                        CreatedAtUtc = p.CreatedAtUtc,
                        PaidAtUtc = p.PaidAtUtc,
                        AdminNote = p.AdminNote,
                        StudentId = enrollment.StudentId
                    }).ToList()
            }).ToList();

            // 8) compute whatsapp digits
            var phone = studentUser?.PhoneNumber;
            var guardianPhone = student?.GuardianPhoneNumber;
            var phoneWhatsapp = PhoneHelpers.ToWhatsappDigits(phone, defaultRegion);
            var guardianWhatsapp = PhoneHelpers.ToWhatsappDigits(guardianPhone, defaultRegion);

            // 9) build final VM (reuse Admin vm type so view markup can be similar)
            var vm = new AdminEnrollmentDetailsVm
            {
                EnrollmentId = enrollment.Id,
                CourseId = enrollment.ReactiveCourseId,
                CourseTitle = enrollment.CourseTitle,
                StudentId = enrollment.StudentId,
                StudentName = studentUser?.FullName ?? enrollment.StudentId,
                StudentEmail = studentUser?.Email,
                StudentPhone = studentUser?.PhoneNumber,
                PhoneWhatsapp = phoneWhatsapp,
                GuardianPhoneNumber = student?.GuardianPhoneNumber,
                GuardianWhatsapp = guardianWhatsapp,
                PhotoStorageKey = studentUser?.PhotoStorageKey,
                StudentPhotoUrl = studentPhotoUrl ?? studentUser?.PhotoUrl,
                TeacherFullName = User.Identity?.Name, // current teacher's display name
                CreatedAtUtc = enrollment.CreatedAtUtc,
                Months = monthsVm,
                CourseCoverUrl = courseCoverUrl
            };

            ViewData["ActivePage"] = "ReactiveEnrollments";
            return View(vm);
        }
    }
}



