using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Web.Areas.Admin.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Edu.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<CategoriesController> _logger;
        private const string CategoriesCacheKey = "School_AllCategories_v1";

        public CategoriesController(ApplicationDbContext db, IMemoryCache memoryCache, ILogger<CategoriesController> logger)
        {
            _db = db;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        // GET: Admin/Categories
        public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
        {
            ViewData["ActivePage"] = "Categories";

            var cached = await _memoryCache.GetOrCreateAsync(CategoriesCacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60);

                var list = await _db.Categories
                    .AsNoTracking()
                    .OrderBy(c => c.Id)
                    .Select(c => new Category
                    {
                        Id = c.Id,
                        NameEn = c.NameEn,
                        NameIt = c.NameIt,
                        NameAr = c.NameAr
                    })
                    .ToListAsync(cancellationToken);

                return list;
            });

            var vm = cached.Select(c => new CategoryAdminListItemVm
            {
                Id = c.Id,
                NameEn = c.NameEn,
                NameIt = c.NameIt,
                NameAr = c.NameAr,
                LocalizedName = LocalizationHelpers.GetLocalizedCategoryName(c)
            }).ToList();

            return View(vm);
        }

        // GET: Admin/Categories/Create
        public IActionResult Create()
        {
            ViewData["ActivePage"] = "Categories";
            var vm = new CategoryEditVm();
            return View(vm);
        }

        // POST: Admin/Categories/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CategoryEditVm model, CancellationToken cancellationToken = default)
        {
            ViewData["ActivePage"] = "Categories";
            if (!ModelState.IsValid) return View(model);

            var category = new Category
            {
                NameEn = model.NameEn?.Trim() ?? string.Empty,
                NameIt = model.NameIt?.Trim() ?? string.Empty,
                NameAr = model.NameAr?.Trim() ?? string.Empty
            };

            try
            {
                _db.Categories.Add(category);
                await _db.SaveChangesAsync(cancellationToken);

                _memoryCache.Remove(CategoriesCacheKey);

                TempData["Success"] = "Category.Created";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed creating category");
                ModelState.AddModelError(string.Empty, "An error occurred while creating the category.");
                return View(model);
            }
        }

        // GET: Admin/Categories/Edit/5
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken = default)
        {
            var category = await _db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            if (category == null) return NotFound();

            var vm = new CategoryEditVm
            {
                Id = category.Id,
                NameEn = category.NameEn,
                NameIt = category.NameIt,
                NameAr = category.NameAr
            };

            ViewData["ActivePage"] = "Categories";
            return View(vm);
        }

        // POST: Admin/Categories/Edit/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(CategoryEditVm model, CancellationToken cancellationToken = default)
        {
            ViewData["ActivePage"] = "Categories";
            if (!ModelState.IsValid) return View(model);

            var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == model.Id, cancellationToken);
            if (category == null) return NotFound();

            category.NameEn = model.NameEn?.Trim() ?? string.Empty;
            category.NameIt = model.NameIt?.Trim() ?? string.Empty;
            category.NameAr = model.NameAr?.Trim() ?? string.Empty;

            try
            {
                _db.Categories.Update(category);
                await _db.SaveChangesAsync(cancellationToken);

                _memoryCache.Remove(CategoriesCacheKey);

                TempData["Success"] = "Category.Updated";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed editing category {CategoryId}", model.Id);
                ModelState.AddModelError(string.Empty, "An error occurred while updating the category.");
                return View(model);
            }
        }

        // POST: Admin/Categories/Delete/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default)
        {
            // If other entities reference category, check and stop deletion
            var inUse = await _db.PrivateCourses.AsNoTracking().AnyAsync(pc => pc.CategoryId == id, cancellationToken);
            if (inUse)
            {
                TempData["Error"] = "Category.DeleteHasReferences";
                return RedirectToAction(nameof(Index));
            }

            var category = await _db.Categories.FindAsync(new object[] { id }, cancellationToken);
            if (category == null) return NotFound();

            try
            {
                _db.Categories.Remove(category);
                await _db.SaveChangesAsync(cancellationToken);

                _memoryCache.Remove(CategoriesCacheKey);

                TempData["Success"] = "Category.Deleted";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Delete failed for category {CategoryId}", id);
                TempData["Error"] = "Category.DeleteFailed";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}



