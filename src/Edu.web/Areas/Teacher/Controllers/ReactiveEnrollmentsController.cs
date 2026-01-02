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
            _db = db;
            _fileStorage = fileStorage;
        }

        // GET: Teacher/ReactiveEnrollments
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            var teacherId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (teacherId == null) return Unauthorized();

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            // Count separately (lightweight scalar)
            var totalCount = await _db.ReactiveEnrollments
                .AsNoTracking()
                .Where(e => e.ReactiveCourse.TeacherId == teacherId)
                .CountAsync();
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

            // Project only required columns (avoids loading whole entities / using Include)
            var query = _db.ReactiveEnrollments
                .AsNoTracking()
                .Where(e => e.ReactiveCourse.TeacherId == teacherId)
                .OrderByDescending(e => e.CreatedAtUtc)
                .Select(e => new TeacherEnrollmentViewModel
                {
                    // note: defensive null-coalescing to prevent client-side NREs if any path is null
                    Id = e.Id,
                    StudentName = e.Student!.User!.FullName ?? e.Student!.User!.UserName ?? string.Empty,
                    StudentPhone = e.Student!.User!.PhoneNumber,
                    PhoneWhatsapp = PhoneHelpers.ToWhatsappDigits(e.Student!.User!.PhoneNumber, defaultRegion),
                    StudentImageKey = e.Student!.User!.PhotoStorageKey,
                    CourseTitle = e.ReactiveCourse!.Title ?? string.Empty,
                    CreatedAtUtc = e.CreatedAtUtc,
                    PaymentStatus = e.MonthPayments.All(m => m.Status == EnrollmentMonthPaymentStatus.Paid)
                        ? "Paid"
                        : (e.MonthPayments.Any(m => m.Status == EnrollmentMonthPaymentStatus.Paid) ? "Partial" : "Pending"),
                    PaidMonths = e.MonthPayments.Count(m => m.Status == EnrollmentMonthPaymentStatus.Paid),
                    TotalMonths = e.MonthPayments.Count()
                });

            var paged = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Resolve image keys -> urls in parallel (only unique keys)
            var imageKeys = paged
                .Where(x => !string.IsNullOrWhiteSpace(x.StudentImageKey))
                .Select(x => x.StudentImageKey!)
                .Distinct()
                .ToList();

            var imageUrlMap = new Dictionary<string, string?>();
            if (imageKeys.Count > 0)
            {
                var tasks = imageKeys.Select(k => _fileStorage.GetPublicUrlAsync(k)).ToArray();
                var urls = await Task.WhenAll(tasks);
                imageUrlMap = imageKeys.Zip(urls, (k, u) => (k, u))
                                       .ToDictionary(x => x.k, x => x.u);
            }

            // assign resolved urls or fallback
            foreach (var item in paged)
            {
                if (!string.IsNullOrWhiteSpace(item.StudentImageKey) && imageUrlMap.TryGetValue(item.StudentImageKey!, out var url) && !string.IsNullOrWhiteSpace(url))
                    item.StudentImageUrl = url;
                else
                    item.StudentImageUrl = DefaultAvatar;
            }

            // Pagination metadata
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            ViewData["ActivePage"] = "ReactiveEnrollments";
            return View(paged);
        }

        // GET: Teacher/ReactiveEnrollments/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var teacherId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(teacherId)) return Unauthorized();

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
                    StudentIdFallback = e.StudentId, // fallback
                    StudentUserId = e.StudentId,
                    CreatedAtUtc = e.CreatedAtUtc,
                    e.IsApproved,
                    e.IsPaid
                })
                .FirstOrDefaultAsync();

            if (enrollment == null) return NotFound();

            // 2) load payments for this enrollment
            var payments = await _db.ReactiveEnrollmentMonthPayments
                .AsNoTracking()
                .Where(p => p.ReactiveEnrollmentId == id)
                .ToListAsync();

            // 3) load months for course
            var months = await _db.ReactiveCourseMonths
                .AsNoTracking()
                .Where(m => m.ReactiveCourseId == enrollment.ReactiveCourseId)
                .OrderBy(m => m.MonthIndex)
                .Select(m => new { m.Id, m.MonthIndex })
                .ToListAsync();

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
                    .ToListAsync();

                lessonCounts = grouped.ToDictionary(x => x.MonthId, x => x.Count);
            }

            // 5) load student user and student entity
            ApplicationUser? studentUser = null;
            Domain.Entities.Student? student = null;
            if (!string.IsNullOrEmpty(enrollment.StudentId))
            {
                studentUser = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == enrollment.StudentId);
                student = await _db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == enrollment.StudentId);
            }

            // 6) resolve student photo URL (best-effort)
            string? studentPhotoUrl = null;
            if (studentUser != null && !string.IsNullOrEmpty(studentUser.PhotoStorageKey))
            {
                try
                {
                    studentPhotoUrl = await _fileStorage.GetPublicUrlAsync(studentUser.PhotoStorageKey);
                }
                catch
                {
                    studentPhotoUrl = studentUser.PhotoUrl ?? studentUser.PhotoStorageKey;
                }
            }
            else studentPhotoUrl = studentUser?.PhotoUrl;

            // resolve course cover url (best-effort)
            string? courseCoverUrl = null;
            if (!string.IsNullOrEmpty(enrollment.CourseCoverKey))
            {
                try { courseCoverUrl = await _fileStorage.GetPublicUrlAsync(enrollment.CourseCoverKey); }
                catch { courseCoverUrl = null; }
            }

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
            })
            .ToList();

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
                StudentPhotoUrl = studentPhotoUrl,
                TeacherFullName = User.Identity?.Name, // current teacher's display name
                CreatedAtUtc = enrollment.CreatedAtUtc,
                Months = monthsVm
            };

            ViewData["ActivePage"] = "ReactiveEnrollments";

            return View(vm);
        }
    }
}


