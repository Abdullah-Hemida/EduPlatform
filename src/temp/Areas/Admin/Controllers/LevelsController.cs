using System.Globalization;
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
        private readonly ILogger<LevelsController> _logger;
        private const string LevelsCacheKey = "School_AllLevels_v1";

        public LevelsController(ApplicationDbContext db, IMemoryCache memoryCache, ILogger<LevelsController> logger)
        {
            _db = db;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        // GET: Admin/Levels
        public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
        {
            ViewData["ActivePage"] = "Levels";

            // Cache the raw level POCOs (no EF tracking). LocalizedName is computed per-request so it follows current culture.
            var cachedLevels = await _memoryCache.GetOrCreateAsync(LevelsCacheKey, async entry =>
            {
                // Cache for 60 minutes by default — invalidated on create/edit/delete
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60);

                // Project to simple Level instances (detached POCOs) to avoid caching tracked EF entities.
                var list = await _db.Levels
                                    .AsNoTracking()
                                    .OrderBy(l => l.Order)
                                    .Select(l => new Level
                                    {
                                        Id = l.Id,
                                        NameEn = l.NameEn,
                                        NameIt = l.NameIt,
                                        NameAr = l.NameAr,
                                        Order = l.Order
                                    })
                                    .ToListAsync(cancellationToken);

                return list;
            });

            // Build VM and compute localized name for the current culture
            var vm = cachedLevels.Select(l => new LevelAdminListItemVm
            {
                Id = l.Id,
                NameEn = l.NameEn,
                NameIt = l.NameIt,
                NameAr = l.NameAr,
                Order = l.Order,
                LocalizedName = LocalizationHelpers.GetLocalizedLevelName(l)
            }).ToList();

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
        public async Task<IActionResult> Create(LevelEditVm model, CancellationToken cancellationToken = default)
        {
            ViewData["ActivePage"] = "Levels";

            if (!ModelState.IsValid) return View(model);

            var level = new Level
            {
                NameEn = model.NameEn?.Trim() ?? string.Empty,
                NameIt = model.NameIt?.Trim() ?? string.Empty,
                NameAr = model.NameAr?.Trim() ?? string.Empty,
                Order = model.Order
            };

            try
            {
                _db.Levels.Add(level);
                await _db.SaveChangesAsync(cancellationToken);

                // invalidate cache
                _memoryCache.Remove(LevelsCacheKey);

                TempData["Success"] = "Level.Created";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed creating level");
                ModelState.AddModelError(string.Empty, "An error occurred while creating the level.");
                return View(model);
            }
        }

        // GET: Admin/Levels/Edit/5
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken = default)
        {
            var level = await _db.Levels.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
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
        public async Task<IActionResult> Edit(LevelEditVm model, CancellationToken cancellationToken = default)
        {
            ViewData["ActivePage"] = "Levels";

            if (!ModelState.IsValid) return View(model);

            var level = await _db.Levels.FirstOrDefaultAsync(l => l.Id == model.Id, cancellationToken);
            if (level == null) return NotFound();

            level.NameEn = model.NameEn?.Trim() ?? string.Empty;
            level.NameIt = model.NameIt?.Trim() ?? string.Empty;
            level.NameAr = model.NameAr?.Trim() ?? string.Empty;
            level.Order = model.Order;

            try
            {
                _db.Levels.Update(level);
                await _db.SaveChangesAsync(cancellationToken);

                // invalidate cache
                _memoryCache.Remove(LevelsCacheKey);

                TempData["Success"] = "Level.Updated";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed editing level {LevelId}", model.Id);
                ModelState.AddModelError(string.Empty, "An error occurred while updating the level.");
                return View(model);
            }
        }

        // POST: Admin/Levels/Delete/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default)
        {
            // Efficient check: ask DB whether any curricula reference this level
            var inUse = await _db.Curricula.AsNoTracking().AnyAsync(c => c.LevelId == id, cancellationToken);
            if (inUse)
            {
                TempData["Error"] = "Level.DeleteHasCurricula";
                return RedirectToAction(nameof(Index));
            }

            var level = await _db.Levels.FindAsync(new object[] { id }, cancellationToken);
            if (level == null) return NotFound();

            try
            {
                _db.Levels.Remove(level);
                await _db.SaveChangesAsync(cancellationToken);

                // invalidate cache
                _memoryCache.Remove(LevelsCacheKey);

                TempData["Success"] = "Level.Deleted";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Delete failed for level {LevelId}", id);
                TempData["Error"] = "Level.DeleteFailed";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}


