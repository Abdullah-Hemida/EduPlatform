using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Web.Areas.Admin.ViewModels;
using Edu.Web.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.Diagnostics;
using System.Globalization;

namespace Edu.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class PurchaseRequestsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<PurchaseRequestsController> _logger;
        private const int PageSize = 10;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public PurchaseRequestsController(ApplicationDbContext db, ILogger<PurchaseRequestsController> logger, IStringLocalizer<SharedResource> localizer)
        {
            _db = db;
            _logger = logger;
            _localizer = localizer;
        }

        // GET: Admin/PurchaseRequests
        public async Task<IActionResult> Index(int? status, string? search, int page = 1)
        {
            var stopwatch = Stopwatch.StartNew();
            _logger.LogInformation("PurchaseRequests.Index start - status={Status}, search={Search}, page={Page}", status, search, page);

            // ✅ Base query (no Include)
            var query = _db.PurchaseRequests
                .AsNoTracking()
                .OrderByDescending(p => p.RequestDateUtc)
                .AsQueryable();

            // ✅ Filtering
            if (status.HasValue)
                query = query.Where(p => p.Status == (PurchaseStatus)status.Value);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p =>
                    p.PrivateCourse.Title.Contains(search) ||
                    p.Student.User.FullName.Contains(search) ||
                    p.Student.User.PhoneNumber.Contains(search));

            // ✅ Pagination calculation
            var totalCount = await query.CountAsync();
            var skip = (page - 1) * PageSize;
            // compute default region from admin UI culture (fallback to IT)
            string defaultRegion = "IT";
            try
            {
                var regionInfo = new RegionInfo(CultureInfo.CurrentUICulture.Name);
                if (!string.IsNullOrEmpty(regionInfo.TwoLetterISORegionName))
                    defaultRegion = regionInfo.TwoLetterISORegionName;
            }
            catch
            {
                defaultRegion = "IT";
            }
            // ✅ Main query with projection
            var items = await query
                .Skip(skip)
                .Take(PageSize)
                .Select(p => new PurchaseRequestListItemVm
                {
                    Id = p.Id,
                    StudentId = p.StudentId,
                    StudentName = p.Student.User.FullName,
                    StudentPhone = p.Student.User.PhoneNumber,
                    PhoneWhatsapp = PhoneHelpers.ToWhatsappDigits(p.Student.User.PhoneNumber, defaultRegion),
                    PrivateCourseId = p.PrivateCourseId,
                    CourseTitle = p.PrivateCourse.Title,
                    TeacherName = p.PrivateCourse.Teacher.User.FullName,
                    RequestDateUtc = p.RequestDateUtc,
                    Status = p.Status.ToString(),
                    Amount = p.Amount,
                    AmountLabel = p.Amount.ToEuro(), // ✅ Converts decimal → formatted string
                    AdminNote = p.AdminNote
                })
                .ToListAsync();

            stopwatch.Stop();
            _logger.LogInformation("PurchaseRequests.Index finished in {Elapsed} ms (totalCount={Total}, pageItems={Count})",
                stopwatch.ElapsedMilliseconds, totalCount, items.Count);

            var model = new PurchaseRequestsIndexViewModel
            {
                Items = items,
                Page = page,
                PageSize = PageSize,
                TotalCount = totalCount,
                Status = status,
                Search = search
            };
            // localized label for the WA button (used by the partial)
            ViewBag.WhatsAppLabel = _localizer["WhatsApp"].Value ?? "WhatsApp";
            ViewData["ActivePage"] = "PurchaseRequests";
            return View(model);
        }

        // POST: Admin/PurchaseRequests/Complete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id)
        {
            var pr = await _db.PurchaseRequests.FindAsync(id);
            if (pr == null) return NotFound();

            pr.Status = PurchaseStatus.Completed;
            _db.PurchaseRequests.Update(pr);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Purchase request marked completed.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/PurchaseRequests/Reject/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var pr = await _db.PurchaseRequests.FindAsync(id);
            if (pr == null) return NotFound();

            pr.Status = PurchaseStatus.Rejected;
            _db.PurchaseRequests.Update(pr);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Purchase request rejected.";
            return RedirectToAction(nameof(Index));
        }

        // Optional: Details
        public async Task<IActionResult> Details(int id)
        {
            var pr = await _db.PurchaseRequests
                .Include(p => p.Student).ThenInclude(s => s.User)
                .Include(p => p.PrivateCourse).ThenInclude(pc => pc.Teacher).ThenInclude(t => t.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pr == null) return NotFound();
            // compute default region from admin UI culture (fallback to IT)
            string defaultRegion = "IT";
            try
            {
                var regionInfo = new RegionInfo(CultureInfo.CurrentUICulture.Name);
                if (!string.IsNullOrEmpty(regionInfo.TwoLetterISORegionName))
                    defaultRegion = regionInfo.TwoLetterISORegionName;
            }
            catch
            {
                defaultRegion = "IT";
            }
            var vm = new PurchaseRequestListItemVm
            {
                Id = pr.Id,
                StudentId = pr.StudentId,
                StudentName = pr.Student?.User?.FullName,
                StudentPhone = pr.Student?.User?.PhoneNumber,
                PhoneWhatsapp = PhoneHelpers.ToWhatsappDigits(pr.Student?.User?.PhoneNumber, defaultRegion),
                GuardianPhoneNumber = pr.Student?.GuardianPhoneNumber,
                GuardianWhatsapp = PhoneHelpers.ToWhatsappDigits(pr.Student?.GuardianPhoneNumber, defaultRegion),
                PrivateCourseId = pr.PrivateCourseId,
                CourseTitle = pr.PrivateCourse?.Title,
                TeacherName = pr.PrivateCourse?.Teacher?.User?.FullName,
                RequestDateUtc = pr.RequestDateUtc,
                Status = pr.Status.ToString(),
                AmountLabel = pr.Amount.ToEuro(),
                AdminNote = pr.AdminNote
            };
            // localized label for the WA button (used by the partial)
            ViewBag.WhatsAppLabel = _localizer["WhatsApp"].Value ?? "WhatsApp";
            ViewData["ActivePage"] = "PurchaseRequests";
            return View(vm);
        }
        // POST: Admin/PurchaseRequests/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var pr = await _db.PurchaseRequests.FindAsync(id);
            if (pr == null) return NotFound();

            if (pr.Status == PurchaseStatus.Completed)
            {
                TempData["Error"] = "Cannot delete a completed purchase request.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // optionally: log who deleted it
                _logger.LogInformation("Admin {User} deleting PurchaseRequest {Id} (Status: {Status})", User?.Identity?.Name, pr.Id, pr.Status);

                _db.PurchaseRequests.Remove(pr);
                await _db.SaveChangesAsync();

                TempData["Success"] = "Purchase request deleted.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete PurchaseRequest {Id}", pr.Id);
                TempData["Error"] = "Failed to delete purchase request.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}

