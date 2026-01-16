using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Web.Areas.Admin.ViewModels;
using Edu.Web.Helpers;
using Edu.Web.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.Globalization;

namespace Edu.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class BookingsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IStringLocalizer<SharedResource> _L;
        private readonly INotificationService _notifier;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<BookingsController> _logger;

        public BookingsController(
            ApplicationDbContext db,
            IStringLocalizer<SharedResource> L,
            INotificationService notifier,
            IWebHostEnvironment env,
            ILogger<BookingsController> logger)
        {
            _db = db;
            _L = L;
            _notifier = notifier;
            _env = env;
            _logger = logger;
        }

        // GET: Admin/Bookings
        // Note: no server-side formatting here — return UTC datetimes and format in view
        public async Task<IActionResult> Index(string? status = null, string tab = "upcoming", int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
        {
            tab = (tab ?? "upcoming").ToLowerInvariant();
            if (tab != "upcoming" && tab != "past" && tab != "all") tab = "upcoming";

            var now = DateTime.UtcNow;

            // Base query: minimal projection of scalars (EF can translate)
            var q = _db.Bookings
                .AsNoTracking()
                .Select(b => new
                {
                    b.Id,
                    b.RequestedDateUtc,
                    b.Price,
                    b.Status,
                    b.SlotId,
                    SlotStartUtc = b.Slot != null ? b.Slot.StartUtc : (DateTime?)null,
                    SlotEndUtc = b.Slot != null ? b.Slot.EndUtc : (DateTime?)null,
                    StudentFullName = b.Student!.User!.FullName,
                    StudentEmail = b.Student!.User!.Email,
                    StudentPhoneNumber = b.Student!.User!.PhoneNumber,
                    TeacherFullName = b.Teacher!.User!.FullName
                });

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<BookingStatus>(status, true, out var st))
            {
                q = q.Where(x => x.Status == st);
            }

            if (tab == "upcoming")
            {
                q = q.Where(x => x.SlotEndUtc != null && x.SlotEndUtc >= now);
            }
            else if (tab == "past")
            {
                q = q.Where(x => x.SlotEndUtc != null && x.SlotEndUtc < now);
            }

            q = q.OrderByDescending(x => x.RequestedDateUtc);

            // Project to view-model but keep UTC datetimes; do NOT format them here
            var projected = q.Select(x => new AdminBookingListItemVm
            {
                Id = x.Id,
                RequestedDateUtc = x.RequestedDateUtc,
                Price = x.Price,                         // keep numeric price; provide label when rendering
                PriceLabel = x.Price.ToEuro(),
                Status = x.Status,
                SlotId = x.SlotId,
                SlotStartUtc = x.SlotStartUtc,
                SlotEndUtc = x.SlotEndUtc,
                // SlotTimes will be built in the view (local timezone + culture)
                StudentName = x.StudentFullName,
                StudentEmail = x.StudentEmail,
                StudentPhoneNumber = x.StudentPhoneNumber,
                StudentWhatsapp = PhoneHelpers.ToWhatsappDigits(x.StudentPhoneNumber, "IT"),
                TeacherName = x.TeacherFullName
            });

            var paged = await PaginatedList<AdminBookingListItemVm>.CreateAsync(projected, page, pageSize);

            var vm = new AdminBookingsIndexVm
            {
                Bookings = paged,
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
        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken = default)
        {
            var booking = await _db.Bookings
                .AsNoTracking()
                .Include(b => b.Slot)
                .Include(b => b.Student).ThenInclude(s => s.User)
                .Include(b => b.Teacher).ThenInclude(t => t.User)
                .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

            if (booking == null) return NotFound();

            // derive a reasonable default region code
            string defaultRegion = "IT";
            try
            {
                var regionInfo = new RegionInfo(CultureInfo.CurrentCulture.Name);
                if (!string.IsNullOrEmpty(regionInfo.TwoLetterISORegionName)) defaultRegion = regionInfo.TwoLetterISORegionName;
            }
            catch { /* keep default */ }

            var vm = new AdminBookingDetailsVm
            {
                Id = booking.Id,
                SlotId = booking.SlotId,
                SlotStartUtc = booking.Slot.StartUtc,
                // Replace this line:
                // With this line (remove the cancellationToken argument):
                SlotEndUtc = booking.Slot.EndUtc,
                // View will display a human friendly formatted time (local)
                StudentName = booking.Student?.User?.FullName,
                StudentEmail = booking.Student?.User?.Email,
                StudentPhoneNumber = booking.Student?.User?.PhoneNumber,
                StudentWhatsapp = PhoneHelpers.ToWhatsappDigits(booking.Student?.User?.PhoneNumber, defaultRegion),
                GuardianPhoneNumber = booking.Student?.GuardianPhoneNumber,
                GuardianWhatsapp = PhoneHelpers.ToWhatsappDigits(booking.Student?.GuardianPhoneNumber, defaultRegion),
                TeacherName = booking.Teacher?.User?.FullName,
                Status = booking.Status,
                RequestedDateUtc = booking.RequestedDateUtc,
                PriceLabel = booking.Price.ToEuro(),
                MeetUrl = booking.MeetUrl,
                Notes = booking.Notes
            };

            ViewData["ActivePage"] = "The Bookings";
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string? note, CancellationToken cancellationToken = default)
        {
            var booking = await _db.Bookings
                .Include(b => b.Student).ThenInclude(s => s.User)
                .Include(b => b.Teacher).ThenInclude(t => t.User)
                .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

            if (booking == null) return NotFound();

            if (booking.Status == BookingStatus.Paid)
            {
                TempData["Error"] = "Booking.CannotDeletePaid";
                return RedirectToAction("Index");
            }

            _db.Bookings.Remove(booking);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                TempData["Success"] = "Admin.Deleted";
                _logger.LogInformation("Booking {BookingId} deleted by {User}", booking.Id, User?.Identity?.Name ?? "unknown");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete booking {BookingId}", booking.Id);
                TempData["Error"] = "Admin.OperationFailed";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [Route("Admin/Bookings/MarkPaid/{id:int}")]
        public async Task<IActionResult> MarkPaid(int id, CancellationToken cancellationToken = default)
        {
            var booking = await _db.Bookings
                .Include(b => b.Student).ThenInclude(s => s.User)
                .Include(b => b.Teacher).ThenInclude(t => t.User)
                .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

            if (booking == null)
                return NotFound(new { success = false, error = "Booking not found" });

            if (booking.Status == BookingStatus.Paid)
                return BadRequest(new { success = false, error = "Booking is already marked as paid" });

            // Transition Pending → Paid
            booking.Status = BookingStatus.Paid;

            // Set PaidAtUtc only now (authoritative payment moment)
            booking.PaidAtUtc = DateTime.UtcNow;

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { success = false, error = "The booking was modified by another user. Please reload." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark booking {BookingId} as paid", booking.Id);
                return StatusCode(500, new { success = false, error = "Failed to update booking" });
            }

            // Best-effort notification. Prefer background queue/hosted service in production.
            _ = Task.Run(async () =>
            {
                try
                {
                    var student = booking.Student?.User;
                    var teacher = booking.Teacher?.User;

                    if (student != null)
                        await _notifier.SendLocalizedEmailAsync(
                            student,
                            "Booking.Email.Subject.Paid",
                            "Booking.Email.Body.Paid",
                            booking.Id,
                            booking.Price.ToString("C"));

                    if (teacher != null)
                        await _notifier.SendLocalizedEmailAsync(
                            teacher,
                            "Booking.Email.Subject.Paid",
                            "Booking.Email.Body.Paid",
                            booking.Id,
                            booking.Price.ToString("C"));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Notifications after marking booking {BookingId} paid failed", booking.Id);
                }
            });

            return Ok(new
            {
                success = true,
                id = booking.Id,
                paidAtUtc = booking.PaidAtUtc
            });
        }
    }
}







