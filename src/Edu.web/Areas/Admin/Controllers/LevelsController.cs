using Edu.Infrastructure.Data;
using Edu.Domain.Entities;
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
        public LevelsController(ApplicationDbContext db , IMemoryCache memoryCache)
        {
            _db = db;
            _memoryCache = memoryCache;
        }

        // GET: Admin/Levels
        public async Task<IActionResult> Index()
        {
            var levels = await _db.Levels.OrderBy(l => l.Order).AsNoTracking().ToListAsync();
            ViewData["ActivePage"] = "Levels";
            return View(levels);
        }

        // GET: Admin/Levels/Create
        public IActionResult Create() => View(new Level());

        // POST: Admin/Levels/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Level model)
        {
            if (!ModelState.IsValid) return View(model);
            _memoryCache.Remove("School_AllLevels_v1");
            _db.Levels.Add(model);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Level created.";
            ViewData["ActivePage"] = "Levels";
            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Levels/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var level = await _db.Levels.FindAsync(id);
            if (level == null) return NotFound();
            ViewData["ActivePage"] = "Levels";
            return View(level);
        }

        // POST: Admin/Levels/Edit/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Level model)
        {
            if (!ModelState.IsValid) return View(model);
            _memoryCache.Remove("School_AllLevels_v1");
            _db.Levels.Update(model);
            await _db.SaveChangesAsync();
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
            _memoryCache.Remove("School_AllLevels_v1");
            _db.Levels.Remove(level);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Level deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}

