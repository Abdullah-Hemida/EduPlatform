using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Web.Resources;
using Edu.Web.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.Globalization;

namespace Edu.Web.Controllers
{
    public class TeachersController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileStorageService _fileStorage;
        private readonly IStringLocalizer<SharedResource> _L;

        // default page size for listing
        private const int DefaultPageSize = 12;

        public TeachersController(ApplicationDbContext db,
                                  UserManager<ApplicationUser> userManager,
                                  IFileStorageService fileStorage,
                                  IStringLocalizer<SharedResource> localizer)
        {
            _db = db;
            _userManager = userManager;
            _fileStorage = fileStorage;
            _L = localizer;
        }

        // GET: /Teachers
        // public listing of teachers with search + paging
        public async Task<IActionResult> Index(string? q, int page = 1, int pageSize = DefaultPageSize)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 6, 48);

            var baseQuery = _db.Teachers
                               .AsNoTracking()
                               .Include(t => t.User)
                               .Where(t => t.Status == TeacherStatus.Approved); // show only approved teachers

            if (!string.IsNullOrWhiteSpace(q))
            {
                var normalized = q.Trim().ToLowerInvariant();
                baseQuery = baseQuery.Where(t =>
                    (t.User != null && (t.User.FullName!.ToLower().Contains(normalized) || t.User.UserName!.ToLower().Contains(normalized)))
                    || t.JobTitle.ToLower().Contains(normalized)
                    || (t.ShortBio != null && t.ShortBio.ToLower().Contains(normalized))
                );
            }

            var total = await baseQuery.CountAsync();

            var teachers = await baseQuery
                .OrderByDescending(t => t.User!.FullName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Pre-fetch counts in batch for better perf:
            var teacherIds = teachers.Select(t => t.Id).ToList();

            var privateCourseCounts = await _db.PrivateCourses
                .AsNoTracking()
                .Where(c => c.TeacherId != null && teacherIds.Contains(c.TeacherId))
                .GroupBy(c => c.TeacherId)
                .Select(g => new { TeacherId = g.Key!, Count = g.Count() })
                .ToListAsync();

            var reactiveCourseCounts = await _db.ReactiveCourses
                .AsNoTracking()
                .Where(r => r.TeacherId != null && teacherIds.Contains(r.TeacherId) && !r.IsArchived)
                .GroupBy(r => r.TeacherId)
                .Select(g => new { TeacherId = g.Key!, Count = g.Count() })
                .ToListAsync();

            // map counts to dictionary
            var privateMap = privateCourseCounts.ToDictionary(x => x.TeacherId, x => x.Count);
            var reactiveMap = reactiveCourseCounts.ToDictionary(x => x.TeacherId, x => x.Count);

            // Prepare culture for euro formatting (IT format)
            var euroCulture = CultureInfo.GetCultureInfo("it-IT");

            var vm = new TeacherIndexVm
            {
                Query = q,
                Page = page,
                PageSize = pageSize,
                TotalCount = total,
                Teachers = new List<TeacherCardVm>()
            };

            foreach (var t in teachers)
            {
                // Resolve photo public url (prefer storage key, then PhotoUrl, else default)
                string photo = Url.Content("~/uploads/images/pngtree-character-default-avatar-image_2237203.jpg");
                if (!string.IsNullOrEmpty(t.User?.PhotoStorageKey))
                {
                    var resolved = await SafeGetPublicUrlAsync(t.User.PhotoStorageKey);
                    if (!string.IsNullOrEmpty(resolved)) photo = resolved;
                    else if (!string.IsNullOrEmpty(t.User.PhotoUrl)) photo = t.User.PhotoUrl!;
                }
                else if (!string.IsNullOrEmpty(t.User?.PhotoUrl))
                {
                    photo = t.User.PhotoUrl!;
                }

                privateMap.TryGetValue(t.Id, out var pcCount);
                reactiveMap.TryGetValue(t.Id, out var rcCount);

                vm.Teachers.Add(new TeacherCardVm
                {
                    Id = t.Id,
                    FullName = t.User?.FullName ?? t.User?.UserName ?? "—",
                    PhotoUrl = photo,
                    JobTitle = t.JobTitle,
                    ShortBio = t.ShortBio,
                    IntroVideoUrl = t.IntroVideoUrl,
                    PrivateCoursesCount = pcCount,
                    ReactiveCoursesCount = rcCount
                });
            }

            // compute total pages
            vm.TotalPages = (int)Math.Ceiling(vm.TotalCount / (double)vm.PageSize);

            return View(vm);
        }

        // GET: /Teachers/Details/{id}
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id)) return BadRequest();

            var teacher = await _db.Teachers
                .AsNoTracking()
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (teacher == null) return NotFound();

            // Resolve photo (unchanged)
            string photo = Url.Content("~/images/default-avatar.png");
            if (!string.IsNullOrEmpty(teacher.User?.PhotoStorageKey))
            {
                var resolved = await SafeGetPublicUrlAsync(teacher.User.PhotoStorageKey);
                if (!string.IsNullOrEmpty(resolved)) photo = resolved;
                else if (!string.IsNullOrEmpty(teacher.User.PhotoUrl)) photo = teacher.User.PhotoUrl!;
            }
            else if (!string.IsNullOrEmpty(teacher.User?.PhotoUrl))
            {
                photo = teacher.User.PhotoUrl!;
            }

            // Upcoming / available slots (only future slots) - include Bookings so we can compute availability & current user's booking
            var now = DateTime.UtcNow;
            var slots = await _db.Slots
                .AsNoTracking()
                .Where(s => s.TeacherId == id && s.EndUtc >= now)
                .OrderBy(s => s.StartUtc)
                .Include(s => s.Bookings) // include bookings
                .ToListAsync();

            // get current user id (may be null/anonymous)
            var currentUserId = _userManager?.GetUserId(User); // ensure _userManager is available on controller

            var slotVms = slots.Select(s =>
            {
                // occupied: Pending + Paid count
                var occupied = s.Bookings?.Count(b => b.Status == BookingStatus.Pending || b.Status == BookingStatus.Paid) ?? 0;
                var available = Math.Max(0, s.Capacity - occupied);

                // find booking for current user if exists (prefer Pending but accept Paid as well)
                int? currBookingId = null;
                var isBooked = false;
                if (!string.IsNullOrEmpty(currentUserId) && s.Bookings != null)
                {
                    var userBooking = s.Bookings
                        .FirstOrDefault(b => b.StudentId == currentUserId && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Paid));
                    if (userBooking != null)
                    {
                        isBooked = true;
                        currBookingId = userBooking.Id;
                    }
                }

                return new SlotVm
                {
                    Id = s.Id,
                    StartUtc = s.StartUtc,
                    EndUtc = s.EndUtc,
                    Price = s.Price,
                    PriceLabel = s.Price.ToEuro(),
                    LocationUrl = s.LocationUrl,
                    IsBooked = isBooked,
                    CurrentBookingId = currBookingId,
                    AvailableSeats = available
                };
            }).ToList();

            // Reactive courses (unchanged - keep your existing logic)
            var reactiveCourses = await _db.ReactiveCourses
                .AsNoTracking()
                .Where(r => r.TeacherId == id && !r.IsArchived)
                .OrderByDescending(r => r.StartDate)
                .Include(r => r.Months)
                .ToListAsync();

            var reactiveVms = new List<ReactiveCourseCardVm>();
            foreach (var rc in reactiveCourses)
            {
                var coverUrl = await SafeGetPublicUrlAsync(rc.CoverImageKey ?? rc.CoverImageKey);
                if (string.IsNullOrEmpty(coverUrl) && !string.IsNullOrEmpty(rc.CoverImageKey))
                    coverUrl = await SafeGetPublicUrlAsync(rc.CoverImageKey);

                reactiveVms.Add(new ReactiveCourseCardVm
                {
                    Id = rc.Id,
                    Title = rc.Title,
                    Description = rc.Description,
                    CoverImageUrl = coverUrl,
                    IntroVideoUrl = rc.IntroVideoUrl,
                    StartDate = rc.StartDate,
                    EndDate = rc.EndDate,
                    PricePerMonth = rc.PricePerMonth,
                    PricePerMonthLabel = rc.PricePerMonth.ToEuro(),
                    MonthsCount = rc.DurationMonths
                });
            }

            // Private courses (unchanged)
            var privateCourses = await _db.PrivateCourses
                .AsNoTracking()
                .Where(pc => pc.TeacherId == id && pc.IsPublished)
                .OrderByDescending(pc => pc.Id)
                .ToListAsync();

            var privateVms = new List<PrivateCourseCardVm>();
            foreach (var pc in privateCourses)
            {
                var cover = await SafeGetPublicUrlAsync(pc.CoverImageKey ?? pc.CoverImageUrl);
                privateVms.Add(new PrivateCourseCardVm
                {
                    Id = pc.Id,
                    Title = pc.Title,
                    Description = pc.Description,
                    CoverImageUrl = cover,
                    Price = pc.Price,
                    PriceLabel = pc.Price.ToEuro()
                });
            }

            var detailsVm = new TeacherDetailsVm
            {
                Id = teacher.Id,
                FullName = teacher.User?.FullName ?? teacher.User?.UserName ?? "—",
                PhotoUrl = photo,
                JobTitle = teacher.JobTitle,
                ShortBio = teacher.ShortBio,
                CVUrl = teacher.CVUrl,
                IntroVideoUrl = teacher.IntroVideoUrl,
                DateOfBirth = teacher.User?.DateOfBirth,
                Slots = slotVms,
                ReactiveCourses = reactiveVms,
                PrivateCourses = privateVms
            };

            return View(detailsVm);
        }

        #region Helpers

        // safe wrapper to call file storage; returns null on error
        private async Task<string?> SafeGetPublicUrlAsync(string? keyOrUrl)
        {
            if (string.IsNullOrEmpty(keyOrUrl)) return null;
            try
            {
                var u = await _fileStorage.GetPublicUrlAsync(keyOrUrl);
                return u;
            }
            catch
            {
                // swallow; do not throw from controller
                return null;
            }
        }
        #endregion
    }
}
