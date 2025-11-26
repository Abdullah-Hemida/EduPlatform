using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Web.Resources;
using Edu.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Edu.Web.Controllers
{
    public class PrivateCoursesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fileStorage;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public PrivateCoursesController(
            ApplicationDbContext db,
            IFileStorageService fileStorage,
            UserManager<ApplicationUser> userManager,
            IStringLocalizer<SharedResource> localizer)
        {
            _db = db;
            _fileStorage = fileStorage;
            _userManager = userManager;
            _localizer = localizer;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Index(string q = null, int? categoryId = null, bool? forChildren = null, int page = 1)
        {
            const int pageSize = 12;

            var query = _db.PrivateCourses
                .AsNoTracking()
                .Where(c => c.IsPublished)
                .Include(c => c.Teacher).ThenInclude(t => t.User)
                .Include(c => c.Category)
                .AsQueryable();

            // Category filter
            if (categoryId.HasValue && categoryId.Value > 0)
                query = query.Where(c => c.CategoryId == categoryId.Value);

            // Search filter
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(c =>
                    EF.Functions.Like(c.Title!, $"%{term}%") ||
                    EF.Functions.Like(c.Description!, $"%{term}%") ||
                    (c.Teacher != null && EF.Functions.Like(c.Teacher.User!.FullName!, $"%{term}%"))
                );
            }

            // Filter: For Children Only
            if (forChildren == true)
                query = query.Where(c => c.IsForChildren);

            // Pagination
            int totalCourses = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalCourses / (double)pageSize);

            var list = await query
                .OrderByDescending(c => c.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vm = new PrivateCourseIndexVm
            {
                Query = q,
                SelectedCategoryId = categoryId,
                ForChildrenOnly = forChildren,

                PageNumber = page,
                TotalPages = totalPages
            };

            foreach (var c in list)
            {
                vm.Courses.Add(new PrivateCourseListItemVm
                {
                    Id = c.Id,
                    Title = c.Title,
                    Description = c.Description?.Length > 240 ? c.Description.Substring(0, 240) + "…" : c.Description,
                    CoverImageKey = c.CoverImageKey,
                    TeacherName = c.Teacher?.User?.FullName ?? c.Teacher?.User?.UserName,
                    CategoryName = c.Category?.Name,
                    PriceLabel = c.Price.ToEuro(),
                    IsForChildren = c.IsForChildren
                });
            }

            // Resolve image URLs
            foreach (var course in vm.Courses)
            {
                course.CoverPublicUrl = !string.IsNullOrEmpty(course.CoverImageKey)
                    ? await _fileStorage.GetPublicUrlAsync(course.CoverImageKey)
                    : null;
            }

            // Load categories
            vm.Categories = await _db.Categories
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new CategoryVm { Id = x.Id, Name = x.Name })
                .ToListAsync();

            return View(vm);
        }

        // GET: /PrivateCourses/Details/5 (public view — NO modules/lessons)
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Details(int id)
        {
            var course = await _db.PrivateCourses
                .AsNoTracking()
                .Include(c => c.Teacher).ThenInclude(t => t.User)
                .Include(c => c.Category)
                .FirstOrDefaultAsync(c => c.Id == id && c.IsPublished);

            if (course == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            bool hasCompletedPurchase = false;
            bool hasPendingPurchase = false;
            if (!string.IsNullOrEmpty(userId))
            {
                hasCompletedPurchase = await _db.PurchaseRequests
                    .AsNoTracking()
                    .AnyAsync(p => p.PrivateCourseId == id && p.StudentId == userId && p.Status == PurchaseStatus.Completed);

                hasPendingPurchase = await _db.PurchaseRequests
                    .AsNoTracking()
                    .AnyAsync(p => p.PrivateCourseId == id && p.StudentId == userId && p.Status == PurchaseStatus.Pending);
            }

            var vm = new PrivateCourseDetailsVm
            {
                Id = course.Id,
                Title = course.Title,
                Description = course.Description,
                CoverImageKey = course.CoverImageKey,
                TeacherName = course.Teacher?.User?.FullName ?? course.Teacher?.User?.UserName,
                CategoryName = course.Category?.Name,
                PriceLabel = course.Price.ToEuro(),
                IsPurchased = hasCompletedPurchase,
                HasPendingPurchase = hasPendingPurchase
                // NOTE: No Modules/Lessons included here — student area handles that after purchase completed
            };
            if (!string.IsNullOrEmpty(vm.CoverImageKey))
            {
                vm.CoverPublicUrl = await _fileStorage.GetPublicUrlAsync(vm.CoverImageKey);

            }
            else
            {
                 vm.CoverPublicUrl = null;
            }
            return View(vm);
        }
    }
}



