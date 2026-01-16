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
using Microsoft.Extensions.Logging;

namespace Edu.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class CurriculaController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fs;
        private readonly IStringLocalizer<SharedResource> _L;
        private readonly ILogger<CurriculaController> _logger;

        public CurriculaController(
            ApplicationDbContext db,
            IFileStorageService fs,
            IStringLocalizer<SharedResource> localizer,
            ILogger<CurriculaController> logger)
        {
            _db = db;
            _fs = fs;
            _L = localizer;
            _logger = logger;
        }

        // Helper to populate levels + select list + display map
        private async Task PopulateLevelsAsync(int? selectedLevelId = null, CancellationToken cancellationToken = default)
        {
            var levels = await _db.Levels
                                  .AsNoTracking()
                                  .OrderBy(l => l.Order)
                                  .ToListAsync(cancellationToken);

            ViewBag.Levels = levels;

            var levelSelectItems = levels
                .Select(l => new { l.Id, Name = LocalizationHelpers.GetLocalizedLevelName(l) })
                .ToList();

            ViewBag.LevelSelectList = new SelectList(levelSelectItems, "Id", "Name", selectedLevelId);
            ViewBag.LevelDisplayMap = levelSelectItems.ToDictionary(x => (int)x.Id, x => (string)x.Name);
            ViewBag.SelectedLevelId = selectedLevelId;
        }

        // GET: Admin/Curricula?levelId=1
        public async Task<IActionResult> Index(int? levelId, CancellationToken cancellationToken = default)
        {
            ViewData["ActivePage"] = "Curricula";

            await PopulateLevelsAsync(levelId, cancellationToken);

            var q = _db.Curricula
                       .AsNoTracking()
                       .OrderBy(c => c.Order)
                       .AsQueryable();

            if (levelId.HasValue) q = q.Where(c => c.LevelId == levelId.Value);

            // include Level for display; if you only show name you could project to a lightweight VM
            q = q.Include(c => c.Level);

            var list = await q.ToListAsync(cancellationToken);

            // resolve cover image public URLs concurrently (faster than sequential awaits)
            var tasks = new List<Task>();
            foreach (var c in list)
            {
                if (!string.IsNullOrEmpty(c.CoverImageKey))
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            c.CoverImageUrl = await _fs.GetPublicUrlAsync(c.CoverImageKey);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Failed getting public URL for curriculum {CurriculumId}", c.Id);
                            c.CoverImageUrl = null;
                        }
                    }));
                }
            }
            if (tasks.Count > 0) await Task.WhenAll(tasks);

            return View(list); // model: IEnumerable<Curriculum>
        }

        public async Task<IActionResult> Create(CancellationToken cancellationToken = default)
        {
            ViewData["ActivePage"] = "Curricula";
            await PopulateLevelsAsync(null, cancellationToken);
            return View(new CurriculumCreateViewModel());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CurriculumCreateViewModel vm, CancellationToken cancellationToken = default)
        {
            await PopulateLevelsAsync(vm.LevelId, cancellationToken);

            if (!ModelState.IsValid) return View(vm);

            var curr = new Curriculum
            {
                Title = vm.Title,
                Description = vm.Description,
                LevelId = vm.LevelId,
                Order = vm.Order
            };

            if (vm.CoverImage != null)
            {
                try
                {
                    var key = await _fs.SaveFileAsync(vm.CoverImage, "curricula");
                    curr.CoverImageKey = key;
                    try
                    {
                        curr.CoverImageUrl = await _fs.GetPublicUrlAsync(key);
                    }
                    catch
                    {
                        curr.CoverImageUrl = null;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save cover image for new curriculum");
                    // don't fail creation because of storage issues; set url/key to null
                    curr.CoverImageKey = null;
                    curr.CoverImageUrl = null;
                }
            }

            _db.Curricula.Add(curr);
            await _db.SaveChangesAsync(cancellationToken);

            // store key; layout will localize
            TempData["Success"] = "Curriculum.Created";
            return RedirectToAction(nameof(Details), new { id = curr.Id });
        }

        // GET: Admin/Curricula/Details/5
        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken = default)
        {
            ViewData["ActivePage"] = "Curricula";

            var curriculum = await _db.Curricula
                                     .Include(c => c.Level)
                                     .AsNoTracking()
                                     .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            if (curriculum == null)
            {
                TempData["Error"] = "Curriculum.NotFound";
                return RedirectToAction("Index");
            }

            if (!string.IsNullOrEmpty(curriculum.CoverImageKey))
            {
                try
                {
                    curriculum.CoverImageUrl = await _fs.GetPublicUrlAsync(curriculum.CoverImageKey);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to get public url for curriculum {CurriculumId}", id);
                    curriculum.CoverImageUrl = null;
                }
            }

            // Load modules + their lessons + lesson files (module-first)
            var modules = await _db.SchoolModules
                                   .AsNoTracking()
                                   .Where(m => m.CurriculumId == id)
                                   .OrderBy(m => m.Order)
                                   .Include(m => m.SchoolLessons)
                                       .ThenInclude(sl => sl.Files)
                                   .ToListAsync(cancellationToken);

            // Load lessons that are directly under the curriculum (module-less)
            var directLessons = await _db.SchoolLessons
                                         .AsNoTracking()
                                         .Where(l => l.CurriculumId == id && l.ModuleId == null)
                                         .OrderBy(l => l.Order)
                                         .Include(l => l.Files)
                                         .ToListAsync(cancellationToken);

            var vm = new CurriculumDetailsViewModel
            {
                Curriculum = curriculum,
                Modules = modules,
                DirectLessons = directLessons
            };

            // Provide localized mapping for levels if view needs it
            await PopulateLevelsAsync(curriculum.LevelId, cancellationToken);

            return View(vm);
        }

        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken = default)
        {
            ViewData["ActivePage"] = "Curricula";

            var curr = await _db.Curricula.FindAsync(new object[] { id }, cancellationToken);
            if (curr == null)
            {
                TempData["Error"] = "Curriculum.NotFound";
                return RedirectToAction("Index");
            }

            await PopulateLevelsAsync(curr.LevelId, cancellationToken);

            var vm = new CurriculumEditViewModel
            {
                Id = curr.Id,
                Title = curr.Title,
                Description = curr.Description,
                LevelId = curr.LevelId,
                Order = curr.Order,
                ExistingCoverUrl = !string.IsNullOrEmpty(curr.CoverImageKey) ? await SafeGetPublicUrlAsync(curr.CoverImageKey) : curr.CoverImageUrl
            };

            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(CurriculumEditViewModel vm, CancellationToken cancellationToken = default)
        {
            await PopulateLevelsAsync(vm.LevelId, cancellationToken);

            if (!ModelState.IsValid) return View(vm);

            var curr = await _db.Curricula.FindAsync(new object[] { vm.Id }, cancellationToken);
            if (curr == null)
            {
                TempData["Error"] = "Curriculum.NotFound";
                return RedirectToAction("Index");
            }

            curr.Title = vm.Title;
            curr.Description = vm.Description;
            curr.LevelId = vm.LevelId;
            curr.Order = vm.Order;

            if (vm.CoverImage != null)
            {
                // delete old key if exists
                if (!string.IsNullOrEmpty(curr.CoverImageKey))
                {
                    try { await _fs.DeleteFileAsync(curr.CoverImageKey); } catch (Exception ex) { _logger.LogDebug(ex, "Failed deleting old cover image"); }
                }

                try
                {
                    var newKey = await _fs.SaveFileAsync(vm.CoverImage, "curricula");
                    curr.CoverImageKey = newKey;
                    curr.CoverImageUrl = await SafeGetPublicUrlAsync(newKey);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save new cover image for curriculum {CurriculumId}", curr.Id);
                    // keep previous key/url cleared if save failed
                    curr.CoverImageKey = null;
                    curr.CoverImageUrl = null;
                }
            }

            _db.Curricula.Update(curr);
            await _db.SaveChangesAsync(cancellationToken);

            TempData["Success"] = "Curriculum.Updated";
            return RedirectToAction(nameof(Details), new { id = curr.Id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default)
        {
            // verify existence
            var curriculum = await _db.Curricula.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            if (curriculum == null)
            {
                TempData["Error"] = "Curriculum.NotFound";
                return RedirectToAction("Index");
            }

            // check for any modules or lessons referencing this curriculum (efficient DB checks)
            var hasModules = await _db.SchoolModules.AsNoTracking().AnyAsync(m => m.CurriculumId == id, cancellationToken);
            var hasLessons = await _db.SchoolLessons.AsNoTracking().AnyAsync(l => l.CurriculumId == id, cancellationToken);

            if (hasModules || hasLessons)
            {
                TempData["Error"] = "Curriculum.DeleteHasChildren";
                return RedirectToAction("Details", new { id });
            }

            try
            {
                var toDelete = new Curriculum { Id = id };
                _db.Curricula.Attach(toDelete);
                _db.Curricula.Remove(toDelete);
                await _db.SaveChangesAsync(cancellationToken);

                TempData["Success"] = "Curriculum.DeletedSuccess";
                return RedirectToAction("Index");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Delete failed for curriculum {CurriculumId}", id);
                TempData["Error"] = "Curriculum.DeleteFailed";
                return RedirectToAction("Details", new { id });
            }
        }

        // Helper that tries to get public URL and returns null on failure
        private async Task<string?> SafeGetPublicUrlAsync(string key)
        {
            try
            {
                return await _fs.GetPublicUrlAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "GetPublicUrlAsync failed for key {Key}", key);
                return null;
            }
        }
    }
}




