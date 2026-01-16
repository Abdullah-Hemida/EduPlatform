using Edu.Application.IServices;
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
using System.Globalization;

namespace Edu.Web.Areas.Teacher.Controllers
{
    [Area("Teacher")]
    [Authorize(Roles = "Teacher")]
    public class BookingsController : TeacherBaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _um;
        private readonly IFileStorageService _fileStorage;
        private readonly INotificationService _notifier;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<BookingsController> _logger;

        public BookingsController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> um,
            IFileStorageService fileStorage,
            INotificationService notifier,
            IWebHostEnvironment env,
            ILogger<BookingsController> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _um = um ?? throw new ArgumentNullException(nameof(um));
            _fileStorage = fileStorage ?? throw new ArgumentNullException(nameof(fileStorage));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
            _env = env;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // GET: Teacher/Bookings
        public async Task<IActionResult> Index(string tab = "upcoming", int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
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

            // Project only the fields we need (no heavy Includes)
            var baseQuery = _db.Bookings
                .AsNoTracking()
                .Where(b => b.TeacherId == teacherId)
                .Select(b => new
                {
                    b.Id,
                    b.SlotId,
                    SlotStartUtc = b.Slot!.StartUtc,
                    SlotEndUtc = b.Slot!.EndUtc,
                    b.StudentId,
                    StudentFullName = b.Student!.User!.FullName,
                    StudentPhone = b.Student!.User!.PhoneNumber,
                    StudentPhotoKey = b.Student!.User!.PhotoStorageKey,
                    b.Status,
                    b.RequestedDateUtc,
                    b.Price,
                    b.MeetUrl
                });

            if (tab == "upcoming")
            {
                baseQuery = baseQuery.Where(b => b.SlotEndUtc >= now);
            }
            else if (tab == "past")
            {
                baseQuery = baseQuery.Where(b => b.SlotEndUtc < now);
            }

            var total = await baseQuery.CountAsync(cancellationToken);

            var ordered = tab == "past"
                ? baseQuery.OrderByDescending(b => b.SlotStartUtc)
                : baseQuery.OrderBy(b => b.SlotStartUtc);

            var pageItems = await ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            // Build VMs and collect image keys
            var vmList = new List<BookingListItemVm>(pageItems.Count);
            var imageKeys = pageItems
                .Where(p => !string.IsNullOrEmpty(p.StudentPhotoKey))
                .Select(p => p.StudentPhotoKey!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var p in pageItems)
            {
                DateTime? startUtc = p.SlotStartUtc;
                DateTime? endUtc = p.SlotEndUtc;

                bool canJoin = false;
                if (p.Status == BookingStatus.Paid && startUtc != null && endUtc != null)
                {
                    var joinStart = startUtc.Value.AddMinutes(-joinWindowMinutes);
                    var joinEnd = endUtc.Value.AddMinutes(joinWindowMinutes);
                    canJoin = now >= joinStart && now <= joinEnd;
                }

                bool canCancel = false;
                if (p.Status == BookingStatus.Pending && startUtc != null)
                {
                    canCancel = startUtc.Value > now.AddMinutes(cancelBeforeMinutes);
                }

                vmList.Add(new BookingListItemVm
                {
                    Id = p.Id,
                    SlotId = p.SlotId,
                    SlotStartUtc = startUtc,
                    SlotEndUtc = endUtc,
                    SlotTimes = startUtc != null && endUtc != null ? $"{startUtc.Value.ToLocalTime():g} - {endUtc.Value.ToLocalTime():g}" : null,
                    SlotStartLocalString = startUtc?.ToLocalTime().ToString("g"),
                    StudentId = p.StudentId,
                    StudentName = p.StudentFullName,
                    StudentPhoneNumber = p.StudentPhone,
                    StudentImageKey = p.StudentPhotoKey,
                    Status = p.Status,
                    RequestedDateUtc = p.RequestedDateUtc,
                    Price = p.Price,
                    PriceLabel = p.Price.ToEuro(),
                    MeetUrl = p.MeetUrl,
                    CanJoin = canJoin,
                    CanCancel = canCancel
                });
            }

            // Resolve image keys -> public URLs (deduplicated)
            var imageMap = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var fallback = Url.Content("~/images/default-avatar.png");

            if (imageKeys.Count > 0)
            {
                var tasks = imageKeys.Select(async k =>
                {
                    try
                    {
                        var url = await _fileStorage.GetPublicUrlAsync(k);
                        imageMap[k] = string.IsNullOrEmpty(url) ? fallback : url;
                    }
                    catch
                    {
                        imageMap[k] = fallback;
                    }
                }).ToArray();

                await Task.WhenAll(tasks);
            }

            // Assign StudentImageUrl to VMs
            foreach (var vm in vmList)
            {
                if (!string.IsNullOrWhiteSpace(vm.StudentImageKey) && imageMap.TryGetValue(vm.StudentImageKey!, out var mapped))
                    vm.StudentImageUrl = mapped ?? fallback;
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
        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken = default)
        {
            var teacherId = _um.GetUserId(User);
            if (string.IsNullOrEmpty(teacherId)) return Challenge();

            // project only needed fields
            var booking = await _db.Bookings
                .AsNoTracking()
                .Where(b => b.Id == id && b.TeacherId == teacherId)
                .Select(b => new
                {
                    b.Id,
                    b.SlotId,
                    SlotStartUtc = b.Slot!.StartUtc,
                    SlotEndUtc = b.Slot!.EndUtc,
                    b.StudentId,
                    StudentFullName = b.Student!.User!.FullName,
                    StudentEmail = b.Student!.User!.Email,
                    StudentPhone = b.Student!.User!.PhoneNumber,
                    StudentPhotoKey = b.Student!.User!.PhotoStorageKey,
                    GuardianPhone = b.Student!.GuardianPhoneNumber,
                    b.Status,
                    b.RequestedDateUtc,
                    b.Price,
                    b.MeetUrl,
                    b.Notes
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (booking == null) return NotFound();

            var fallback = Url.Content("~/images/default-avatar.png");

            string? studentImageUrl = null;
            if (!string.IsNullOrEmpty(booking.StudentPhotoKey))
            {
                try
                {
                    studentImageUrl = await _fileStorage.GetPublicUrlAsync(booking.StudentPhotoKey);
                    if (string.IsNullOrEmpty(studentImageUrl)) studentImageUrl = fallback;
                }
                catch
                {
                    studentImageUrl = fallback;
                }
            }
            else studentImageUrl = fallback;

            // determine default region code by current culture (two-letter ISO, e.g. "IT")
            string defaultRegion = "IT";
            try
            {
                var regionInfo = new RegionInfo(CultureInfo.CurrentCulture.Name);
                if (!string.IsNullOrEmpty(regionInfo.TwoLetterISORegionName))
                    defaultRegion = regionInfo.TwoLetterISORegionName;
            }
            catch { /* keep IT */ }

            var vm = new BookingDetailsVm
            {
                Id = booking.Id,
                SlotId = booking.SlotId,
                SlotTimes = booking.SlotStartUtc != null && booking.SlotEndUtc != null ? $"{booking.SlotStartUtc.ToLocalTime():g} - {booking.SlotEndUtc.ToLocalTime():g}" : null,
                StudentId = booking.StudentId,
                StudentName = booking.StudentFullName,
                StudentEmail = booking.StudentEmail,
                StudentPhoneNumber = booking.StudentPhone,
                StudentWhatsapp = PhoneHelpers.ToWhatsappDigits(booking.StudentPhone, defaultRegion),
                GuardianPhoneNumber = booking.GuardianPhone,
                GuardianWhatsapp = PhoneHelpers.ToWhatsappDigits(booking.GuardianPhone, defaultRegion),
                StudentImageUrl = studentImageUrl,
                TeacherId = teacherId,
                TeacherName = User.Identity?.Name,
                Status = booking.Status,
                RequestedDateUtc = booking.RequestedDateUtc,
                PriceLabel = booking.Price.ToEuro(),
                MeetUrl = booking.MeetUrl,
                Notes = booking.Notes,
            };

            ViewData["ActivePage"] = "TheBookings";
            return View(vm);
        }

        // POST: Teacher/Bookings/UpdateMeetUrl
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMeetUrl(UpdateMeetUrlVm vm, CancellationToken cancellationToken = default)
        {
            var teacherId = _um.GetUserId(User);
            if (string.IsNullOrEmpty(teacherId)) return Challenge();

            var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == vm.BookingId && b.TeacherId == teacherId, cancellationToken);
            if (booking == null) return NotFound();

            booking.MeetUrl = vm.MeetUrl;
            _db.Bookings.Update(booking);
            await _db.SaveChangesAsync(cancellationToken);

            // notify student (localized, best-effort)
            try
            {
                if (!string.IsNullOrEmpty(booking.StudentId))
                {
                    var studentUser = await _db.Users
                        .AsNoTracking()
                        .Where(u => u.Id == booking.StudentId)
                        .Select(u => new { u.Id, u.Email })
                        .FirstOrDefaultAsync(cancellationToken);

                    if (studentUser?.Email != null)
                    {
                        await NotifyFireAndForgetAsync(async () =>
                            await _notifier.SendLocalizedEmailAsync(
                                new ApplicationUser { Id = studentUser.Id, Email = studentUser.Email }, // notifier expects ApplicationUser
                                "Email.Booking.MeetUpdated.Subject",
                                "Email.Booking.MeetUpdated.Body",
                                booking.Id
                            )
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed notifying student about MeetUrl update for booking {BookingId}", booking.Id);
            }

            TempData["Success"] = "Booking.MeetUrlUpdated";
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





