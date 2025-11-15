using Edu.Infrastructure.Data;
using Edu.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Edu.Web.Areas.Shared.ViewModels;
using Edu.Web.Resources;
using Edu.Infrastructure.Services;
using Edu.Infrastructure.Helpers;

namespace Edu.Web.Areas.Teacher.Controllers
{
    [Area("Teacher")]
    [Authorize(Roles = "Teacher")]
    public class BookingsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _um;
        private readonly IStringLocalizer<SharedResource> _L;
        private readonly IEmailSender _emailSender;

        public BookingsController(ApplicationDbContext db, UserManager<ApplicationUser> um, IStringLocalizer<SharedResource> L, IEmailSender emailSender)
        {
            _db = db;
            _um = um;
            _L = L;
            _emailSender = emailSender;
        }

        // GET: Teacher/Bookings
        public async Task<IActionResult> Index(string tab = "upcoming", int page = 1, int pageSize = 10)
        {
            var teacherId = _um.GetUserId(User);
            if (string.IsNullOrEmpty(teacherId)) return Challenge();

            tab = (tab ?? "").ToLowerInvariant();
            if (tab != "upcoming" && tab != "past" && tab != "all") tab = "upcoming";

            if (page <= 0) page = 1;
            if (pageSize <= 0 || pageSize > 100) pageSize = 10;

            var now = DateTime.UtcNow;

            // Policy: adjust these values as needed (or move to config)
            const int joinWindowMinutes = 15;     // allow join from start -15 to end +15
            const int cancelBeforeMinutes = 60;   // teacher can cancel if slot starts after now + 60min

            // Base query for teacher bookings (use IQueryable to allow further Where assignments)
            IQueryable<Booking> baseQuery = _db.Bookings
                .AsNoTracking()
                .Where(b => b.TeacherId == teacherId)
                .Include(b => b.Slot)
                .Include(b => b.Student).ThenInclude(s => s.User);

            // apply tab (time) filter
            if (tab == "upcoming")
            {
                baseQuery = baseQuery.Where(b => b.Slot != null && b.Slot.EndUtc >= now);
            }
            else if (tab == "past")
            {
                baseQuery = baseQuery.Where(b => b.Slot != null && b.Slot.EndUtc < now);
            }

            var total = await baseQuery.CountAsync();

            // ordering: upcoming ascending, past descending
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

                // teacher can join when booking is paid and within join window
                bool canJoin = false;
                if (b.Status == BookingStatus.Paid && startUtc.HasValue && endUtc.HasValue)
                {
                    var joinStart = startUtc.Value.AddMinutes(-joinWindowMinutes);
                    var joinEnd = endUtc.Value.AddMinutes(joinWindowMinutes);
                    canJoin = DateTime.UtcNow >= joinStart && DateTime.UtcNow <= joinEnd;
                }

                // teacher can cancel pending bookings if far enough in future (policy)
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
                    StudentId = b.StudentId,
                    StudentName = b.Student?.User?.FullName,
                    StudentPhoneNumber = b.Student?.User?.PhoneNumber,
                    Status = b.Status,
                    RequestedDateUtc = b.RequestedDateUtc,
                    Price = b.Price,
                    PriceLabel = b.Price.ToEuro(),
                    MeetUrl = b.MeetUrl,
                    CanJoin = canJoin,
                    CanCancel = canCancel
                };
            }).ToList();

            ViewData["ActivePage"] = "TheBookings";
            ViewData["BookingsTab"] = tab;
            ViewData["BookingsPage"] = page;
            ViewData["BookingsPageSize"] = pageSize;
            ViewData["BookingsTotal"] = total;

            return View(vm);
        }

        // GET: Teacher/Bookings/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var teacherId = _um.GetUserId(User);
            var booking = await _db.Bookings
                .Include(b => b.Slot)
                .Include(b => b.Student).ThenInclude(s => s.User)
                .Include(b => b.ModerationLogs)
                .FirstOrDefaultAsync(b => b.Id == id && b.TeacherId == teacherId);

            if (booking == null) return NotFound();

            var vm = new BookingDetailsVm
            {
                Id = booking.Id,
                SlotId = booking.SlotId,
                SlotTimes = booking.Slot != null ? $"{booking.Slot.StartUtc.ToLocalTime():g} - {booking.Slot.EndUtc.ToLocalTime():g}" : null,
                StudentId = booking.StudentId,
                StudentName = booking.Student?.User?.FullName,
                StudentEmail = booking.Student?.User?.Email,
                StudentPhoneNumber = booking.Student?.User?.PhoneNumber,
                TeacherId = booking.TeacherId,
                TeacherName = User.Identity?.Name,
                Status = booking.Status,
                RequestedDateUtc = booking.RequestedDateUtc,
                PriceLabel = booking.Price.ToEuro(),
                MeetUrl = booking.MeetUrl,
                Notes = booking.Notes,
                ModerationLogs = booking.ModerationLogs.OrderByDescending(l => l.CreatedAtUtc).ToList()
            };
            ViewData["ActivePage"] = "TheBookings";
            return View(vm);
        }

        // POST: Teacher/Bookings/UpdateMeetUrl
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMeetUrl(UpdateMeetUrlVm vm)
        {
            var teacherId = _um.GetUserId(User);
            var booking = await _db.Bookings.Include(b => b.Student).FirstOrDefaultAsync(b => b.Id == vm.BookingId && b.TeacherId == teacherId);
            if (booking == null) return NotFound();

            booking.MeetUrl = vm.MeetUrl;
            _db.Bookings.Update(booking);

            _db.BookingModerationLogs.Add(new BookingModerationLog
            {
                BookingId = booking.Id,
                ActorId = teacherId,
                ActorName = User.Identity?.Name,
                Action = "MeetUrlUpdated",
                Note = vm.MeetUrl,
                CreatedAtUtc = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            // notify student
            try
            {
                var studentEmail = booking.Student?.User?.Email;
                if (!string.IsNullOrEmpty(studentEmail))
                {
                    await _emailSender.SendEmailAsync(studentEmail, _L["Email.Booking.MeetUpdated.Subject"], string.Format(_L["Email.Booking.MeetUpdated.Body"], booking.Id));
                }
            }
            catch { }

            TempData["Success"] = _L["Booking.MeetUrlUpdated"].Value;
            return RedirectToAction(nameof(Details), new { id = booking.Id });
        }
    }
}



