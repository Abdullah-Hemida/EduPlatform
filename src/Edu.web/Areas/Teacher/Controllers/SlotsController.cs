using Edu.Infrastructure.Data;
using Edu.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Edu.Web.Areas.Teacher.ViewModels;
using Microsoft.Extensions.Localization;
using Edu.Web.Resources;
using Edu.Infrastructure.Helpers;

namespace Edu.Web.Areas.Teacher.Controllers
{
    [Area("Teacher")]
    [Authorize(Roles = "Teacher")]
    public class SlotsController : TeacherBaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _um;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public SlotsController(ApplicationDbContext db, UserManager<ApplicationUser> um, IStringLocalizer<SharedResource> localizer)
        {
            _db = db;
            _um = um;
            _localizer = localizer;
        }

        // GET: Teacher/Slots
        public async Task<IActionResult> Index()
        {
            var teacherId = _um.GetUserId(User);
            var nowUtc = DateTime.UtcNow;
            var slots = await _db.Slots
                .Where(s => s.TeacherId == teacherId && s.EndUtc >= nowUtc)
                .Include(s => s.Bookings)
                .OrderBy(s => s.StartUtc)
                .ToListAsync();

            var vm = slots.Select(s => new SlotListItemVm
            {
                Id = s.Id,
                StartLocal = s.StartUtc.ToLocalTime(),
                EndLocal = s.EndUtc.ToLocalTime(),
                Capacity = s.Capacity,
                AvailableSeats = s.Capacity,
                PriceLabel = s.Price.ToEuro(),
                LocationUrl = s.LocationUrl
            }).ToList();
            ViewData["ActivePage"] = "Slots";
            return View(vm);
        }

        // GET: Teacher/Slots/Create
        public IActionResult Create()
        {
            var vm = new SlotCreateEditVm
            {
                StartLocal = DateTime.Now.AddHours(1),
                EndLocal = DateTime.Now.AddHours(2),
                Capacity = 1
            };
            return View(vm);
        }

        // POST: Teacher/Slots/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SlotCreateEditVm vm)
        {
            if (!ModelState.IsValid) return View(vm);

            if (vm.EndLocal <= vm.StartLocal)
            {
                ModelState.AddModelError("", _localizer["Slots.StartEndInvalid"] ?? "End must be after start");
                return View(vm);
            }

            var teacherId = _um.GetUserId(User);

            var slot = new Slot
            {
                TeacherId = teacherId,
                StartUtc = vm.StartLocal.ToUniversalTime(),
                EndUtc = vm.EndLocal.ToUniversalTime(),
                Capacity = vm.Capacity,
                Price = vm.Price,
                LocationUrl = vm.LocationUrl
            };

            _db.Slots.Add(slot);
            await _db.SaveChangesAsync();

            TempData["Success"] = _localizer["Slots.Created"] ?? "Slot created.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Teacher/Slots/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var teacherId = _um.GetUserId(User);
            var slot = await _db.Slots.FirstOrDefaultAsync(s => s.Id == id && s.TeacherId == teacherId);
            if (slot == null) return NotFound();

            var vm = new SlotCreateEditVm
            {
                Id = slot.Id,
                StartLocal = slot.StartUtc.ToLocalTime(),
                EndLocal = slot.EndUtc.ToLocalTime(),
                Capacity = slot.Capacity,
                Price = slot.Price,
                LocationUrl = slot.LocationUrl,
                RowVersion = slot.RowVersion
            };
            ViewData["ActivePage"] = "Slots";
            return View(vm);
        }

        // POST: Teacher/Slots/Edit/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(SlotCreateEditVm vm)
        {
            if (!ModelState.IsValid) return View(vm);

            if (vm.EndLocal <= vm.StartLocal)
            {
                ModelState.AddModelError("", _localizer["Slots.StartEndInvalid"] ?? "End must be after start");
                return View(vm);
            }

            var teacherId = _um.GetUserId(User);
            var slot = await _db.Slots.FirstOrDefaultAsync(s => s.Id == vm.Id && s.TeacherId == teacherId);

            if (slot == null) return NotFound();

            // concurrency: check RowVersion
            if (vm.RowVersion != null && slot.RowVersion != null && !vm.RowVersion.SequenceEqual(slot.RowVersion))
            {
                ModelState.AddModelError("", _localizer["ConcurrencyError"]);
                return View(vm);
            }

            slot.StartUtc = vm.StartLocal.ToUniversalTime();
            slot.EndUtc = vm.EndLocal.ToUniversalTime();
            slot.Capacity = vm.Capacity;
            slot.Price = vm.Price;
            slot.LocationUrl = vm.LocationUrl;

            _db.Slots.Update(slot);
            await _db.SaveChangesAsync();

            TempData["Success"] = _localizer["Slots.Updated"] ?? "Slot updated.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Teacher/Slots/Delete/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var teacherId = _um.GetUserId(User);
            var slot = await _db.Slots.Include(s => s.Bookings).FirstOrDefaultAsync(s => s.Id == id && s.TeacherId == teacherId);
            if (slot == null) return NotFound();

            // don't allow delete if there are accepted (or pending?) bookings depending on policy
            var activeBookings = slot.Bookings.Count(b => b.Status == BookingStatus.Pending);
            if (activeBookings > 0)
            {
                TempData["Error"] = _localizer["Slot.DeleteHasAcceptedBookings"] ?? "Cannot delete slot with accepted or pending bookings.";
                return RedirectToAction(nameof(Index));
            }

            _db.Slots.Remove(slot);
            await _db.SaveChangesAsync();

            TempData["Success"] = _localizer["Slots.Deleted"] ?? "Slot deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}


