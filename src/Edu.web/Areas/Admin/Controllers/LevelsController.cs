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
    public class LevelsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IMemoryCache _memoryCache;
        private const string LevelsCacheKey = "School_AllLevels_v1";

        public LevelsController(ApplicationDbContext db, IMemoryCache memoryCache)
        {
            _db = db;
            _memoryCache = memoryCache;
        }

        // GET: Admin/Levels
        public async Task<IActionResult> Index()
        {
            var levels = await _db.Levels
                                  .AsNoTracking()
                                  .OrderBy(l => l.Order)
                                  .ToListAsync();

            var vm = levels.Select(l => new LevelAdminListItemVm
            {
                Id = l.Id,
                NameEn = l.NameEn,
                NameIt = l.NameIt,
                NameAr = l.NameAr,
                Order = l.Order,
                LocalizedName = LocalizationHelpers.GetLocalizedLevelName(l)
            }).ToList();

            ViewData["ActivePage"] = "Levels";
            return View(vm);
        }

        // GET: Admin/Levels/Create
        public IActionResult Create()
        {
            ViewData["ActivePage"] = "Levels";
            var vm = new LevelEditVm();
            return View(vm);
        }

        // POST: Admin/Levels/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LevelEditVm model)
        {
            if (!ModelState.IsValid)
            {
                ViewData["ActivePage"] = "Levels";
                return View(model);
            }

            var level = new Level
            {
                NameEn = model.NameEn?.Trim() ?? string.Empty,
                NameIt = model.NameIt?.Trim() ?? string.Empty,
                NameAr = model.NameAr?.Trim() ?? string.Empty,
                Order = model.Order
            };

            _db.Levels.Add(level);
            await _db.SaveChangesAsync();

            // invalidate cache
            _memoryCache.Remove(LevelsCacheKey);

            TempData["Success"] = "Level created.";
            ViewData["ActivePage"] = "Levels";
            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Levels/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var level = await _db.Levels.FindAsync(id);
            if (level == null) return NotFound();

            var vm = new LevelEditVm
            {
                Id = level.Id,
                NameEn = level.NameEn,
                NameIt = level.NameIt,
                NameAr = level.NameAr,
                Order = level.Order
            };

            ViewData["ActivePage"] = "Levels";
            return View(vm);
        }

        // POST: Admin/Levels/Edit/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(LevelEditVm model)
        {
            if (!ModelState.IsValid)
            {
                ViewData["ActivePage"] = "Levels";
                return View(model);
            }

            var level = await _db.Levels.FirstOrDefaultAsync(l => l.Id == model.Id);
            if (level == null) return NotFound();

            level.NameEn = model.NameEn?.Trim() ?? string.Empty;
            level.NameIt = model.NameIt?.Trim() ?? string.Empty;
            level.NameAr = model.NameAr?.Trim() ?? string.Empty;
            level.Order = model.Order;

            _db.Levels.Update(level);
            await _db.SaveChangesAsync();

            // invalidate cache
            _memoryCache.Remove(LevelsCacheKey);

            TempData["Success"] = "Level updated.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Levels/Delete/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var level = await _db.Levels.Include(l => l.Curricula).FirstOrDefaultAsync(l => l.Id == id);
            if (level == null) return NotFound();

            if (level.Curricula != null && level.Curricula.Any())
            {
                TempData["Error"] = "Cannot delete level that has curricula. Remove or move curricula first.";
                return RedirectToAction(nameof(Index));
            }

            _db.Levels.Remove(level);
            await _db.SaveChangesAsync();

            // invalidate cache
            _memoryCache.Remove(LevelsCacheKey);

            TempData["Success"] = "Level deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}

