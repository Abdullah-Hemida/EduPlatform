// File: Areas/Student/Controllers/BookingsController.cs
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Infrastructure.Services;
using Edu.Web.Areas.Shared.ViewModels;
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
        private readonly IEmailSender _emailSender;

        public BookingsController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IStringLocalizer<SharedResource> localizer,
            IEmailSender emailSender)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _L = localizer ?? throw new ArgumentNullException(nameof(localizer));
            _emailSender = emailSender; // may be null; SafeSendEmailAsync guards this
        }

        // POST: Student/Bookings/Create (form submit)
        // Note: there is NO Create(GET) anymore. Quick-book forms should POST here.
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateBookingVm vm)
        {
            if (vm == null || vm.SlotId <= 0)
            {
                TempData["Error"] = _L["Admin.OperationFailed"].Value;
                return RedirectToAction("Index", "Slots");
            }

            var slot = await _db.Slots
                .Include(s => s.Bookings)
                .FirstOrDefaultAsync(s => s.Id == vm.SlotId);

            if (slot == null)
            {
                TempData["Error"] = _L["Slot.NotFound"].Value;
                return RedirectToAction("Index", "Slots");
            }

            var occupied = slot.Bookings.Count(b => b.Status == BookingStatus.Pending || b.Status == BookingStatus.Paid);
            var available = slot.Capacity - occupied;
            if (available <= 0)
            {
                TempData["Error"] = _L["Slot.AlreadyBooked"].Value;
                return RedirectToAction("Index", "Slots");
            }

            var studentId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(studentId))
            {
                return Challenge();
            }

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
                _db.Bookings.Add(booking);
                await _db.SaveChangesAsync(); // persist to get booking.Id

                // record moderation log and notify teacher (centralized)
                await CreateBookingLogAndNotifyAsync(booking, "Requested", vm.Notes ?? "Student requested booking");

                TempData["Success"] = _L["Booking.Requested"].Value;
                return RedirectToAction(nameof(MyBookings));
            }
            catch
            {
                TempData["Error"] = _L["Admin.OperationFailed"].Value;
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

            // NOTE: use IQueryable<Booking> (not IIncludableQueryable) so Where() assignment works
            IQueryable<Booking> baseQuery = _db.Bookings
                .AsNoTracking()
                .Where(b => b.StudentId == studentId)
                .Include(b => b.Slot)
                .Include(b => b.Teacher).ThenInclude(t => t.User);

            if (tab == "upcoming")
            {
                baseQuery = baseQuery.Where(b => b.Slot != null && b.Slot.EndUtc >= now);
            }
            else if (tab == "past")
            {
                baseQuery = baseQuery.Where(b => b.Slot != null && b.Slot.EndUtc < now);
            }

            var total = await baseQuery.CountAsync();

            IQueryable<Booking> ordered;
            if (tab == "past")
                ordered = baseQuery.OrderByDescending(b => b.Slot!.StartUtc);
            else
                ordered = baseQuery.OrderBy(b => b.Slot!.StartUtc);

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

        // POST: Student/Bookings/Cancel/5 (form)
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
                TempData["Error"] = _L["Booking.InvalidStatus"].Value;
                return RedirectToAction(nameof(MyBookings));
            }

            try
            {
                // log + notify via helper (helper will add the moderation log and notify teacher)
                await CreateBookingLogAndNotifyAsync(booking, "CancelledByStudent", null);

                // then remove booking
                _db.Bookings.Remove(booking);
                await _db.SaveChangesAsync();

                TempData["Success"] = _L["Booking.Cancelled"].Value;
            }
            catch
            {
                TempData["Error"] = _L["Admin.OperationFailed"].Value;
            }

            return RedirectToAction(nameof(MyBookings));
        }

        #region Helpers

        /// <summary>
        /// centralized helper: writes a moderation log and notifies the teacher (best-effort).
        /// It saves the moderation log and performs the notification.
        /// </summary>
        private async Task CreateBookingLogAndNotifyAsync(Booking booking, string action, string? note)
        {
            if (booking == null) throw new ArgumentNullException(nameof(booking));

            // Add moderation log
            _db.BookingModerationLogs.Add(new BookingModerationLog
            {
                BookingId = booking.Id,
                ActorId = booking.StudentId ?? _userManager.GetUserId(User),
                ActorName = User.Identity?.Name,
                Action = action,
                Note = note,
                CreatedAtUtc = DateTime.UtcNow
            });

            // Save the new log (so it is persisted)
            await _db.SaveChangesAsync();

            // Notify teacher (best-effort)
            if (!string.IsNullOrEmpty(booking.TeacherId))
            {
                try
                {
                    var teacherUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == booking.TeacherId);
                    if (!string.IsNullOrEmpty(teacherUser?.Email))
                    {
                        string subject;
                        string body;

                        if (action == "Requested")
                        {
                            subject = _L["Email.Booking.Requested.Subject"];
                            body = string.Format(_L["Email.Booking.Requested.Body"], booking.Id, User.Identity?.Name);
                        }
                        else if (action == "CancelledByStudent")
                        {
                            subject = string.Format(_L["Email.Booking.Cancelled.Subject"], booking.Id);
                            body = string.Format(_L["Email.Booking.Cancelled.Body"], booking.Id, User.Identity?.Name);
                        }
                        else if (action == "MarkPaid")
                        {
                            subject = string.Format(_L["Email.Booking.Paid.Subject"], booking.Id);
                            body = string.Format(_L["Email.Booking.Paid.Body"], booking.Id);
                        }
                        else
                        {
                            subject = _L["Email.Booking.Updated.Subject"];
                            body = string.Format(_L["Email.Booking.Updated.Body"], booking.Id, action, note ?? "");
                        }

                        await SafeSendEmailAsync(teacherUser.Email, subject, body);
                    }
                }
                catch
                {
                    // swallow; notification is best-effort
                }
            }
        }

        private async Task SafeSendEmailAsync(string to, string subject, string htmlBody)
        {
            try
            {
                if (_emailSender != null)
                    await _emailSender.SendEmailAsync(to, subject, htmlBody);
            }
            catch
            {
                // swallow to avoid interfering with user flow
            }
        }

        #endregion
    }
}





