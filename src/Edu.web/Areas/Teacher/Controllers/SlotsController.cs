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
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _um = um ?? throw new ArgumentNullException(nameof(um));
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        }

        // GET: Teacher/Slots
        public async Task<IActionResult> Index()
        {
            var teacherId = _um.GetUserId(User);
            var nowUtc = DateTime.UtcNow;

            // Project only the fields we need (avoid pulling full Booking entities)
            var slotsProjection = await _db.Slots
                .AsNoTracking()
                .Where(s => s.TeacherId == teacherId && s.EndUtc >= nowUtc)
                .OrderBy(s => s.StartUtc)
                .Select(s => new
                {
                    s.Id,
                    s.StartUtc,
                    s.EndUtc,
                    s.Capacity,
                    s.Price,
                    s.LocationUrl,
                    // Count bookings that consume seats (Pending or Paid — adjust policy if needed)
                    OccupiedSeats = s.Bookings.Count(b => b.Status == BookingStatus.Pending || b.Status == BookingStatus.Paid)
                })
                .ToListAsync();

            var vm = slotsProjection.Select(s => new SlotListItemVm
            {
                Id = s.Id,
                StartLocal = s.StartUtc.ToLocalTime(),
                EndLocal = s.EndUtc.ToLocalTime(),
                Capacity = s.Capacity,
                AvailableSeats = Math.Max(0, s.Capacity - s.OccupiedSeats),
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
                ModelState.AddModelError("", _localizer["Slots.StartEndInvalid"].Value ?? "End must be after start");
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

            // store resource key (not localized string)
            TempData["Success"] = "Slots.Created";
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
                ModelState.AddModelError("", _localizer["Slots.StartEndInvalid"].Value ?? "End must be after start");
                return View(vm);
            }

            var teacherId = _um.GetUserId(User);
            var slot = await _db.Slots.FirstOrDefaultAsync(s => s.Id == vm.Id && s.TeacherId == teacherId);

            if (slot == null) return NotFound();

            // concurrency: check RowVersion (byte[]), show localized message on conflict
            if (vm.RowVersion != null && slot.RowVersion != null && !vm.RowVersion.SequenceEqual(slot.RowVersion))
            {
                ModelState.AddModelError("", _localizer["ConcurrencyError"].Value ?? "The record was changed by someone else.");
                return View(vm);
            }

            slot.StartUtc = vm.StartLocal.ToUniversalTime();
            slot.EndUtc = vm.EndLocal.ToUniversalTime();
            slot.Capacity = vm.Capacity;
            slot.Price = vm.Price;
            slot.LocationUrl = vm.LocationUrl;

            _db.Slots.Update(slot);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Slots.Updated";
            return RedirectToAction(nameof(Index));
        }

        // POST: Teacher/Slots/Delete/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var teacherId = _um.GetUserId(User);

            // count active bookings directly (avoid loading bookings collection)
            var activeBookings = await _db.Bookings
                .AsNoTracking()
                .CountAsync(b => b.SlotId == id && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Paid));

            if (activeBookings > 0)
            {
                TempData["Error"] = "Slot.DeleteHasAcceptedBookings";
                return RedirectToAction(nameof(Index));
            }

            var slot = await _db.Slots.FirstOrDefaultAsync(s => s.Id == id && s.TeacherId == teacherId);
            if (slot == null) return NotFound();

            _db.Slots.Remove(slot);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Slots.Deleted";
            return RedirectToAction(nameof(Index));
        }
    }
}



//https://www.youtube.com/shorts/a5Xg1R7tLH0?feature=share