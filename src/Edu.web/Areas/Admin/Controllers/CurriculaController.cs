using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Web.Areas.Admin.ViewModels;
using Edu.Web.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Edu.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class CurriculaController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fs;
        private readonly IStringLocalizer<SharedResource> _L;

        public CurriculaController(ApplicationDbContext db, IFileStorageService fs, IStringLocalizer<SharedResource> localizer)
        {
            _db = db;
            _fs = fs;
            _L = localizer;
        }

        // GET: Admin/Curricula?levelId=1
        public async Task<IActionResult> Index(int? levelId)
        {
            ViewData["ActivePage"] = "Curricula";

            // load levels once, compute localized Text for each
            var levels = await _db.Levels
                                  .AsNoTracking()
                                  .OrderBy(l => l.Order)
                                  .ToListAsync();

            // Provide the raw levels list (for foreach + LocalizationHelpers in the Razor view)
            ViewBag.Levels = levels;

            // Provide an optional SelectList (if some views use tag helpers)
            var levelSelectItems = levels.Select(l => new { l.Id, Name = LocalizationHelpers.GetLocalizedLevelName(l) }).ToList();
            ViewBag.LevelSelectList = new SelectList(levelSelectItems, "Id", "Name", levelId);

            ViewBag.SelectedLevelId = levelId;

            // also create a lookup map for levelId -> localizedName (useful in views)
            ViewBag.LevelDisplayMap = levelSelectItems.ToDictionary(x => (int)x.Id, x => (string)x.Name);

            var q = _db.Curricula
                       .Include(c => c.Level)
                       .AsNoTracking()
                       .OrderBy(c => c.Order)
                       .AsQueryable();

            if (levelId.HasValue) q = q.Where(c => c.LevelId == levelId.Value);

            var list = await q.ToListAsync();

            // resolve cover image public URLs from storage keys (if present)
            foreach (var c in list)
            {
                if (!string.IsNullOrEmpty(c.CoverImageKey))
                {
                    try
                    {
                        c.CoverImageUrl = await _fs.GetPublicUrlAsync(c.CoverImageKey);
                    }
                    catch
                    {
                        c.CoverImageUrl = null;
                    }
                }
            }

            return View(list); // model: IEnumerable<Curriculum>
        }

        public async Task<IActionResult> Create()
        {
            ViewData["ActivePage"] = "Curricula";

            var levels = await _db.Levels
                                  .AsNoTracking()
                                  .OrderBy(l => l.Order)
                                  .ToListAsync();

            // Provide raw levels for the Razor foreach + localized name helper
            ViewBag.Levels = levels;

            // optional SelectList
            var levelSelectItems = levels.Select(l => new { l.Id, Name = LocalizationHelpers.GetLocalizedLevelName(l) }).ToList();
            ViewBag.LevelSelectList = new SelectList(levelSelectItems, "Id", "Name");

            return View(new CurriculumCreateViewModel());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CurriculumCreateViewModel vm)
        {
            // Ensure levels are available when we need to re-render the view
            var levels = await _db.Levels.AsNoTracking().OrderBy(l => l.Order).ToListAsync();
            ViewBag.Levels = levels;
            ViewBag.LevelSelectList = new SelectList(levels.Select(l => new { l.Id, Name = LocalizationHelpers.GetLocalizedLevelName(l) }), "Id", "Name", vm.LevelId);

            if (!ModelState.IsValid)
            {
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
            {
                var key = await _fs.SaveFileAsync(vm.CoverImage, "curricula");
                curr.CoverImageKey = key;
                try
                {
                    curr.CoverImageUrl = await _fs.GetPublicUrlAsync(key); // generated, not stored
                }
                catch
                {
                    curr.CoverImageUrl = null;
                }
            }

            _db.Curricula.Add(curr);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Curriculum created.";
            return RedirectToAction(nameof(Details), new { id = curr.Id });
        }

        // GET: Admin/Curricula/Details/5
        public async Task<IActionResult> Details(int id)
        {
            ViewData["ActivePage"] = "Curricula";

            var curriculum = await _db.Curricula
                                     .Include(c => c.Level)
                                     .AsNoTracking()
                                     .FirstOrDefaultAsync(c => c.Id == id);
            if (curriculum == null) return NotFound();

            // Load cover image URL from storage key (if available)
            if (!string.IsNullOrEmpty(curriculum.CoverImageKey))
            {
                try
                {
                    curriculum.CoverImageUrl = await _fs.GetPublicUrlAsync(curriculum.CoverImageKey);
                }
                catch
                {
                    curriculum.CoverImageUrl = null;
                }
            }

            // Load modules + their lessons + lesson files
            var modules = await _db.SchoolModules
                                   .AsNoTracking()
                                   .Where(m => m.CurriculumId == id)
                                   .OrderBy(m => m.Order)
                                   .Include(m => m.SchoolLessons)
                                       .ThenInclude(sl => sl.Files)
                                   .ToListAsync();

            // Load lessons that are directly under the curriculum (module-less)
            var directLessons = await _db.SchoolLessons
                                         .AsNoTracking()
                                         .Where(l => l.CurriculumId == id && l.ModuleId == null)
                                         .OrderBy(l => l.Order)
                                         .Include(l => l.Files)
                                         .ToListAsync();

            var vm = new CurriculumDetailsViewModel
            {
                Curriculum = curriculum,
                Modules = modules,
                DirectLessons = directLessons
            };

            // Provide localized mapping for levels if view needs it
            var levels = await _db.Levels.AsNoTracking().OrderBy(l => l.Order).ToListAsync();
            ViewBag.LevelDisplayMap = levels.Select(l => new { l.Id, Name = LocalizationHelpers.GetLocalizedLevelName(l) })
                                            .ToDictionary(x => (int)x.Id, x => (string)x.Name);
            ViewBag.Levels = levels;

            return View(vm);
        }

        public async Task<IActionResult> Edit(int id)
        {
            ViewData["ActivePage"] = "Curricula";
            var curr = await _db.Curricula.FindAsync(id);
            if (curr == null) return NotFound();

            var levels = await _db.Levels.AsNoTracking().OrderBy(l => l.Order).ToListAsync();

            // Provide raw levels for Razor foreach + localization helper
            ViewBag.Levels = levels;

            // Provide a SelectList for convenience (if you use tag helpers elsewhere)
            ViewBag.LevelSelectList = new SelectList(levels.Select(l => new { l.Id, Name = LocalizationHelpers.GetLocalizedLevelName(l) }), "Id", "Name", curr.LevelId);

            var vm = new CurriculumEditViewModel
            {
                Id = curr.Id,
                Title = curr.Title,
                Description = curr.Description,
                LevelId = curr.LevelId,
                Order = curr.Order,
                ExistingCoverUrl = !string.IsNullOrEmpty(curr.CoverImageKey) ? await _fs.GetPublicUrlAsync(curr.CoverImageKey) : curr.CoverImageUrl
            };

            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(CurriculumEditViewModel vm)
        {
            // repopulate levels for re-render on validation error
            var levels = await _db.Levels.AsNoTracking().OrderBy(l => l.Order).ToListAsync();
            ViewBag.Levels = levels;
            ViewBag.LevelSelectList = new SelectList(levels.Select(l => new { l.Id, Name = LocalizationHelpers.GetLocalizedLevelName(l) }), "Id", "Name", vm.LevelId);

            if (!ModelState.IsValid)
            {
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
                // delete old key if exists
                if (!string.IsNullOrEmpty(curr.CoverImageKey))
                {
                    try { await _fs.DeleteFileAsync(curr.CoverImageKey); } catch { /* ignore */ }
                }

                var newKey = await _fs.SaveFileAsync(vm.CoverImage, "curricula");
                curr.CoverImageKey = newKey;
                try
                {
                    curr.CoverImageUrl = await _fs.GetPublicUrlAsync(newKey);
                }
                catch
                {
                    curr.CoverImageUrl = null;
                }
            }

            _db.Curricula.Update(curr);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Curriculum updated.";
            return RedirectToAction(nameof(Details), new { id = curr.Id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var curriculum = await _db.Curricula.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
            if (curriculum == null)
            {
                TempData["Error"] = _L["Curriculum.NotFound"].Value;
                return RedirectToAction("Index");
            }

            // check for any modules
            var hasModules = await _db.SchoolModules.AsNoTracking().AnyAsync(m => m.CurriculumId == id);

            // check for any lessons that reference this curriculum (covers direct lessons and module lessons)
            var hasLessons = await _db.SchoolLessons.AsNoTracking().AnyAsync(l => l.CurriculumId == id);

            if (hasModules || hasLessons)
            {
                // Localized message
                TempData["Error"] = _L["Curriculum.DeleteHasChildren"].Value;
                return RedirectToAction("Details", new { id });
            }

            try
            {
                var toDelete = new Curriculum { Id = id };
                _db.Curricula.Attach(toDelete);
                _db.Curricula.Remove(toDelete);
                await _db.SaveChangesAsync();

                TempData["Success"] = _L["Curriculum.DeletedSuccess"].Value;
                return RedirectToAction("Index");
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = _L["Curriculum.DeleteFailed"].Value;
                return RedirectToAction("Details", new { id });
            }
        }
    }
}



