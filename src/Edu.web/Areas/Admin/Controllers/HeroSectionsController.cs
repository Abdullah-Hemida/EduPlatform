using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Web.Areas.Admin.ViewModels;
using Edu.Web.Resources;
using Edu.Web.Views.Shared.Components.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.EntityFrameworkCore;

namespace Edu.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class HeroSectionsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fileStorage;
        private readonly IHeroService _heroService;
        private readonly IStringLocalizer<SharedResource> _L;

        public HeroSectionsController(ApplicationDbContext db, IFileStorageService fileStorage, IHeroService heroService, IStringLocalizer<SharedResource> localizer)
        {
            _db = db;
            _fileStorage = fileStorage;
            _heroService = heroService;
            _L = localizer;
        }

        // GET: Index
        public async Task<IActionResult> Index()
        {
            var list = await _db.HeroSections.OrderBy(h => h.Placement).ThenBy(h => h.Order).ToListAsync();
            return View(list);
        }

        // GET: Create
        public IActionResult Create(HeroPlacement? placement)
        {
            var vm = new AdminHeroVm { Placement = placement ?? HeroPlacement.Home };
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AdminHeroVm vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var ent = new HeroSection
            {
                Placement = vm.Placement,
                TitleEn = vm.TitleEn,
                TitleIt = vm.TitleIt,
                TitleAr = vm.TitleAr,
                DescriptionEn = vm.DescriptionEn,
                DescriptionIt = vm.DescriptionIt,
                DescriptionAr = vm.DescriptionAr,
                IsActive = vm.IsActive,
                Order = vm.Order
            };

            // upload image if provided
            if (vm.ImageFile != null && vm.ImageFile.Length > 0)
            {
                var storageKey = await _fileStorage.SaveFileAsync(vm.ImageFile, "hero");
                ent.ImageStorageKey = storageKey;
            }
            await _heroService.InvalidateCacheAsync(vm.Placement);
            _db.HeroSections.Add(ent);
            await _db.SaveChangesAsync();

            await _heroService.InvalidateCacheAsync(ent.Placement);

            TempData["Success"] = _L["Admin.CreateSuccess"].Value;
            return RedirectToAction(nameof(Index));
        }

        // GET: Edit
        public async Task<IActionResult> Edit(int id)
        {
            var ent = await _db.HeroSections.FindAsync(id);
            if (ent == null) return NotFound();

            var vm = new AdminHeroVm
            {
                Id = ent.Id,
                Placement = ent.Placement,
                ImageStorageKey = ent.ImageStorageKey,
                TitleEn = ent.TitleEn,
                TitleIt = ent.TitleIt,
                TitleAr = ent.TitleAr,
                DescriptionEn = ent.DescriptionEn,
                DescriptionIt = ent.DescriptionIt,
                DescriptionAr = ent.DescriptionAr,
                IsActive = ent.IsActive,
                Order = ent.Order
            };
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(AdminHeroVm vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var ent = await _db.HeroSections.FindAsync(vm.Id);
            if (ent == null) return NotFound();

            ent.TitleEn = vm.TitleEn;
            ent.TitleIt = vm.TitleIt;
            ent.TitleAr = vm.TitleAr;
            ent.DescriptionEn = vm.DescriptionEn;
            ent.DescriptionIt = vm.DescriptionIt;
            ent.DescriptionAr = vm.DescriptionAr;
            ent.IsActive = vm.IsActive;
            ent.Order = vm.Order;
            ent.UpdatedAtUtc = DateTime.UtcNow;

            if (vm.ImageFile != null && vm.ImageFile.Length > 0)
            {
                // optionally delete old file via IFileStorageService if supported
                if (!string.IsNullOrEmpty(ent.ImageStorageKey))
                {
                    await _fileStorage.DeleteFileAsync(ent.ImageStorageKey);
                }
                var storageKey = await _fileStorage.SaveFileAsync(vm.ImageFile, "hero");
                ent.ImageStorageKey = storageKey;
            }
            await _heroService.InvalidateCacheAsync(vm.Placement);
            _db.HeroSections.Update(ent);
            await _db.SaveChangesAsync();

            await _heroService.InvalidateCacheAsync(ent.Placement);

            TempData["Success"] = _L["Admin.UpdateSuccess"].Value;
            return RedirectToAction(nameof(Index));
        }

        // GET: Delete
        public async Task<IActionResult> Delete(int id)
        {
            var ent = await _db.HeroSections.FindAsync(id);
            if (ent == null) return NotFound();
            return View(ent);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var ent = await _db.HeroSections.FindAsync(id);
            if (ent == null) return NotFound();

            if (!string.IsNullOrEmpty(ent.ImageStorageKey))
            {
                await _fileStorage.DeleteFileAsync(ent.ImageStorageKey);
            }

            await _heroService.InvalidateCacheAsync(ent.Placement);

            _db.HeroSections.Remove(ent);
            await _db.SaveChangesAsync();

            await _heroService.InvalidateCacheAsync(ent.Placement);

            TempData["Success"] = _L["Admin.DeleteSuccess"].Value;
            return RedirectToAction(nameof(Index));
        }
    }
}
