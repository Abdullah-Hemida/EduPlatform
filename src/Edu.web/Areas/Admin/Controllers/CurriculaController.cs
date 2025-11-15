using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Application.IServices;
using Edu.Web.Areas.Admin.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Edu.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class CurriculaController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fs;

        public CurriculaController(ApplicationDbContext db, IFileStorageService fs)
        {
            _db = db;
            _fs = fs;
        }

        // GET: Admin/Curricula?levelId=1
        public async Task<IActionResult> Index(int? levelId)
        {
            ViewData["ActivePage"] = "Curricula";
            ViewBag.Levels = await _db.Levels.OrderBy(l => l.Order).AsNoTracking().ToListAsync();
            ViewBag.SelectedLevelId = levelId;

            var q = _db.Curricula.Include(c => c.Level).AsNoTracking().OrderBy(c => c.Order).AsQueryable();
            if (levelId.HasValue) q = q.Where(c => c.LevelId == levelId.Value);

            var list = await q.ToListAsync();
            return View(list); // model: IEnumerable<Curriculum>
        }

        public async Task<IActionResult> Create()
        {
            ViewData["ActivePage"] = "Curricula";
            ViewBag.Levels = await _db.Levels.OrderBy(l => l.Order).AsNoTracking().ToListAsync();
            return View(new CurriculumCreateViewModel());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CurriculumCreateViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Levels = await _db.Levels.OrderBy(l => l.Order).AsNoTracking().ToListAsync();
                return View(vm);
            }

            var curr = new Curriculum
            {
                Title = vm.Title,
                Description = vm.Description,
                LevelId = vm.LevelId,
                Order = vm.Order
            };

            if (vm.CoverImage != null)
                curr.CoverImageUrl = await _fs.SaveFileAsync(vm.CoverImage, "curricula");

            _db.Curricula.Add(curr);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Curriculum created.";
            return RedirectToAction(nameof(Details), new { id = curr.Id });
        }

        public async Task<IActionResult> Details(int id)
        {
            ViewData["ActivePage"] = "Curricula";

            var curriculum = await _db.Curricula
                                     .Include(c => c.Level)
                                     .AsNoTracking()
                                     .FirstOrDefaultAsync(c => c.Id == id);
            if (curriculum == null) return NotFound();

            var modules = await _db.SchoolModules
                                   .Where(m => m.CurriculumId == id)
                                   .OrderBy(m => m.Order)
                                   .Include(m => m.SchoolLessons.OrderBy(sl => sl.Order))
                                   .ThenInclude(sl => sl.Files)
                                   .AsNoTracking()
                                   .ToListAsync();

            var vm = new CurriculumDetailsViewModel
            {
                Curriculum = curriculum,
                Modules = modules
            };

            return View(vm);
        }

        public async Task<IActionResult> Edit(int id)
        {
            ViewData["ActivePage"] = "Curricula";
            var curr = await _db.Curricula.FindAsync(id);
            if (curr == null) return NotFound();

            ViewBag.Levels = await _db.Levels.OrderBy(l => l.Order).AsNoTracking().ToListAsync();

            var vm = new CurriculumEditViewModel
            {
                Id = curr.Id,
                Title = curr.Title,
                Description = curr.Description,
                LevelId = curr.LevelId,
                Order = curr.Order,
                ExistingCoverUrl = curr.CoverImageUrl
            };

            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(CurriculumEditViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Levels = await _db.Levels.OrderBy(l => l.Order).AsNoTracking().ToListAsync();
                return View(vm);
            }

            var curr = await _db.Curricula.FindAsync(vm.Id);
            if (curr == null) return NotFound();

            curr.Title = vm.Title;
            curr.Description = vm.Description;
            curr.LevelId = vm.LevelId;
            curr.Order = vm.Order;

            if (vm.CoverImage != null)
            {
                // delete old if exists
                if (!string.IsNullOrEmpty(curr.CoverImageUrl))
                {
                    await _fs.DeleteFileAsync(curr.CoverImageUrl);
                }
                curr.CoverImageUrl = await _fs.SaveFileAsync(vm.CoverImage, "curricula");
            }

            _db.Curricula.Update(curr);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Curriculum updated.";
            return RedirectToAction(nameof(Details), new { id = curr.Id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var curr = await _db.Curricula.FindAsync(id);
            if (curr == null) return NotFound();

            // delete modules/lessons/files
            var modules = await _db.SchoolModules.Where(m => m.CurriculumId == id).ToListAsync();
            foreach (var m in modules)
            {
                var lessons = await _db.SchoolLessons.Where(sl => sl.ModuleId == m.Id).ToListAsync();
                foreach (var l in lessons)
                {
                    var files = await _db.FileResources.Where(fr => fr.SchoolLessonId == l.Id).ToListAsync();
                    foreach (var f in files)
                    {
                        await _fs.DeleteFileAsync(f.FileUrl);
                        _db.FileResources.Remove(f);
                    }
                    _db.SchoolLessons.Remove(l);
                }
                _db.SchoolModules.Remove(m);
            }

            if (!string.IsNullOrEmpty(curr.CoverImageUrl))
            {
                await _fs.DeleteFileAsync(curr.CoverImageUrl);
            }

            _db.Curricula.Remove(curr);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Curriculum deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}



