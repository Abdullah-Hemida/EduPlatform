using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Web.Areas.Teacher.ViewModels;
using Edu.Web.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Edu.Web.Areas.Teacher.Controllers
{
    [Area("Teacher")]
    [Authorize(Roles = "Teacher")]
    public class PurchaseRequestsController : TeacherBaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public PurchaseRequestsController(ApplicationDbContext db,
                                          UserManager<ApplicationUser> userManager,
                                          IStringLocalizer<SharedResource> localizer)
        {
            _db = db;
            _userManager = userManager;
            _localizer = localizer;
        }

        // GET: Teacher/PurchaseRequests
        public async Task<IActionResult> Index(string? q = null, PurchaseStatus? FilterStatus = null, int page = 1, int pageSize = 10)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var teacher = await _db.Teachers.AsNoTracking().FirstOrDefaultAsync(t => t.Id == user.Id);
            if (teacher == null) return Forbid();

            var teacherCourseIdsQuery = _db.PrivateCourses
                                           .AsNoTracking()
                                           .Where(c => c.TeacherId == user.Id)
                                           .Select(c => c.Id);

            var query = _db.PurchaseRequests
                           .AsNoTracking()
                           .Where(pr => teacherCourseIdsQuery.Contains(pr.PrivateCourseId))
                           .Include(pr => pr.PrivateCourse)
                               .ThenInclude(c => c.Category)
                           .Include(pr => pr.Student)
                               .ThenInclude(s => s.User)
                           .AsQueryable();

            if (FilterStatus.HasValue)
            {
                query = query.Where(pr => pr.Status == FilterStatus.Value);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var norm = q.Trim();
                query = query.Where(pr =>
                    (pr.PrivateCourse != null && pr.PrivateCourse.Title != null && pr.PrivateCourse.Title.Contains(norm)) ||
                    (pr.Student != null && pr.Student.User != null && pr.Student.User.FullName != null && pr.Student.User.FullName.Contains(norm)) ||
                    (pr.Student != null && pr.Student.User != null && pr.Student.User.Email != null && pr.Student.User.Email.Contains(norm)) ||
                    pr.Id.ToString() == norm
                );
            }

            // total count before paging
            var totalCount = await query.CountAsync();

            // clamp page/pageSize
            pageSize = Math.Max(1, Math.Min(100, pageSize)); // safe bounds
            page = Math.Max(1, page);

            var items = await query
                        .OrderByDescending(pr => pr.RequestDateUtc)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToListAsync();

            var vm = new PurchaseRequestListVm
            {
                Query = q,
                FilterStatus = FilterStatus,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                Requests = items.Select(pr => new PurchaseRequestItemVm
                {
                    Id = pr.Id,
                    PrivateCourseId = pr.PrivateCourseId,
                    CourseTitle = pr.PrivateCourse?.Title,
                    CourseCoverUrl = pr.PrivateCourse?.CoverImageUrl,
                    StudentName = pr.Student?.User?.FullName ?? pr.Student?.User?.UserName,
                    StudentEmail = pr.Student?.User?.Email,
                    RequestDateUtc = pr.RequestDateUtc,
                    AmountLabel = pr.Amount.ToEuro(),
                    Status = pr.Status
                }).ToList()
            };

            ViewData["StatusOptions"] = new SelectList(new[]
            {
        new { Value = "", Text = _localizer["Common.All"].Value },
        new { Value = PurchaseStatus.Pending.ToString(), Text = _localizer["Status.Pending"].Value },
        new { Value = PurchaseStatus.Completed.ToString(), Text = _localizer["Status.Completed"].Value },
        new { Value = PurchaseStatus.Rejected.ToString(), Text = _localizer["Status.Rejected"].Value },
    }, "Value", "Text", FilterStatus?.ToString());

            ViewData["ActivePage"] = "PurchaseRequests";
            return View(vm);
        }

        // GET: Teacher/PurchaseRequests/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // ensure teacher exists
            var teacher = await _db.Teachers.AsNoTracking().FirstOrDefaultAsync(t => t.Id == user.Id);
            if (teacher == null) return Forbid();

            var pr = await _db.PurchaseRequests
                              .AsNoTracking()
                              .Include(p => p.PrivateCourse).ThenInclude(c => c.Category)
                              .Include(p => p.Student).ThenInclude(s => s.User)
                              .FirstOrDefaultAsync(p => p.Id == id);

            if (pr == null) return NotFound();

            // ensure this purchase request belongs to one of teacher's courses
            if (pr.PrivateCourse?.TeacherId != user.Id) return Forbid();

            var vm = new PurchaseRequestDetailsVm
            {
                Id = pr.Id,
                PrivateCourseId = pr.PrivateCourseId,
                CourseTitle = pr.PrivateCourse?.Title,
                CourseCoverUrl = pr.PrivateCourse?.CoverImageUrl,
                CategoryName = pr.PrivateCourse?.Category?.Name,
                StudentId = pr.StudentId,
                StudentName = pr.Student?.User?.FullName ?? pr.Student?.User?.UserName,
                StudentEmail = pr.Student?.User?.Email,
                RequestDateUtc = pr.RequestDateUtc,
                Status = pr.Status,
                AdminNote = pr.AdminNote,
                Amount = pr.Amount,
                AmountLabel = pr.Amount.ToEuro()
            };

            ViewData["ActivePage"] = "PurchaseRequests";
            return View(vm);
        }
    }
}


