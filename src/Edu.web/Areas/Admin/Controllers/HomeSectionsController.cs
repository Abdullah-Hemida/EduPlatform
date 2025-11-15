using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Web.Areas.Admin.ViewModels;
using Edu.Web.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Edu.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class HomeSectionsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _files;
        private readonly IStringLocalizer<SharedResource> _localizer;

        // cultures to support — keep in same order used in UI (en,it,ar)
        private readonly string[] _cultures = new[] { "en", "it", "ar" };

        public HomeSectionsController(
            ApplicationDbContext db,
            IFileStorageService files,
            IStringLocalizer<SharedResource> localizer)
        {
            _db = db;
            _files = files;
            _localizer = localizer;
        }
        // Index (if not present)
        public async Task<IActionResult> Index()
        {
            ViewData["ActivePage"] = "HomeSections";
            var list = await _db.HomeSections
                .Include(s => s.Translations)
                .Include(s => s.Items)
                .OrderBy(s => s.Order)
                .AsNoTracking()
                .ToListAsync();

            return View(list);
        }

        // --- CREATE GET: shows a simple create form (Order) ---
        public IActionResult Create()
        {
            ViewData["ActivePage"] = "HomeSections";
            // show minimal create page; admin will be redirected to Edit afterwards
            return View();
        }

        // --- CREATE POST: creates record and initial translations then redirect to Edit ---
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int order = 0)
        {
            ViewData["ActivePage"] = "HomeSections";

            // If order not specified, get max existing + 1
            if (order <= 0)
                order = await _db.HomeSections.AnyAsync()
                    ? await _db.HomeSections.MaxAsync(s => s.Order) + 1
                    : 1;

            var section = new HomeSection
            {
                Order = order
            };

            foreach (var c in _cultures)
            {
                section.Translations.Add(new HomeSectionTranslation
                {
                    Culture = c,
                    Title = "",
                    Subtitle = ""
                });
            }

            _db.HomeSections.Add(section);
            await _db.SaveChangesAsync();

            TempData["Success"] = _localizer["Admin.CreateSuccess"] ?? "Section created";
            return RedirectToAction(nameof(Edit), new { id = section.Id });
        }

        // --- DELETE GET: show a confirmation ---
        public async Task<IActionResult> Delete(int id)
        {
            ViewData["ActivePage"] = "HomeSections";
            var sec = await _db.HomeSections
                .Include(s => s.Translations)
                .Include(s => s.Items).ThenInclude(i => i.Translations)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (sec == null) return NotFound();

            return View(sec);
        }

        // --- DELETE POST: actually remove (and delete files) ---
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            ViewData["ActivePage"] = "HomeSections";
            var sec = await _db.HomeSections
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (sec == null) return NotFound();

            // delete section image
            if (!string.IsNullOrEmpty(sec.ImageStorageKey))
            {
                try { await _files.DeleteFileAsync(sec.ImageStorageKey); } catch { /* swallow */ }
            }

            // delete item images
            foreach (var it in sec.Items)
            {
                if (!string.IsNullOrEmpty(it.ImageStorageKey))
                {
                    try { await _files.DeleteFileAsync(it.ImageStorageKey); } catch { }
                }
            }

            _db.HomeSections.Remove(sec);
            await _db.SaveChangesAsync();

            TempData["Success"] = _localizer["Admin.DeleteSuccess"] ?? "Deleted";
            return RedirectToAction(nameof(Index));
        }
        // GET: Admin/HomeSections/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            ViewData["ActivePage"] = "HomeSections";
            var section = await _db.HomeSections
                .Include(s => s.Translations)
                .Include(s => s.Items).ThenInclude(i => i.Translations)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id);

            if (section == null) return NotFound();

            var vm = new HomeSectionEditVm
            {
                Id = section.Id,
                Order = section.Order,
                ExistingSectionImageKey = section.ImageStorageKey
            };

            if (!string.IsNullOrEmpty(section.ImageStorageKey))
            {
                vm.ExistingSectionImageUrl = await _files.GetPublicUrlAsync(section.ImageStorageKey);
            }

            // ensure one translation vm per culture (fallback to existing)
            foreach (var c in _cultures)
            {
                var t = section.Translations.FirstOrDefault(x => x.Culture == c);
                vm.Translations.Add(new HomeSectionTranslationVm
                {
                    Culture = c,
                    Title = t?.Title,
                    Subtitle = t?.Subtitle
                });
            }

            // items
            foreach (var item in section.Items.OrderBy(i => i.Order))
            {
                var itemVm = new HomeSectionItemEditVm
                {
                    Id = item.Id,
                    Order = item.Order,
                    ExistingImageKey = item.ImageStorageKey,
                    LinkUrl = item.LinkUrl,
                    Text = item.Text // fallback
                };
                if (!string.IsNullOrEmpty(item.ImageStorageKey))
                {
                    itemVm.ExistingImageUrl = await _files.GetPublicUrlAsync(item.ImageStorageKey);
                }

                // translations for item
                foreach (var c in _cultures)
                {
                    var it = item.Translations.FirstOrDefault(x => x.Culture == c);
                    itemVm.Translations.Add(new HomeSectionItemTranslationVm
                    {
                        Culture = c,
                        Text = it?.Text
                    });
                }

                vm.Items.Add(itemVm);
            }

            return View(vm);
        }

        // POST: Admin/HomeSections/Edit/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(HomeSectionEditVm vm)
        {
            ViewData["ActivePage"] = "HomeSections";
            if (!ModelState.IsValid)
            {
                // reload urls for previews
                if (!string.IsNullOrEmpty(vm.ExistingSectionImageKey)) vm.ExistingSectionImageUrl = await _files.GetPublicUrlAsync(vm.ExistingSectionImageKey);
                foreach (var it in vm.Items)
                {
                    if (!string.IsNullOrEmpty(it.ExistingImageKey)) it.ExistingImageUrl = await _files.GetPublicUrlAsync(it.ExistingImageKey);
                }
                return View(vm);
            }

            var section = await _db.HomeSections
                .Include(s => s.Translations)
                .Include(s => s.Items).ThenInclude(i => i.Translations)
                .FirstOrDefaultAsync(s => s.Id == vm.Id);

            if (section == null) return NotFound();

            // Update basic section props
            section.Order = vm.Order;

            // Section image handling:
            // If admin uploaded new file -> save and replace; if RemoveSectionImage checkbox posted (we expect name RemoveSectionImage on form),
            // but this vm doesn't contain RemoveSectionImage field to avoid overcomplication; we'll detect if existing key present and no file uploaded and maybe a remove flag input.
            // Let's try to read form directly for RemoveSectionImage
            var removeSectionImageFlag = Request.Form["RemoveSectionImage"].FirstOrDefault() == "true";

            var sectionImageFile = Request.Form.Files["SectionImageFile"];
            if (sectionImageFile != null && sectionImageFile.Length > 0)
            {
                // save new
                var newKey = await _files.SaveFileAsync(sectionImageFile, $"home/sections/{section.Id}");
                // remove old if exists
                if (!string.IsNullOrEmpty(section.ImageStorageKey))
                {
                    try { await _files.DeleteFileAsync(section.ImageStorageKey); } catch { /* swallow */ }
                }
                section.ImageStorageKey = newKey;
            }
            else if (removeSectionImageFlag && !string.IsNullOrEmpty(section.ImageStorageKey))
            {
                // remove existing
                try { await _files.DeleteFileAsync(section.ImageStorageKey); } catch { }
                section.ImageStorageKey = null;
            }
            // else keep existing

            // Translations: update or create
            foreach (var tVm in vm.Translations)
            {
                var trans = section.Translations.FirstOrDefault(x => x.Culture == tVm.Culture);
                if (trans == null)
                {
                    trans = new HomeSectionTranslation
                    {
                        Culture = tVm.Culture,
                        Title = tVm.Title,
                        Subtitle = tVm.Subtitle
                    };
                    section.Translations.Add(trans);
                }
                else
                {
                    trans.Title = tVm.Title;
                    trans.Subtitle = tVm.Subtitle;
                }
            }

            // Handle items: We'll reconcile posted vm.Items with DB items.
            // Approach:
            //  - For each posted item vm: if Id==0 -> create new item
            //  - If Id>0 -> update existing item
            //  - If any DB item not present in posted list -> remove it
            var postedIds = vm.Items.Where(i => i.Id != 0).Select(i => i.Id).ToHashSet();

            // Remove missing DB items
            var toRemove = section.Items.Where(i => !postedIds.Contains(i.Id)).ToList();
            foreach (var rem in toRemove)
            {
                // delete file if present
                if (!string.IsNullOrEmpty(rem.ImageStorageKey))
                {
                    try { await _files.DeleteFileAsync(rem.ImageStorageKey); } catch { }
                }
                _db.HomeSectionItems.Remove(rem);
            }

            // Process posted items
            for (int idx = 0; idx < vm.Items.Count; idx++)
            {
                var itemVm = vm.Items[idx];
                HomeSectionItem itemEntity = null;
                if (itemVm.Id == 0)
                {
                    itemEntity = new HomeSectionItem
                    {
                        Order = itemVm.Order,
                        LinkUrl = itemVm.LinkUrl,
                        Text = itemVm.Text,
                        HomeSectionId = section.Id
                    };
                    section.Items.Add(itemEntity);
                }
                else
                {
                    itemEntity = section.Items.FirstOrDefault(x => x.Id == itemVm.Id);
                    if (itemEntity == null) continue; // safe guard
                    itemEntity.Order = itemVm.Order;
                    itemEntity.LinkUrl = itemVm.LinkUrl;
                    itemEntity.Text = itemVm.Text;
                }

                // Image handling per item: posted file name is Items[index].ImageFile; model binder also populated vm.ImageFile so prefer that
                // But depending on binding you might need to read Request.Form.Files with the specific name
                // Try to get file by the input name in Request.Files (name pattern Items[{index}].ImageFile)
                string fileKeyName = $"Items[{idx}].ImageFile";
                var postedFile = Request.Form.Files[fileKeyName] ?? itemVm.ImageFile;

                if (postedFile != null && postedFile.Length > 0)
                {
                    // upload and replace
                    var newKey = await _files.SaveFileAsync(postedFile, $"home/sections/{section.Id}/items");
                    if (!string.IsNullOrEmpty(itemEntity.ImageStorageKey))
                    {
                        try { await _files.DeleteFileAsync(itemEntity.ImageStorageKey); } catch { }
                    }
                    itemEntity.ImageStorageKey = newKey;
                }
                else
                {
                    // Remove image if RemoveImage checkbox posted
                    var removeFlagName = $"Items[{idx}].RemoveImage";
                    var removeItemImage = Request.Form[removeFlagName].FirstOrDefault() == "true" || itemVm.RemoveImage;
                    if (removeItemImage && !string.IsNullOrEmpty(itemEntity.ImageStorageKey))
                    {
                        try { await _files.DeleteFileAsync(itemEntity.ImageStorageKey); } catch { }
                        itemEntity.ImageStorageKey = null;
                    }
                }

                // Handle item translations (vm contains a list per item)
                foreach (var itTransVm in itemVm.Translations)
                {
                    var itemTrans = itemEntity.Translations.FirstOrDefault(t => t.Culture == itTransVm.Culture);
                    if (itemTrans == null)
                    {
                        itemTrans = new HomeSectionItemTranslation
                        {
                            Culture = itTransVm.Culture,
                            Text = itTransVm.Text
                        };
                        itemEntity.Translations.Add(itemTrans);
                    }
                    else
                    {
                        itemTrans.Text = itTransVm.Text;
                    }
                }
            }

            // Persist changes
            await _db.SaveChangesAsync();

            TempData["Success"] = _localizer["Admin.UpdateSuccess"] ?? "Saved";

            return RedirectToAction(nameof(Edit), new { id = section.Id });
        }
        // Toggle active/visible flag on section
        //[HttpPost, ValidateAntiForgeryToken]
        //public async Task<IActionResult> ToggleActive(int id)
        //{
        //    var sec = await _db.HomeSections.FirstOrDefaultAsync(s => s.Id == id);
        //    if (sec == null) return NotFound();

        //    sec.IsActive = !sec.IsActive;
        //    await _db.SaveChangesAsync();

        //    TempData["Success"] = sec.IsActive ? _localizer["Admin.Activated"] ?? "Activated" : _localizer["Admin.Deactivated"] ?? "Deactivated";
        //    return RedirectToAction(nameof(Index));
        //}

        //// Optional explicit activate
        //[HttpPost, ValidateAntiForgeryToken]
        //public async Task<IActionResult> Activate(int id)
        //{
        //    var sec = await _db.HomeSections.FirstOrDefaultAsync(s => s.Id == id);
        //    if (sec == null) return NotFound();

        //    sec.IsActive = true;
        //    await _db.SaveChangesAsync();
        //    TempData["Success"] = _localizer["Admin.Activated"] ?? "Activated";
        //    return RedirectToAction(nameof(Index));
        //}
    }
}

