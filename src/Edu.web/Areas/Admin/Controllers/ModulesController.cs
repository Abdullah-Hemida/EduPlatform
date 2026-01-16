using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Web.Areas.Admin.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Edu.Application.IServices;

namespace Edu.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ModulesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fileService;
        private readonly ILogger<ModulesController> _logger;

        public ModulesController(ApplicationDbContext db, IFileStorageService fileService, ILogger<ModulesController> logger)
        {
            _db = db;
            _fileService = fileService;
            _logger = logger;
        }

        // GET: Admin/Modules/Create?curriculumId=5
        public IActionResult Create(int curriculumId)
        {
            ViewData["ActivePage"] = "Curricula";
            var vm = new ModuleCreateViewModel { CurriculumId = curriculumId, Order = 1 };
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ModuleCreateViewModel vm, CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid) return View(vm);

            var m = new SchoolModule
            {
                CurriculumId = vm.CurriculumId,
                Title = vm.Title,
                Order = vm.Order
            };
            _db.SchoolModules.Add(m);
            await _db.SaveChangesAsync(cancellationToken);

            TempData["Success"] = "Module.Added";
            return RedirectToAction("Details", "Curricula", new { area = "Admin", id = vm.CurriculumId });
        }

        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken = default)
        {
            ViewData["ActivePage"] = "Curricula";
            var m = await _db.SchoolModules.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (m == null) return NotFound();

            var vm = new ModuleEditViewModel { Id = m.Id, CurriculumId = m.CurriculumId, Title = m.Title, Order = m.Order };
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ModuleEditViewModel vm, CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid) return View(vm);

            var m = await _db.SchoolModules.FindAsync(new object[] { vm.Id }, cancellationToken);
            if (m == null) return NotFound();

            m.Title = vm.Title;
            m.Order = vm.Order;
            _db.SchoolModules.Update(m);
            await _db.SaveChangesAsync(cancellationToken);

            TempData["Success"] = "Module.Updated";
            return RedirectToAction("Details", "Curricula", new { area = "Admin", id = vm.CurriculumId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, int curriculumId, CancellationToken cancellationToken = default)
        {
            var m = await _db.SchoolModules.FindAsync(new object[] { id }, cancellationToken);
            if (m == null) return NotFound();

            try
            {
                // Load lessons and their files in one query
                var lessons = await _db.SchoolLessons
                    .Where(l => l.ModuleId == id)
                    .Include(l => l.Files)
                    .ToListAsync(cancellationToken);

                // Collect file resources to remove
                var filesToRemove = lessons.SelectMany(l => l.Files ?? Enumerable.Empty<FileResource>()).ToList();

                // Attempt to delete each underlying storage item (best-effort)
                foreach (var f in filesToRemove)
                {
                    try
                    {
                        var keyOrUrl = !string.IsNullOrWhiteSpace(f.StorageKey) ? f.StorageKey : f.FileUrl;
                        if (!string.IsNullOrWhiteSpace(keyOrUrl))
                        {
                            await _fileService.DeleteFileAsync(keyOrUrl);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed deleting storage object for FileResource {FileResourceId}", f.Id);
                        // continue: we still remove DB rows
                    }
                }

                // Remove file resource rows in bulk
                if (filesToRemove.Any())
                {
                    _db.FileResources.RemoveRange(filesToRemove);
                }

                // Remove lessons
                if (lessons.Any())
                {
                    _db.SchoolLessons.RemoveRange(lessons);
                }

                // Remove module
                _db.SchoolModules.Remove(m);

                // Save all changes in one transaction
                await _db.SaveChangesAsync(cancellationToken);

                TempData["Success"] = "Module.Deleted";
                return RedirectToAction("Details", "Curricula", new { area = "Admin", id = curriculumId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed deleting module {ModuleId}", id);
                TempData["Error"] = "Module.DeleteFailed";
                return RedirectToAction("Details", "Curricula", new { area = "Admin", id = curriculumId });
            }
        }
    }
}


