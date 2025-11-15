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

        // GET: /PrivateCourses
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Index(string q = null, int? categoryId = null)
        {
            var query = _db.PrivateCourses
                .AsNoTracking()
                .Where(c => c.IsPublished)
                .Include(c => c.Teacher).ThenInclude(t => t.User)
                .Include(c => c.Category)
                .AsQueryable();

            if (categoryId.HasValue && categoryId.Value > 0)
                query = query.Where(c => c.CategoryId == categoryId.Value);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(c =>
                    EF.Functions.Like(c.Title!, $"%{term}%") ||
                    EF.Functions.Like(c.Description!, $"%{term}%") ||
                    (c.Teacher != null && EF.Functions.Like(c.Teacher.User!.FullName!, $"%{term}%"))
                );
            }

            var list = await query.OrderByDescending(c => c.Id).ToListAsync();

            var vm = new PrivateCourseIndexVm
            {
                Query = q,
                SelectedCategoryId = categoryId,
                Courses = new List<PrivateCourseListItemVm>()
            };

            foreach (var c in list)
            {
                var coverKeyOrUrl = !string.IsNullOrEmpty(c.CoverImageKey) ? c.CoverImageKey : c.CoverImageUrl;
                string? coverPublicUrl = null;
                if (!string.IsNullOrEmpty(coverKeyOrUrl))
                    coverPublicUrl = await _fileStorage.GetPublicUrlAsync(coverKeyOrUrl);

                vm.Courses.Add(new PrivateCourseListItemVm
                {
                    Id = c.Id,
                    Title = c.Title,
                    Description = c.Description?.Length > 240 ? c.Description.Substring(0, 240) + "…" : c.Description,
                    CoverPublicUrl = coverPublicUrl,
                    TeacherName = c.Teacher?.User?.FullName ?? c.Teacher?.User?.UserName,
                    CategoryName = c.Category?.Name,
                    PriceLabel = c.Price.ToEuro()
                });
            }

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

            var coverKeyOrUrl = !string.IsNullOrEmpty(course.CoverImageKey) ? course.CoverImageKey : course.CoverImageUrl;
            string? coverPublicUrl = null;
            if (!string.IsNullOrEmpty(coverKeyOrUrl))
                coverPublicUrl = await _fileStorage.GetPublicUrlAsync(coverKeyOrUrl);

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
                CoverPublicUrl = coverPublicUrl,
                TeacherName = course.Teacher?.User?.FullName ?? course.Teacher?.User?.UserName,
                CategoryName = course.Category?.Name,
                PriceLabel = course.Price.ToEuro(),
                IsPurchased = hasCompletedPurchase,
                HasPendingPurchase = hasPendingPurchase
                // NOTE: No Modules/Lessons included here — student area handles that after purchase completed
            };

            return View(vm);
        }
    }
}



