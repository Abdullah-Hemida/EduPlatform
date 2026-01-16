// File: Areas/Student/Controllers/BookingsController.cs
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Web.Areas.Shared.ViewModels;
using Edu.Web.Helpers;
using Edu.Web.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Edu.Web.Areas.Student.Controllers
{
    [Area("Student")]
    [Authorize(Roles = "Student")]
    public class BookingsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStringLocalizer<SharedResource> _L;
        private readonly INotificationService _notifier;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<BookingsController> _logger;

        public BookingsController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IStringLocalizer<SharedResource> localizer,
            INotificationService notifier,
            IWebHostEnvironment env,
            ILogger<BookingsController> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _L = localizer ?? throw new ArgumentNullException(nameof(localizer));
            _notifier = notifier;
            _env = env;
            _logger = logger;
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateBookingVm vm)
        {
            if (vm == null || vm.SlotId <= 0)
            {
                TempData["Error"] = "Admin.OperationFailed";
                return RedirectToAction("Index", "Slots");
            }

            // Load only slot metadata (avoid loading whole bookings collection)
            var slot = await _db.Slots
                .AsNoTracking()
                .Where(s => s.Id == vm.SlotId)
                .Select(s => new
                {
                    s.Id,
                    s.Capacity,
                    s.TeacherId,
                    s.LocationUrl,
                    s.Price
                })
                .FirstOrDefaultAsync();

            if (slot == null)
            {
                TempData["Error"] = "Slot.NotFound";
                return RedirectToAction("Index", "Slots");
            }

            // Count occupied seats from DB (Pending or Paid)
            var occupied = await _db.Bookings
                .AsNoTracking()
                .CountAsync(b => b.SlotId == vm.SlotId && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Paid));

            var available = slot.Capacity - occupied;
            if (available <= 0)
            {
                TempData["Error"] = "Slot.AlreadyBooked";
                return RedirectToAction("Index", "Slots");
            }

            var studentId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(studentId)) return Challenge();

            var booking = new Booking
            {
                SlotId = vm.SlotId,
                StudentId = studentId,
                TeacherId = slot.TeacherId,
                MeetUrl = slot.LocationUrl,
                RequestedDateUtc = DateTime.UtcNow,
                Notes = vm.Notes,
                Status = BookingStatus.Pending,
                Price = slot.Price
            };

            try
            {
                // Double-check capacity right before save to reduce race window
                var occupiedNow = await _db.Bookings
                    .Where(b => b.SlotId == vm.SlotId && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Paid))
                    .CountAsync();

                if (occupiedNow >= slot.Capacity)
                {
                    TempData["Error"] = "Slot.AlreadyBooked";
                    return RedirectToAction("Index", "Slots");
                }

                _db.Bookings.Add(booking);
                await _db.SaveChangesAsync();

                // create log & notify (notification helper does NOT save changes)
                await CreateBookingLogAndNotifyAsync(booking, "Requested", vm.Notes ?? "Student requested booking");

                TempData["Success"] = "Booking.Requested";
                return RedirectToAction(nameof(MyBookings));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Create booking failed for slot {SlotId}", vm.SlotId);
                TempData["Error"] = "Admin.OperationFailed";
                return RedirectToAction("Index", "Slots");
            }
        }

        // GET: Student/Bookings/MyBookings
        public async Task<IActionResult> MyBookings(string tab = "upcoming", int page = 1, int pageSize = 10)
        {
            var studentId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(studentId)) return Challenge();

            tab = (tab ?? "").ToLowerInvariant();
            if (tab != "upcoming" && tab != "past" && tab != "all") tab = "upcoming";

            if (page <= 0) page = 1;
            if (pageSize <= 0 || pageSize > 100) pageSize = 10;

            var now = DateTime.UtcNow;
            const int joinWindowMinutes = 15;
            const int cancelBeforeMinutes = 60;

            // Project minimal fields and include only what we need
            IQueryable<Booking> baseQuery = _db.Bookings
                .AsNoTracking()
                .Where(b => b.StudentId == studentId)
                .Include(b => b.Slot)
                .Include(b => b.Teacher).ThenInclude(t => t.User);

            if (tab == "upcoming")
                baseQuery = baseQuery.Where(b => b.Slot != null && b.Slot.EndUtc >= now);
            else if (tab == "past")
                baseQuery = baseQuery.Where(b => b.Slot != null && b.Slot.EndUtc < now);

            var total = await baseQuery.CountAsync();

            var ordered = tab == "past"
                ? baseQuery.OrderByDescending(b => b.Slot!.StartUtc)
                : baseQuery.OrderBy(b => b.Slot!.StartUtc);

            var bookings = await ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vm = bookings.Select(b =>
            {
                var startUtc = b.Slot?.StartUtc;
                var endUtc = b.Slot?.EndUtc;

                bool canJoin = false;
                if (b.Status == BookingStatus.Paid && startUtc.HasValue && endUtc.HasValue)
                {
                    var joinStart = startUtc.Value.AddMinutes(-joinWindowMinutes);
                    var joinEnd = endUtc.Value.AddMinutes(joinWindowMinutes);
                    canJoin = DateTime.UtcNow >= joinStart && DateTime.UtcNow <= joinEnd;
                }

                bool canCancel = false;
                if (b.Status == BookingStatus.Pending && startUtc.HasValue)
                {
                    canCancel = startUtc.Value > DateTime.UtcNow.AddMinutes(cancelBeforeMinutes);
                }

                return new BookingListItemVm
                {
                    Id = b.Id,
                    SlotId = b.SlotId,
                    SlotStartUtc = startUtc,
                    SlotEndUtc = endUtc,
                    SlotTimes = b.Slot != null ? $"{b.Slot.StartUtc.ToLocalTime():g} - {b.Slot.EndUtc.ToLocalTime():g}" : null,
                    SlotStartLocalString = startUtc?.ToLocalTime().ToString("g"),
                    TeacherName = b.Teacher?.User?.FullName,
                    Status = b.Status,
                    RequestedDateUtc = b.RequestedDateUtc,
                    PriceLabel = b.Price.ToEuro(),
                    Price = b.Price,
                    MeetUrl = b.MeetUrl,
                    StudentId = b.StudentId,
                    CanJoin = canJoin,
                    CanCancel = canCancel
                };
            }).ToList();

            ViewData["ActivePage"] = "Bookings";
            ViewData["BookingsTab"] = tab;
            ViewData["BookingsPage"] = page;
            ViewData["BookingsPageSize"] = pageSize;
            ViewData["BookingsTotal"] = total;

            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var studentId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(studentId)) return Challenge();

            var booking = await _db.Bookings
                .Include(b => b.Teacher).ThenInclude(t => t.User)
                .FirstOrDefaultAsync(b => b.Id == id && b.StudentId == studentId);

            if (booking == null) return NotFound();
            if (booking.Status != BookingStatus.Pending)
            {
                TempData["Error"] = "Booking.InvalidStatus";
                return RedirectToAction(nameof(MyBookings));
            }

            try
            {
                // notify teacher (does not SaveChanges)
                await CreateBookingLogAndNotifyAsync(booking, "CancelledByStudent", null);

                _db.Bookings.Remove(booking);
                await _db.SaveChangesAsync();

                TempData["Success"] = "Booking.Cancelled";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cancel booking failed {BookingId}", id);
                TempData["Error"] = "Admin.OperationFailed";
            }

            return RedirectToAction(nameof(MyBookings));
        }

        private async Task CreateBookingLogAndNotifyAsync(Booking booking, string action, string? note)
        {
            if (booking == null) throw new ArgumentNullException(nameof(booking));

            // DO NOT call SaveChanges here: caller handles DB persistence lifecycle.
            if (!string.IsNullOrEmpty(booking.TeacherId))
            {
                try
                {
                    var teacherUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == booking.TeacherId);
                    if (!string.IsNullOrEmpty(teacherUser?.Email))
                    {
                        string subjKey, bodyKey;
                        object[] args;
                        if (action == "Requested")
                        {
                            subjKey = "Email.Booking.Requested.Subject";
                            bodyKey = "Email.Booking.Requested.Body";
                            args = new object[] { booking.Id, User.Identity?.Name ?? "" };
                        }
                        else if (action == "CancelledByStudent")
                        {
                            subjKey = "Email.Booking.Cancelled.Subject";
                            bodyKey = "Email.Booking.Cancelled.Body";
                            args = new object[] { booking.Id, User.Identity?.Name ?? "" };
                        }
                        else
                        {
                            subjKey = "Email.Booking.Updated.Subject";
                            bodyKey = "Email.Booking.Updated.Body";
                            args = new object[] { booking.Id, action, note ?? "" };
                        }

                        await NotifyFireAndForgetAsync(async () =>
                            await _notifier.SendLocalizedEmailAsync(teacherUser, subjKey, bodyKey, args));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Booking notify teacher failed for booking {BookingId}", booking.Id);
                }
            }
        }

        private async Task NotifyFireAndForgetAsync(Func<Task> work)
        {
            if (work == null) return;
            try
            {
                if (_env?.IsDevelopment() == true) await work();
                else _ = Task.Run(work);
            }
            catch (Exception ex) { _logger.LogError(ex, "NotifyFireAndForget failed"); }
        }
    }
}







