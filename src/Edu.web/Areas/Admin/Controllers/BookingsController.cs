using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Infrastructure.Services;
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
    public class BookingsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IStringLocalizer<SharedResource> _L;
        private readonly IEmailSender _emailSender;

        public BookingsController(ApplicationDbContext db, IStringLocalizer<SharedResource> L, IEmailSender emailSender)
        {
            _db = db;
            _L = L;
            _emailSender = emailSender;
        }

        // GET: Admin/Bookings
        public async Task<IActionResult> Index(string? status = null, string tab = "upcoming", int page = 1, int pageSize = 20)
        {
            tab = (tab ?? "upcoming").ToLowerInvariant();
            if (tab != "upcoming" && tab != "past" && tab != "all") tab = "upcoming";

            var now = DateTime.UtcNow;

            // Note: project the minimal fields first (anonymous projection)
            var q = _db.Bookings
                .AsNoTracking()
                .Select(b => new
                {
                    b.Id,
                    b.RequestedDateUtc,
                    b.Price,
                    b.Status,
                    b.SlotId,
                    SlotStart = b.Slot != null ? b.Slot.StartUtc : (DateTime?)null,
                    SlotEnd = b.Slot != null ? b.Slot.EndUtc : (DateTime?)null,
                    StudentFullName = b.Student != null && b.Student.User != null ? b.Student.User.FullName : null,
                    StudentEmail = b.Student != null && b.Student.User != null ? b.Student.User.Email : null,
                    StudentPhoneNumber = b.Student != null && b.Student.User != null ? b.Student.User.PhoneNumber : null,
                    TeacherFullName = b.Teacher != null && b.Teacher.User != null ? b.Teacher.User.FullName : null
                }).AsQueryable();

            // status filter (existing)
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<BookingStatus>(status, true, out var st))
            {
                q = q.Where(x => x.Status == st);
            }

            // tab time filter (use SlotEnd/SlotStart from projection)
            if (tab == "upcoming")
            {
                q = q.Where(x => x.SlotEnd != null && x.SlotEnd >= now);
            }
            else if (tab == "past")
            {
                q = q.Where(x => x.SlotEnd != null && x.SlotEnd < now);
            }
            // else "all" -> no additional time filter

            q = q.OrderByDescending(x => x.RequestedDateUtc);

            var projected = q.Select(x => new AdminBookingListItemVm
            {
                Id = x.Id,
                RequestedDateUtc = x.RequestedDateUtc,
                PriceLabel = x.Price.ToEuro(),
                Status = x.Status,
                SlotId = x.SlotId,
                SlotStartUtc = x.SlotStart,
                SlotEndUtc = x.SlotEnd,
                SlotTimes = x.SlotStart != null ? $"{x.SlotStart.Value.ToLocalTime():g} - {x.SlotEnd.Value.ToLocalTime():g}" : null,
                StudentName = x.StudentFullName,
                StudentEmail = x.StudentEmail,
                StudentPhoneNumber = x.StudentPhoneNumber,
                TeacherName = x.TeacherFullName
            });

            var paged = await PaginatedList<AdminBookingListItemVm>.CreateAsync(projected, page, pageSize);

            var vm = new AdminBookingsIndexVm
            {
                Bookings = paged,                // PaginatedList<T> is itself enumerable/list
                Page = paged.PageIndex,
                PageSize = paged.PageSize,
                TotalCount = paged.TotalCount,
                StatusFilter = status,
                Tab = tab
            };

            ViewData["ActivePage"] = "The Bookings";
            return View(vm);
        }

        // GET: Admin/Bookings/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var booking = await _db.Bookings
                .AsNoTracking()
                .Include(b => b.Slot)
                .Include(b => b.Student).ThenInclude(s => s.User)
                .Include(b => b.Teacher).ThenInclude(t => t.User)
                .Include(b => b.ModerationLogs)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null) return NotFound();

            var vm = new AdminBookingDetailsVm
            {
                Id = booking.Id,
                SlotId = booking.SlotId,
                SlotTimes = booking.Slot != null ? $"{booking.Slot.StartUtc.ToLocalTime():g} - {booking.Slot.EndUtc.ToLocalTime():g}" : null,
                StudentName = booking.Student?.User?.FullName,
                StudentEmail = booking.Student?.User?.Email,
                StudentPhoneNumber = booking.Student?.User?.PhoneNumber,
                TeacherName = booking.Teacher?.User?.FullName,
                Status = booking.Status,
                RequestedDateUtc = booking.RequestedDateUtc,
                PriceLabel = booking.Price.ToEuro(),
                MeetUrl = booking.MeetUrl,
                Notes = booking.Notes,
                ModerationLogs = booking.ModerationLogs?.OrderByDescending(l => l.CreatedAtUtc).ToList() ?? new List<BookingModerationLog>()
            };

            ViewData["ActivePage"] = "The Bookings";
            return View(vm);
        }
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string? note)
        {
            var booking = await _db.Bookings
                .Include(b => b.Student).ThenInclude(s => s.User)
                .Include(b => b.Teacher).ThenInclude(t => t.User)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null) return NotFound();

            // allow deleting only pending (or cancelled) to be safe:
            if (booking.Status == BookingStatus.Paid)
            {
                TempData["Error"] = _L["Booking.CannotDeletePaid"] ?? "Cannot delete a paid booking.";
                return RedirectToAction("Index");
            }

            // Log deletion (important for audit) BEFORE removing row
            _db.BookingModerationLogs.Add(new BookingModerationLog
            {
                BookingId = booking.Id,
                ActorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                ActorName = User.Identity?.Name,
                Action = "DeletedByAdmin",
                Note = note ?? "Deleted by admin",
                CreatedAtUtc = DateTime.UtcNow
            });

            _db.Bookings.Remove(booking);

            try
            {
                await _db.SaveChangesAsync();
                TempData["Success"] = _L["Admin.Deleted"] ?? "Deleted successfully.";
            }
            catch
            {
                TempData["Error"] = _L["Admin.OperationFailed"] ?? "Operation failed.";
            }

            return RedirectToAction("Index");
        }
    }
}





