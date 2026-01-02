using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Infrastructure.Services;
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
        public BookingsController(ApplicationDbContext db, IStringLocalizer<SharedResource> L, INotificationService notifier, IWebHostEnvironment env, ILogger<BookingsController> logger)
        {
            _db = db;
            _L = L;
            _notifier = notifier;
            _env = env;
            _logger = logger;
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
                StudentWhatsapp = PhoneHelpers.ToWhatsappDigits(x.StudentPhoneNumber, "IT"),
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
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null) return NotFound();

            // determine default region code (two-letter ISO, e.g. "IT")
            string defaultRegion = "IT";
            try
            {
                // derive from current culture (e.g. "it-IT" -> RegionInfo -> "IT")
                var regionInfo = new RegionInfo(CultureInfo.CurrentCulture.Name);
                if (!string.IsNullOrEmpty(regionInfo.TwoLetterISORegionName))
                {
                    defaultRegion = regionInfo.TwoLetterISORegionName;
                }
            }
            catch
            {
                // ignore and keep fallback "IT"
            }

            var studentPhone = booking.Student?.User?.PhoneNumber;
            var guardianPhone = booking.Student?.GuardianPhoneNumber;

            var vm = new AdminBookingDetailsVm
            {
                Id = booking.Id,
                SlotId = booking.SlotId,
                SlotTimes = booking.Slot != null ? $"{booking.Slot.StartUtc.ToLocalTime():g} - {booking.Slot.EndUtc.ToLocalTime():g}" : null,
                StudentName = booking.Student?.User?.FullName,
                StudentEmail = booking.Student?.User?.Email,
                StudentPhoneNumber = studentPhone,
                StudentWhatsapp = PhoneHelpers.ToWhatsappDigits(studentPhone, defaultRegion),
                GuardianPhoneNumber = guardianPhone,
                GuardianWhatsapp = PhoneHelpers.ToWhatsappDigits(guardianPhone, defaultRegion),
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
                TempData["Error"] = _L["Booking.CannotDeletePaid"].Value ?? "Cannot delete a paid booking.";
                return RedirectToAction("Index");
            }

            _db.Bookings.Remove(booking);

            try
            {
                await _db.SaveChangesAsync();
                TempData["Success"] = _L["Admin.Deleted"].Value ?? "Deleted successfully.";
            }
            catch
            {
                TempData["Error"] = _L["Admin.OperationFailed"].Value ?? "Operation failed.";
            }

            return RedirectToAction("Index");
        }

        // DTO for AJAX request
        public class MarkPaidRequest
        {
            public int Id { get; set; }
            public decimal? Amount { get; set; }
            public string? PaymentRef { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> MarkPaid([FromBody] MarkPaidRequest request)
        {
            if (request == null) return BadRequest(new { success = false, error = "Invalid payload" });

            var booking = await _db.Bookings
                .Include(b => b.Student).ThenInclude(s => s.User)
                .Include(b => b.Teacher).ThenInclude(t => t.User)
                .FirstOrDefaultAsync(b => b.Id == request.Id);

            if (booking == null) return NotFound(new { success = false, error = "Booking not found" });

            if (booking.Status == BookingStatus.Paid) return BadRequest(new { success = false, error = "Already paid" });

            booking.Status = BookingStatus.Paid;
            if (request.Amount.HasValue) booking.Price = request.Amount.Value;

            var paymentRefProp = booking.GetType().GetProperty("PaymentRef");
            if (paymentRefProp != null && paymentRefProp.CanWrite) paymentRefProp.SetValue(booking, request.PaymentRef);

            var paidAtProp = booking.GetType().GetProperty("PaidAtUtc");
            if (paidAtProp != null && paidAtProp.CanWrite) paidAtProp.SetValue(booking, DateTime.UtcNow);

            try { await _db.SaveChangesAsync(); }
            catch (Exception ex) { return StatusCode(500, new { success = false, error = _L["Admin.OperationFailed"].Value ?? "Operation failed" }); }

            // Notify teacher and student via NotificationService (best-effort)
            try
            {
                async Task SendForUser(ApplicationUser? user)
                {
                    if (user == null || string.IsNullOrEmpty(user.Email)) return;
                    await _notifier.SendLocalizedEmailAsync(user, "Booking.Email.Subject.Paid", "Booking.Email.Body.Paid", booking.Id, booking.Price.ToString("C"));
                }

                var studentUser = booking.Student?.User;
                var teacherUser = booking.Teacher?.User;

                await NotifyFireAndForgetAsync(async () =>
                {
                    if (studentUser != null) await SendForUser(studentUser);
                    if (teacherUser != null) await SendForUser(teacherUser);
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Notify after MarkPaid failed for booking {BookingId}", booking.Id);
            }

            var priceLabelRes = booking.Price.ToEuro();
            return Ok(new { success = true, price = booking.Price, priceLabel = priceLabelRes });
        }

        // Helper to run notifications awaited in development, fire-and-forget otherwise
        private async Task NotifyFireAndForgetAsync(Func<Task> work)
        {
            if (work == null) return;
            try
            {
                if (_env?.IsDevelopment() == true) await work();
                else _ = Task.Run(work);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NotifyFireAndForget failed");
            }
        }
    }
}






