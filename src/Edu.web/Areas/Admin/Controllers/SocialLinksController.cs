using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Web.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Edu.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class SocialLinksController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public SocialLinksController(ApplicationDbContext db, IStringLocalizer<SharedResource> localizer)
        {
            _db = db;
            _localizer = localizer;
        }

        public async Task<IActionResult> Index()
        {
            var list = await _db.SocialLinks.OrderBy(s => s.Order).ToListAsync();
            return View(list);
        }

        public IActionResult Create() => View(new SocialLink { IsVisible = true });

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SocialLink model)
        {
            if (!ModelState.IsValid) return View(model);
            _db.SocialLinks.Add(model);
            await _db.SaveChangesAsync();
            TempData["Success"] = _localizer["Admin.CreateSuccess"].Value;
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var item = await _db.SocialLinks.FindAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(SocialLink model)
        {
            if (!ModelState.IsValid) return View(model);
            var item = await _db.SocialLinks.FindAsync(model.Id);
            if (item == null) return NotFound();
            item.Provider = model.Provider;
            item.Url = model.Url;
            item.IsVisible = model.IsVisible;
            item.Order = model.Order;
            await _db.SaveChangesAsync();
            TempData["Success"] = _localizer["Admin.UpdateSuccess"].Value;
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            var item = await _db.SocialLinks.FindAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _db.SocialLinks.FindAsync(id);
            if (item == null) return NotFound();
            _db.SocialLinks.Remove(item);
            await _db.SaveChangesAsync();
            TempData["Success"] = _localizer["Admin.DeleteSuccess"].Value;
            return RedirectToAction(nameof(Index));
        }
    }
}


