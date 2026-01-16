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
        private readonly IStringLocalizer<SharedResource> _localizer;
        private const int PageSize = 10;

        public PurchaseRequestsController(ApplicationDbContext db, ILogger<PurchaseRequestsController> logger, IStringLocalizer<SharedResource> localizer)
        {
            _db = db;
            _logger = logger;
            _localizer = localizer;
        }

        // GET: Admin/PurchaseRequests
        public async Task<IActionResult> Index(int? status, string? search, int page = 1, CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("PurchaseRequests.Index start - status={Status}, search={Search}, page={Page}", status, search, page);

            // base query (no Include; projection later)
            var query = _db.PurchaseRequests
                .AsNoTracking()
                .OrderByDescending(p => p.RequestDateUtc)
                .AsQueryable();

            if (status.HasValue)
                query = query.Where(p => p.Status == (PurchaseStatus)status.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                var like = $"%{term}%";
                // EF will translate navigation property access in projection; using Like here for DB-side filtering
                query = query.Where(p =>
                    EF.Functions.Like(p.PrivateCourse!.Title!, like) ||
                    EF.Functions.Like(p.Student!.User!.FullName!, like) ||
                    EF.Functions.Like(p.Student!.User!.PhoneNumber!, like));
            }

            // total for pagination
            var totalCount = await query.CountAsync(cancellationToken);

            var skip = Math.Max(0, (page - 1)) * PageSize;

            // compute default region once
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

            // projection: only required fields (no Include)
            var items = await query
                .Skip(skip)
                .Take(PageSize)
                .Select(p => new PurchaseRequestListItemVm
                {
                    Id = p.Id,
                    StudentId = p.StudentId,
                    StudentName = p.Student!.User!.FullName,
                    StudentPhone = p.Student!.User!.PhoneNumber,
                    PhoneWhatsapp = PhoneHelpers.ToWhatsappDigits(p.Student!.User!.PhoneNumber, defaultRegion),
                    PrivateCourseId = p.PrivateCourseId,
                    CourseTitle = p.PrivateCourse!.Title,
                    TeacherName = p.PrivateCourse!.Teacher!.User!.FullName,
                    RequestDateUtc = p.RequestDateUtc,
                    // <-- assign enum (NOT string)
                    Status = p.Status,
                    Amount = p.Amount,
                    AmountLabel = p.Amount.ToEuro(),
                    AdminNote = p.AdminNote
                })
                .ToListAsync(cancellationToken);

            sw.Stop();
            _logger.LogInformation("PurchaseRequests.Index finished in {Elapsed} ms (totalCount={Total}, pageItems={Count})",
                sw.ElapsedMilliseconds, totalCount, items.Count);

            var model = new PurchaseRequestsIndexViewModel
            {
                Items = items,
                Page = page,
                PageSize = PageSize,
                TotalCount = totalCount,
                Status = status,
                Search = search
            };

            ViewBag.WhatsAppLabel = _localizer["WhatsApp"].Value ?? "WhatsApp";
            ViewData["ActivePage"] = "PurchaseRequests";
            return View(model);
        }

        // POST: Admin/PurchaseRequests/Complete/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id, CancellationToken cancellationToken = default)
        {
            var pr = await _db.PurchaseRequests.FindAsync(new object[] { id }, cancellationToken);
            if (pr == null) return NotFound();

            pr.Status = PurchaseStatus.Completed;
            _db.PurchaseRequests.Update(pr);
            await _db.SaveChangesAsync(cancellationToken);

            // set TempData to the key name (centralized alert will localize)
            TempData["Success"] = "PurchaseRequest.Completed";
            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/PurchaseRequests/Reject/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, CancellationToken cancellationToken = default)
        {
            var pr = await _db.PurchaseRequests.FindAsync(new object[] { id }, cancellationToken);
            if (pr == null) return NotFound();

            pr.Status = PurchaseStatus.Rejected;
            _db.PurchaseRequests.Update(pr);
            await _db.SaveChangesAsync(cancellationToken);

            TempData["Success"] = "PurchaseRequest.Rejected";
            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/PurchaseRequests/Details/5
        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken = default)
        {
            var pr = await _db.PurchaseRequests
                .AsNoTracking()
                .Where(p => p.Id == id)
                .Select(p => new
                {
                    p.Id,
                    p.StudentId,
                    StudentFullName = p.Student!.User!.FullName,
                    StudentPhone = p.Student!.User!.PhoneNumber,
                    p.Student!.GuardianPhoneNumber,
                    PrivateCourseId = p.PrivateCourseId,
                    CourseTitle = p.PrivateCourse!.Title,
                    TeacherName = p.PrivateCourse!.Teacher!.User!.FullName,
                    p.RequestDateUtc,
                    // <-- enum value
                    Status = p.Status,
                    Amount = p.Amount,
                    AdminNote = p.AdminNote
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (pr == null) return NotFound();

            // region calculation
            string defaultRegion = "IT";
            try
            {
                var regionInfo = new RegionInfo(CultureInfo.CurrentUICulture.Name);
                if (!string.IsNullOrEmpty(regionInfo.TwoLetterISORegionName))
                    defaultRegion = regionInfo.TwoLetterISORegionName;
            }
            catch { defaultRegion = "IT"; }

            var vm = new PurchaseRequestListItemVm
            {
                Id = pr.Id,
                StudentId = pr.StudentId,
                StudentName = pr.StudentFullName,
                StudentPhone = pr.StudentPhone,
                PhoneWhatsapp = PhoneHelpers.ToWhatsappDigits(pr.StudentPhone, defaultRegion),
                GuardianPhoneNumber = pr.GuardianPhoneNumber,
                GuardianWhatsapp = PhoneHelpers.ToWhatsappDigits(pr.GuardianPhoneNumber, defaultRegion),
                PrivateCourseId = pr.PrivateCourseId,
                CourseTitle = pr.CourseTitle,
                TeacherName = pr.TeacherName,
                RequestDateUtc = pr.RequestDateUtc,
                // <-- assign enum directly
                Status = pr.Status,
                AmountLabel = pr.Amount.ToEuro(),
                AdminNote = pr.AdminNote
            };

            ViewBag.WhatsAppLabel = _localizer["WhatsApp"].Value ?? "WhatsApp";
            ViewData["ActivePage"] = "PurchaseRequests";
            return View(vm);
        }

        // POST: Admin/PurchaseRequests/Delete/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default)
        {
            var pr = await _db.PurchaseRequests.FindAsync(new object[] { id }, cancellationToken);
            if (pr == null) return NotFound();

            if (pr.Status == PurchaseStatus.Completed)
            {
                // set key, not localized string
                TempData["Error"] = "PurchaseRequest.CannotDeleteCompleted";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _logger.LogInformation("Admin {User} deleting PurchaseRequest {Id} (Status: {Status})", User?.Identity?.Name, pr.Id, pr.Status);
                _db.PurchaseRequests.Remove(pr);
                await _db.SaveChangesAsync(cancellationToken);

                TempData["Success"] = "PurchaseRequest.Deleted";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete PurchaseRequest {Id}", pr.Id);
                TempData["Error"] = "PurchaseRequest.DeleteFailed";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}



