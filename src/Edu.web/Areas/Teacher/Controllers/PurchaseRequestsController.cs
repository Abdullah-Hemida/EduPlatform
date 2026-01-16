using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        }

        // GET: Teacher/PurchaseRequests
        public async Task<IActionResult> Index(string? q = null, PurchaseStatus? FilterStatus = null, int page = 1, int pageSize = 10)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // quick teacher existence check (keeps previous behavior)
            var teacherExists = await _db.Teachers.AsNoTracking().AnyAsync(t => t.Id == user.Id);
            if (!teacherExists) return Forbid();

            // safe clamp page parameters
            pageSize = Math.Clamp(pageSize, 1, 100);
            page = Math.Max(1, page);

            // query for the teacher's course ids (used to restrict the purchase requests)
            var teacherCourseIdsQuery = _db.PrivateCourses
                                           .AsNoTracking()
                                           .Where(c => c.TeacherId == user.Id)
                                           .Select(c => c.Id);

            // Base query: restrict to requests for this teacher's courses.
            // We do NOT Include navigation props — instead we project only required fields.
            var baseQuery = _db.PurchaseRequests
                               .AsNoTracking()
                               .Where(pr => teacherCourseIdsQuery.Contains(pr.PrivateCourseId));

            if (FilterStatus.HasValue)
            {
                baseQuery = baseQuery.Where(pr => pr.Status == FilterStatus.Value);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var norm = q.Trim();
                // Use server-side string matching (EF translates Contains -> LIKE for many providers).
                baseQuery = baseQuery.Where(pr =>
                    (pr.PrivateCourse != null && pr.PrivateCourse.Title != null && EF.Functions.Like(pr.PrivateCourse.Title, $"%{norm}%")) ||
                    (pr.Student != null && pr.Student.User != null && EF.Functions.Like(pr.Student.User.FullName, $"%{norm}%")) ||
                    (pr.Student != null && pr.Student.User != null && EF.Functions.Like(pr.Student.User.Email, $"%{norm}%")) ||
                    pr.Id.ToString() == norm
                );
            }

            // total count before paging
            var totalCount = await baseQuery.CountAsync();

            var skip = (page - 1) * pageSize;

            // Project the fields we need for the list view to avoid pulling full entities
            // Include category language columns so we can localize after materialization
            var items = await baseQuery
                        .OrderByDescending(pr => pr.RequestDateUtc)
                        .Skip(skip)
                        .Take(pageSize)
                        .Select(pr => new
                        {
                            pr.Id,
                            pr.PrivateCourseId,
                            CourseTitle = pr.PrivateCourse != null ? pr.PrivateCourse.Title : null,
                            // Category language columns (may be null)
                            CategoryNameEn = pr.PrivateCourse != null && pr.PrivateCourse.Category != null ? pr.PrivateCourse.Category.NameEn : null,
                            CategoryNameIt = pr.PrivateCourse != null && pr.PrivateCourse.Category != null ? pr.PrivateCourse.Category.NameIt : null,
                            CategoryNameAr = pr.PrivateCourse != null && pr.PrivateCourse.Category != null ? pr.PrivateCourse.Category.NameAr : null,

                            // student info
                            StudentFullName = pr.Student != null && pr.Student.User != null ? pr.Student.User.FullName : null,
                            StudentUserName = pr.Student != null && pr.Student.User != null ? pr.Student.User.UserName : null,
                            StudentEmail = pr.Student != null && pr.Student.User != null ? pr.Student.User.Email : null,

                            pr.RequestDateUtc,
                            Amount = pr.Amount,
                            Status = pr.Status
                        })
                        .ToListAsync();

            // Map into view model items, computing localized category name in-memory
            var vmItems = items.Select(i =>
            {
                // compute localized category name safely
                var localizedCategory = LocalizationHelpers.GetLocalizedCategoryName(new Category
                {
                    NameEn = i.CategoryNameEn ?? string.Empty,
                    NameIt = i.CategoryNameIt ?? string.Empty,
                    NameAr = i.CategoryNameAr ?? string.Empty
                });

                return new PurchaseRequestItemVm
                {
                    Id = i.Id,
                    PrivateCourseId = i.PrivateCourseId,
                    CourseTitle = i.CourseTitle,
                    CategoryName = string.IsNullOrWhiteSpace(localizedCategory) ? null : localizedCategory,
                    StudentName = !string.IsNullOrEmpty(i.StudentFullName) ? i.StudentFullName : i.StudentUserName,
                    StudentEmail = i.StudentEmail,
                    RequestDateUtc = i.RequestDateUtc,
                    AmountLabel = (i.Amount).ToEuro(),
                    Status = i.Status
                };
            }).ToList();

            var vm = new PurchaseRequestListVm
            {
                Query = q,
                FilterStatus = FilterStatus,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                Requests = vmItems
            };

            // Status dropdown options (localized)
            ViewData["StatusOptions"] = new SelectList(new[]
            {
                new { Value = "", Text = _localizer["Common.All"].Value },
                new { Value = PurchaseStatus.Pending.ToString(), Text = _localizer["Status.Pending"].Value },
                new { Value = PurchaseStatus.Completed.ToString(), Text = _localizer["Status.Completed"].Value },
                new { Value = PurchaseStatus.Rejected.ToString(), Text = _localizer["Status.Rejected"].Value },
                new { Value = PurchaseStatus.Cancelled.ToString(), Text = _localizer["Status.Cancelled"].Value }
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
            var teacherExists = await _db.Teachers.AsNoTracking().AnyAsync(t => t.Id == user.Id);
            if (!teacherExists) return Forbid();

            // fetch the purchase request projected and ensure it belongs to one of the teacher's courses
            var pr = await _db.PurchaseRequests
                              .AsNoTracking()
                              .Where(p => p.Id == id && _db.PrivateCourses.Where(c => c.TeacherId == user.Id).Select(c => c.Id).Contains(p.PrivateCourseId))
                              .Select(p => new
                              {
                                  p.Id,
                                  p.PrivateCourseId,
                                  CourseTitle = p.PrivateCourse != null ? p.PrivateCourse.Title : null,
                                  // project category language columns
                                  CategoryNameEn = p.PrivateCourse != null && p.PrivateCourse.Category != null ? p.PrivateCourse.Category.NameEn : null,
                                  CategoryNameIt = p.PrivateCourse != null && p.PrivateCourse.Category != null ? p.PrivateCourse.Category.NameIt : null,
                                  CategoryNameAr = p.PrivateCourse != null && p.PrivateCourse.Category != null ? p.PrivateCourse.Category.NameAr : null,

                                  p.StudentId,
                                  StudentName = p.Student != null && p.Student.User != null ? p.Student.User.FullName : null,
                                  StudentUserName = p.Student != null && p.Student.User != null ? p.Student.User.UserName : null,
                                  StudentEmail = p.Student != null && p.Student.User != null ? p.Student.User.Email : null,
                                  p.RequestDateUtc,
                                  p.Status,
                                  p.AdminNote,
                                  p.Amount
                              })
                              .FirstOrDefaultAsync();

            if (pr == null) return NotFound();

            // compute localized category name
            var localizedCategoryName = LocalizationHelpers.GetLocalizedCategoryName(new Category
            {
                NameEn = pr.CategoryNameEn ?? string.Empty,
                NameIt = pr.CategoryNameIt ?? string.Empty,
                NameAr = pr.CategoryNameAr ?? string.Empty
            });

            var vm = new PurchaseRequestDetailsVm
            {
                Id = pr.Id,
                PrivateCourseId = pr.PrivateCourseId,
                CourseTitle = pr.CourseTitle,
                CategoryName = string.IsNullOrWhiteSpace(localizedCategoryName) ? null : localizedCategoryName,
                StudentId = pr.StudentId,
                StudentName = !string.IsNullOrEmpty(pr.StudentName) ? pr.StudentName : pr.StudentUserName,
                StudentEmail = pr.StudentEmail,
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




