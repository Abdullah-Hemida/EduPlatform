using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Web.Areas.Teacher.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
            if (teacherId == null) return Unauthorized();

            // query ensures teacher owns the course (single DB round-trip)
            var enrollment = await _db.ReactiveEnrollments
                .AsNoTracking()
                .Where(e => e.Id == id && e.ReactiveCourse.TeacherId == teacherId)
                .Select(e => new
                {
                    e.Id,
                    e.ReactiveCourseId,
                    CourseTitle = e.ReactiveCourse!.Title,
                    CourseCoverKey = e.ReactiveCourse!.CoverImageKey,
                    StudentId = e.StudentId,
                    StudentName = e.Student!.User!.FullName ?? e.Student!.User!.UserName,
                    StudentEmail = e.Student!.User!.Email,
                    StudentPhoneNumber = e.Student!.User!.PhoneNumber,
                    GuardianPhoneNumber = e.Student.GuardianPhoneNumber,
                    e.CreatedAtUtc,
                    e.IsApproved,
                    e.IsPaid,
                    MonthPayments = e.MonthPayments.Select(mp => new
                    {
                        mp.Id,
                        mp.ReactiveCourseMonthId,
                        MonthIndex = mp.ReactiveCourseMonth != null ? mp.ReactiveCourseMonth.MonthIndex : 0,
                        mp.Amount,
                        mp.Status,
                        mp.AdminNote,
                        mp.PaymentReference,
                        mp.CreatedAtUtc,
                        mp.PaidAtUtc
                    }).ToList()
                })
                .FirstOrDefaultAsync();

            if (enrollment == null) return NotFound(); // either not found or not belonging to this teacher

            // resolve cover URL (if exists)
            string? coverUrl = null;
            if (!string.IsNullOrWhiteSpace(enrollment.CourseCoverKey))
            {
                try
                {
                    coverUrl = await _fileStorage.GetPublicUrlAsync(enrollment.CourseCoverKey);
                }
                catch
                {
                    coverUrl = null;
                }
            }

            var payments = enrollment.MonthPayments
                .OrderBy(mp => mp.MonthIndex)
                .Select(mp => new MonthPaymentVm
                {
                    Id = mp.Id,
                    ReactiveCourseMonthId = mp.ReactiveCourseMonthId,
                    MonthIndex = mp.MonthIndex,
                    Amount = mp.Amount,
                    AmountLabel = mp.Amount.ToEuro(),
                    Status = mp.Status,
                    AdminNote = mp.AdminNote,
                    PaymentReference = mp.PaymentReference,
                    CreatedAtUtc = mp.CreatedAtUtc,
                    PaidAtUtc = mp.PaidAtUtc
                }).ToList();

            var vm = new ReactiveEnrollmentDetailsVm
            {
                Id = enrollment.Id,
                ReactiveCourseId = enrollment.ReactiveCourseId,
                CourseTitle = enrollment.CourseTitle,
                CourseCoverUrl = coverUrl,
                StudentId = enrollment.StudentId,
                StudentName = enrollment.StudentName,
                StudentEmail = enrollment.StudentEmail,
                StudentPhoneNumber = enrollment.StudentPhoneNumber,
                GuardianPhoneNumber = enrollment.GuardianPhoneNumber,
                CreatedAtUtc = enrollment.CreatedAtUtc,
                IsApproved = enrollment.IsApproved,
                IsPaid = enrollment.IsPaid,
                MonthPayments = payments,
                TotalPaid = payments.Where(p => p.Status == EnrollmentMonthPaymentStatus.Paid).Sum(p => p.Amount)
            };

            vm.TotalPaidLabel = vm.TotalPaid.ToEuro();

            ViewData["ActivePage"] = "ReactiveEnrollments";
            return View(vm);
        }
    }
}


