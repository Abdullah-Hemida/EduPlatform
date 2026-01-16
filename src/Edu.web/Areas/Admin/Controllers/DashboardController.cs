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
using Microsoft.Extensions.Logging;

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
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IStringLocalizer<SharedResource> localizer,
            IFileStorageService fileStorage,
            ILogger<DashboardController> logger)
        {
            _db = db;
            _userManager = userManager;
            _localizer = localizer;
            _fileStorage = fileStorage;
            _logger = logger;
        }

        // GET: Admin/Dashboard
        public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
        {
            try
            {
                // summary counts (CountAsync is efficient; it doesn't track)
                var totalUsers = await _db.Users.CountAsync(cancellationToken);
                var totalTeachers = await _db.Teachers.CountAsync(cancellationToken);
                var totalStudents = await _db.Students.CountAsync(cancellationToken);
                var pendingTeacherApplications = await _db.Teachers.CountAsync(t => t.Status == TeacherStatus.Pending, cancellationToken);

                var totalPrivateCourses = await _db.PrivateCourses.CountAsync(cancellationToken);
                var totalCurricula = await _db.Curricula.CountAsync(cancellationToken);

                var totalReactiveCourses = await _db.ReactiveCourses.CountAsync(cancellationToken);
                var pendingPurchaseRequests = await _db.PurchaseRequests.CountAsync(p => p.Status == PurchaseStatus.Pending, cancellationToken);
                var pendingReactiveEnrollments = await _db.ReactiveEnrollments.CountAsync(e => !e.IsApproved, cancellationToken);
                var pendingBookings = await _db.Bookings.CountAsync(b => b.Status == BookingStatus.Pending, cancellationToken);

                // recent items — project directly to summary VMs to avoid materializing full entities
                var recentBookings = await _db.Bookings
                    .AsNoTracking()
                    .OrderByDescending(b => b.RequestedDateUtc)
                    .Take(6)
                    .Select(b => new BookingSummaryVm
                    {
                        Id = b.Id,
                        RequestedDateUtc = b.RequestedDateUtc,
                        StudentName = b.Student != null && b.Student.User != null ? (b.Student.User.FullName ?? b.Student.User.UserName) : null,
                        TeacherName = b.Teacher != null && b.Teacher.User != null ? (b.Teacher.User.FullName ?? b.Teacher.User.UserName) : null,
                        Status = b.Status.ToString(),
                        MeetUrl = b.MeetUrl
                    })
                    .ToListAsync(cancellationToken);

                var recentPurchaseRequests = await _db.PurchaseRequests
                    .AsNoTracking()
                    .OrderByDescending(p => p.RequestDateUtc)
                    .Take(6)
                    .Select(p => new PurchaseRequestSummaryVm
                    {
                        Id = p.Id,
                        PrivateCourseId = p.PrivateCourseId,
                        CourseTitle = p.PrivateCourse != null ? p.PrivateCourse.Title : null,
                        StudentName = p.Student != null && p.Student.User != null ? (p.Student.User.FullName ?? p.Student.User.UserName) : null,
                        TeacherName = p.PrivateCourse != null && p.PrivateCourse.Teacher != null && p.PrivateCourse.Teacher.User != null ? (p.PrivateCourse.Teacher.User.FullName ?? p.PrivateCourse.Teacher.User.UserName) : null,
                        RequestDateUtc = p.RequestDateUtc,
                        Status = p.Status.ToString(),
                        Amount = p.Amount
                    })
                    .ToListAsync(cancellationToken);

                var recentReactiveEnrollments = await _db.ReactiveEnrollments
                    .AsNoTracking()
                    .OrderByDescending(e => e.CreatedAtUtc)
                    .Take(6)
                    .Select(e => new ReactiveEnrollmentSummaryVm
                    {
                        Id = e.Id,
                        ReactiveCourseId = e.ReactiveCourseId,
                        CourseTitle = e.ReactiveCourse != null ? e.ReactiveCourse.Title : null,
                        StudentName = e.Student != null && e.Student.User != null ? (e.Student.User.FullName ?? e.Student.User.UserName) : null,
                        CreatedAtUtc = e.CreatedAtUtc,
                        IsApproved = e.IsApproved,
                        IsPaid = e.IsPaid
                    })
                    .ToListAsync(cancellationToken);

                // recent teacher applications (take last 6 by created/Id)
                var recentTeachers = await _db.Teachers
                    .AsNoTracking()
                    .OrderByDescending(t => t.Id) // replace with CreatedAt if you have it
                    .Take(6)
                    .Select(t => new TeacherSummaryVm
                    {
                        Id = t.Id,
                        FullName = t.User != null ? (t.User.FullName ?? t.User.UserName) : string.Empty,
                        Email = t.User != null ? t.User.Email : string.Empty,
                        Status = t.Status.ToString()
                    })
                    .ToListAsync(cancellationToken);

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
                    PendingBookings = pendingBookings,

                    RecentBookings = recentBookings,
                    RecentPurchaseRequests = recentPurchaseRequests,
                    RecentReactiveEnrollments = recentReactiveEnrollments,
                    RecentTeacherApplications = recentTeachers
                };

                ViewData["ActivePage"] = "AdminDashboard";
                return View(vm);
            }
            catch (OperationCanceledException) // request was cancelled
            {
                _logger.LogInformation("Dashboard Index cancelled by client.");
                return new StatusCodeResult(499); // client closed request (non-standard) / or just return NoContent()
            }
            catch (Exception ex)
            {
                // unexpected error: log and show minimal friendly message
                _logger.LogError(ex, "Failed to build admin dashboard");
                TempData["Error"] = "Admin.DashboardError";
                return View(new AdminDashboardVm()); // return an empty VM so view renders gracefully
            }
        }
    }
}


