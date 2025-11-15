using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Web.Areas.Admin.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Edu.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ModulesController : Controller
    {
        private readonly ApplicationDbContext _db;
        public ModulesController(ApplicationDbContext db) { _db = db; }

        // GET: Admin/Modules/Create?curriculumId=5
        public IActionResult Create(int curriculumId)
        {
            ViewData["ActivePage"] = "Curricula";
            var vm = new ModuleCreateViewModel { CurriculumId = curriculumId, Order = 1 };
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ModuleCreateViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var m = new SchoolModule
            {
                CurriculumId = vm.CurriculumId,
                Title = vm.Title,
                Order = vm.Order
            };
            _db.SchoolModules.Add(m);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Module added.";
            return RedirectToAction("Details", "Curricula", new { area = "Admin", id = vm.CurriculumId });
        }

        public async Task<IActionResult> Edit(int id)
        {
            ViewData["ActivePage"] = "Curricula";
            var m = await _db.SchoolModules.FindAsync(id);
            if (m == null) return NotFound();

            var vm = new ModuleEditViewModel { Id = m.Id, CurriculumId = m.CurriculumId, Title = m.Title, Order = m.Order };
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ModuleEditViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var m = await _db.SchoolModules.FindAsync(vm.Id);
            if (m == null) return NotFound();

            m.Title = vm.Title;
            m.Order = vm.Order;
            _db.SchoolModules.Update(m);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Module updated.";
            return RedirectToAction("Details", "Curricula", new { area = "Admin", id = vm.CurriculumId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, int curriculumId)
        {
            var m = await _db.SchoolModules.FindAsync(id);
            if (m == null) return NotFound();

            // delete lessons and files
            var lessons = await _db.SchoolLessons.Where(l => l.ModuleId == id).ToListAsync();
            foreach (var l in lessons)
            {
                var files = await _db.FileResources.Where(fr => fr.SchoolLessonId == l.Id).ToListAsync();
                foreach (var f in files)
                {
                    // Note: file deletion should be handled via file service, but not available here - inject if needed
                    _db.FileResources.Remove(f);
                }
                _db.SchoolLessons.Remove(l);
            }

            _db.SchoolModules.Remove(m);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Module deleted.";
            return RedirectToAction("Details", "Curricula", new { area = "Admin", id = curriculumId });
        }
    }
}

