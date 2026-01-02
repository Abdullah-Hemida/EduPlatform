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
    public class FooterContactsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public FooterContactsController(ApplicationDbContext db, IStringLocalizer<SharedResource> localizer)
        {
            _db = db;
            _localizer = localizer;
        }

        public async Task<IActionResult> Index(string culture = "it")
        {
            ViewData["ActivePage"] = "FooterContacts";
            ViewData["Culture"] = culture;
            var list = await _db.FooterContacts
                .Where(c => c.Culture == culture)
                .OrderBy(c => c.Order)
                .ToListAsync();
            return View(list);
        }

        public IActionResult Create(string culture = "it")
        {
            ViewData["ActivePage"] = "FooterContacts";
            var vm = new FooterContact { Culture = culture, Order = 0 };
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FooterContact model)
        {
            ViewData["ActivePage"] = "FooterContacts";
            if (!ModelState.IsValid) return View(model);

            _db.FooterContacts.Add(model);
            await _db.SaveChangesAsync();
            TempData["Success"] = _localizer["Admin.CreateSuccess"].Value;
            return RedirectToAction(nameof(Index), new { culture = model.Culture });
        }

        public async Task<IActionResult> Edit(int id)
        {
            ViewData["ActivePage"] = "FooterContacts";
            var item = await _db.FooterContacts.FindAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(FooterContact model)
        {
            ViewData["ActivePage"] = "FooterContacts";
            if (!ModelState.IsValid) return View(model);

            var item = await _db.FooterContacts.FindAsync(model.Id);
            if (item == null) return NotFound();

            item.PersonName = model.PersonName;
            item.Position = model.Position;
            item.Contact = model.Contact;
            item.Order = model.Order;
            item.Culture = model.Culture;

            await _db.SaveChangesAsync();
            TempData["Success"] = _localizer["Admin.UpdateSuccess"].Value;
            return RedirectToAction(nameof(Index), new { culture = model.Culture });
        }

        public async Task<IActionResult> Delete(int id)
        {
            ViewData["ActivePage"] = "FooterContacts";
            var item = await _db.FooterContacts.FindAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            ViewData["ActivePage"] = "FooterContacts";
            var item = await _db.FooterContacts.FindAsync(id);
            if (item == null) return NotFound();
            var culture = item.Culture;
            _db.FooterContacts.Remove(item);
            await _db.SaveChangesAsync();
            TempData["Success"] = _localizer["Admin.DeleteSuccess"].Value;
            return RedirectToAction(nameof(Index), new { culture });
        }
    }
}

