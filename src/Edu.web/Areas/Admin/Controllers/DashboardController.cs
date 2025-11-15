using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Web.Areas.Admin.ViewModels;
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
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IFileStorageService _fileStorage;

        public DashboardController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IStringLocalizer<SharedResource> localizer,
            IFileStorageService fileStorage)
        {
            _db = db;
            _userManager = userManager;
            _localizer = localizer;
            _fileStorage = fileStorage;
        }

        // GET: Admin/Dashboard
        public async Task<IActionResult> Index()
        {
            // summary counts
            var totalUsers = await _db.Users.CountAsync();
            var totalTeachers = await _db.Teachers.CountAsync();
            var totalStudents = await _db.Students.CountAsync();
            var pendingTeacherApplications = await _db.Teachers.CountAsync(t => t.Status == TeacherStatus.Pending);

            var totalPrivateCourses = await _db.PrivateCourses.CountAsync();
            var totalCurricula = await _db.Curricula.CountAsync();

            var totalReactiveCourses = await _db.ReactiveCourses.CountAsync();
            var pendingPurchaseRequests = await _db.PurchaseRequests.CountAsync(p => p.Status == PurchaseStatus.Pending);
            var pendingReactiveEnrollments = await _db.ReactiveEnrollments.CountAsync(e => !e.IsApproved);
            var pendingBookings = await _db.Bookings.CountAsync(b => b.Status == BookingStatus.Pending);

            // recent items (take most recent 6)
            var recentBookings = await _db.Bookings
                .AsNoTracking()
                .Include(b => b.Student).ThenInclude(s => s.User)
                .Include(b => b.Teacher).ThenInclude(t => t.User)
                .OrderByDescending(b => b.RequestedDateUtc)
                .Take(6)
                .ToListAsync();

            var recentPurchaseRequests = await _db.PurchaseRequests
                .AsNoTracking()
                .Include(p => p.PrivateCourse).ThenInclude(c => c.Teacher).ThenInclude(t => t.User)
                .Include(p => p.Student).ThenInclude(s => s.User)
                .OrderByDescending(p => p.RequestDateUtc)
                .Take(6)
                .ToListAsync();

            var recentReactiveEnrollments = await _db.ReactiveEnrollments
                .AsNoTracking()
                .Include(e => e.ReactiveCourse).ThenInclude(rc => rc.Teacher).ThenInclude(t => t.User)
                .Include(e => e.Student).ThenInclude(s => s.User)
                .OrderByDescending(e => e.CreatedAtUtc)
                .Take(6)
                .ToListAsync();

            // recent teacher applications (last 6)
            var recentTeachers = await _db.Teachers
                .AsNoTracking()
                .Include(t => t.User)
                .OrderByDescending(t => t.Id) // assumes Id roughly correlates to creation time; if you have createdAt use it
                .Take(6)
                .ToListAsync();

            // Map to VM
            var vm = new AdminDashboardVm
            {
                TotalUsers = totalUsers,
                TotalTeachers = totalTeachers,
                TotalStudents = totalStudents,
                PendingTeacherApplications = pendingTeacherApplications,
                TotalPrivateCourses = totalPrivateCourses,
                TotalCurricula = totalCurricula,
                TotalReactiveCourses = totalReactiveCourses,
                PendingPurchaseRequests = pendingPurchaseRequests,
                PendingReactiveEnrollments = pendingReactiveEnrollments,
                PendingBookings = pendingBookings
            };

            vm.RecentBookings = recentBookings.Select(b => new BookingSummaryVm
            {
                Id = b.Id,
                RequestedDateUtc = b.RequestedDateUtc,
                StudentName = b.Student?.User?.FullName ?? b.Student?.User?.UserName,
                TeacherName = b.Teacher?.User?.FullName ?? b.Teacher?.User?.UserName,
                Status = b.Status.ToString(),
                MeetUrl = b.MeetUrl
            }).ToList();

            vm.RecentPurchaseRequests = recentPurchaseRequests.Select(p => new PurchaseRequestSummaryVm
            {
                Id = p.Id,
                PrivateCourseId = p.PrivateCourseId,
                CourseTitle = p.PrivateCourse?.Title,
                StudentName = p.Student?.User?.FullName ?? p.Student?.User?.UserName,
                TeacherName = p.PrivateCourse?.Teacher?.User?.FullName,
                RequestDateUtc = p.RequestDateUtc,
                Status = p.Status.ToString(),
                Amount = p.Amount
            }).ToList();

            vm.RecentReactiveEnrollments = recentReactiveEnrollments.Select(e => new ReactiveEnrollmentSummaryVm
            {
                Id = e.Id,
                ReactiveCourseId = e.ReactiveCourseId,
                CourseTitle = e.ReactiveCourse?.Title,
                StudentName = e.Student?.User?.FullName ?? e.Student?.User?.UserName,
                CreatedAtUtc = e.CreatedAtUtc,
                IsApproved = e.IsApproved,
                IsPaid = e.IsPaid
            }).ToList();

            vm.RecentTeacherApplications = recentTeachers.Select(t => new TeacherSummaryVm
            {
                Id = t.Id,
                FullName = t.User?.FullName ?? t.Id,
                Email = t.User?.Email,
                Status = t.Status.ToString()
            }).ToList();

            ViewData["ActivePage"] = "AdminDashboard";
            return View(vm);
        }
    }
}

