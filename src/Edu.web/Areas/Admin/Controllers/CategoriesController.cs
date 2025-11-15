using Edu.Infrastructure.Data;
using Edu.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Edu.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext _db;
        public CategoriesController(ApplicationDbContext db) => _db = db;

        // GET: Admin/Categories
        public async Task<IActionResult> Index()
        {
            var cats = await _db.Categories.OrderBy(c => c.Name).AsNoTracking().ToListAsync();
            ViewData["ActivePage"] = "Categories";
            return View(cats);
        }

        // GET Create
        public IActionResult Create() => View(new Category());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category model)
        {
            if (!ModelState.IsValid) return View(model);
            _db.Categories.Add(model);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Category created.";
            return RedirectToAction(nameof(Index));
        }

        // Edit
        public async Task<IActionResult> Edit(int id)
        {
            var c = await _db.Categories.FindAsync(id);
            if (c == null) return NotFound();
            ViewData["ActivePage"] = "Categories";
            return View(c);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Category model)
        {
            if (!ModelState.IsValid) return View(model);
            _db.Categories.Update(model);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Category updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var c = await _db.Categories.Include(x => x.PrivateCourses).FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound();

            if (c.PrivateCourses != null && c.PrivateCourses.Any())
            {
                TempData["Error"] = "Cannot delete category used by private courses.";
                return RedirectToAction(nameof(Index));
            }

            _db.Categories.Remove(c);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Category deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}

