using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Infrastructure.Services;
using Edu.Web.Areas.Shared.ViewModels;
using Edu.Web.Helpers;
using Edu.Web.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Edu.Web.Areas.Teacher.Controllers
{
    [Area("Teacher")]
    [Authorize(Roles = "Teacher")]
    public class BookingsController : TeacherBaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _um;
        private readonly IStringLocalizer<SharedResource> _L;
        private readonly IFileStorageService _fileStorage;
        private readonly INotificationService _notifier;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<BookingsController> _logger;

        public BookingsController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> um,
            IStringLocalizer<SharedResource> L,
            IFileStorageService fileStorage,
            INotificationService notifier,
            IWebHostEnvironment env,
            ILogger<BookingsController> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _um = um ?? throw new ArgumentNullException(nameof(um));
            _L = L ?? throw new ArgumentNullException(nameof(L));
            _fileStorage = fileStorage ?? throw new ArgumentNullException(nameof(fileStorage));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
            _env = env;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
            const int joinWindowMinutes = 15;
            const int cancelBeforeMinutes = 60;

            IQueryable<Booking> baseQuery = _db.Bookings
                .AsNoTracking()
                .Where(b => b.TeacherId == teacherId)
                .Include(b => b.Slot)
                .Include(b => b.Student).ThenInclude(s => s.User);

            if (tab == "upcoming")
            {
                baseQuery = baseQuery.Where(b => b.Slot != null && b.Slot.EndUtc >= now);
            }
            else if (tab == "past")
            {
                baseQuery = baseQuery.Where(b => b.Slot != null && b.Slot.EndUtc < now);
            }

            var total = await baseQuery.CountAsync();

            IQueryable<Booking> ordered = tab == "past"
                ? baseQuery.OrderByDescending(b => b.Slot!.StartUtc)
                : baseQuery.OrderBy(b => b.Slot!.StartUtc);

            var bookings = await ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Build VMs and collect image keys
            var vmList = new List<BookingListItemVm>();
            var imageKeys = new HashSet<string?>(StringComparer.OrdinalIgnoreCase);

            foreach (var b in bookings)
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

                var vm = new BookingListItemVm
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
                    StudentImageKey = b.Student?.User?.PhotoStorageKey, // new
                    Status = b.Status,
                    RequestedDateUtc = b.RequestedDateUtc,
                    Price = b.Price,
                    PriceLabel = b.Price.ToEuro(),
                    MeetUrl = b.MeetUrl,
                    CanJoin = canJoin,
                    CanCancel = canCancel
                };

                if (!string.IsNullOrWhiteSpace(vm.StudentImageKey))
                    imageKeys.Add(vm.StudentImageKey);

                vmList.Add(vm);
            }

            // Resolve image keys -> public URLs (deduplicated)
            var imageMap = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var fallback = Url.Content("~/images/default-avatar.png"); // adjust path as needed

            foreach (var key in imageKeys)
            {
                try
                {
                    var url = await _fileStorage.GetPublicUrlAsync(key!);
                    imageMap[key!] = string.IsNullOrEmpty(url) ? fallback : url;
                }
                catch
                {
                    imageMap[key!] = fallback;
                }
            }

            // Assign StudentImageUrl to VMs
            foreach (var vm in vmList)
            {
                if (!string.IsNullOrWhiteSpace(vm.StudentImageKey) && imageMap.ContainsKey(vm.StudentImageKey!))
                    vm.StudentImageUrl = imageMap[vm.StudentImageKey!];
                else
                    vm.StudentImageUrl = fallback;
            }

            ViewData["ActivePage"] = "TheBookings";
            ViewData["BookingsTab"] = tab;
            ViewData["BookingsPage"] = page;
            ViewData["BookingsPageSize"] = pageSize;
            ViewData["BookingsTotal"] = total;

            return View(vmList);
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

            var fallback = Url.Content("~/images/default-avatar.png");

            string? studentImageUrl = null;
            var studentKey = booking.Student?.User?.PhotoStorageKey;
            if (!string.IsNullOrWhiteSpace(studentKey))
            {
                try
                {
                    studentImageUrl = await _fileStorage.GetPublicUrlAsync(studentKey);
                }
                catch
                {
                    studentImageUrl = null;
                }
            }

            var vm = new BookingDetailsVm
            {
                Id = booking.Id,
                SlotId = booking.SlotId,
                SlotTimes = booking.Slot != null ? $"{booking.Slot.StartUtc.ToLocalTime():g} - {booking.Slot.EndUtc.ToLocalTime():g}" : null,
                StudentId = booking.StudentId,
                StudentName = booking.Student?.User?.FullName,
                StudentEmail = booking.Student?.User?.Email,
                StudentPhoneNumber = booking.Student?.User?.PhoneNumber,
                GuardianPhoneNumber = booking.Student.GuardianPhoneNumber,
                StudentImageUrl = string.IsNullOrEmpty(studentImageUrl) ? fallback : studentImageUrl, // new
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
            var booking = await _db.Bookings.Include(b => b.Student).ThenInclude(s => s.User).FirstOrDefaultAsync(b => b.Id == vm.BookingId && b.TeacherId == teacherId);
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

            // notify student (localized, best-effort)
            try
            {
                var studentUser = booking.Student?.User;
                if (studentUser != null && !string.IsNullOrEmpty(studentUser.Email))
                {
                    await NotifyFireAndForgetAsync(async () =>
                        await _notifier.SendLocalizedEmailAsync(
                            studentUser,
                            "Email.Booking.MeetUpdated.Subject",
                            "Email.Booking.MeetUpdated.Body",
                            booking.Id
                        )
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed notifying student about MeetUrl update for booking {BookingId}", booking.Id);
            }

            TempData["Success"] = _L["Booking.MeetUrlUpdated"].Value;
            return RedirectToAction(nameof(Details), new { id = booking.Id });
        }

        // helper to run notifications awaited in Development, or fire-and-forget in other envs
        private async Task NotifyFireAndForgetAsync(Func<Task> work)
        {
            if (work == null) return;
            try
            {
                if (_env?.IsDevelopment() == true)
                {
                    await work();
                }
                else
                {
                    _ = Task.Run(work);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NotifyFireAndForgetAsync failed executing notification work");
            }
        }
    }
}




