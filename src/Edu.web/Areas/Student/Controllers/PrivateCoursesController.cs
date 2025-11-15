using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Services;
using Edu.Web.Areas.Student.ViewModels;
using Edu.Web.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Edu.Infrastructure.Helpers;

namespace Edu.Web.Areas.Student.Controllers
{
    [Area("Student")]
    [Authorize(Roles = "Student")]
    public class PrivateCoursesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fileStorage;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IEmailSender _emailSender;

        public PrivateCoursesController(
            ApplicationDbContext db,
            IFileStorageService fileStorage,
            UserManager<ApplicationUser> userManager,
            IStringLocalizer<SharedResource> localizer,
            IEmailSender emailSender)
        {
            _db = db;
            _fileStorage = fileStorage;
            _userManager = userManager;
            _localizer = localizer;
            _emailSender = emailSender;
        }

        // GET: Student/PrivateCourses/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var course = await _db.PrivateCourses
                .Include(c => c.Teacher).ThenInclude(t => t.User)
                .Include(c => c.Category)
                .Include(c => c.PrivateModules).ThenInclude(m => m.PrivateLessons).ThenInclude(l => l.Files)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            var purchase = await _db.PurchaseRequests.FirstOrDefaultAsync(p => p.PrivateCourseId == id && p.StudentId == userId && p.Status == PurchaseStatus.Completed);

            var vm = new PrivateCourseDetailsVm
            {
                Id = course.Id,
                Title = course.Title,
                Description = course.Description,
                CoverPublicUrl = !string.IsNullOrEmpty(course.CoverImageUrl) ? await _fileStorage.GetPublicUrlAsync(course.CoverImageUrl) : null,
                TeacherName = course.Teacher?.User?.FullName,
                CategoryName = course.Category?.Name,
                PriceLabel = course.Price.ToEuro(),
                IsPublished = course.IsPublished,
                IsPurchased = purchase != null,
                TotalLessons = course.PrivateModules?.Sum(m => m.PrivateLessons.Count) ?? 0,
                Modules = course.PrivateModules.OrderBy(m => m.Order).Select(m => new PrivateModuleVm
                {
                    Id = m.Id,
                    Title = m.Title,
                    Lessons = m.PrivateLessons.OrderBy(l => l.Order).Select(l => new PrivateLessonVm
                    {
                        Id = l.Id,
                        Title = l.Title,
                        YouTubeVideoId = l.YouTubeVideoId,
                        VideoUrl = l.VideoUrl,
                        Files = l.Files.Select(f => new FileResourceVm
                        {
                            Id = f.Id,
                            Name = f.Name,
                            FileType = f.FileType,
                            PublicUrl = string.IsNullOrEmpty(f.StorageKey) ? f.FileUrl : null // will fill public url below
                        }).ToList()
                    }).ToList()
                }).ToList()
            };

            // Resolve file public URLs (async)
            foreach (var module in vm.Modules)
            {
                foreach (var lesson in module.Lessons)
                {
                    for (int i = 0; i < lesson.Files.Count; i++)
                    {
                        var fr = lesson.Files[i];
                        if (string.IsNullOrEmpty(fr.PublicUrl) && !string.IsNullOrEmpty(fr.Name))
                        {
                            // find corresponding FileResource entity to get StorageKey
                            var fileEntity = course.PrivateModules.SelectMany(mm => mm.PrivateLessons).SelectMany(ll => ll.Files).FirstOrDefault(x => x.Id == fr.Id);
                            if (fileEntity != null && !string.IsNullOrEmpty(fileEntity.StorageKey))
                            {
                                fr.PublicUrl = await _fileStorage.GetPublicUrlAsync(fileEntity.StorageKey);
                            }
                            else if (!string.IsNullOrEmpty(fileEntity?.FileUrl))
                            {
                                fr.PublicUrl = fileEntity.FileUrl;
                            }
                        }
                    }
                }
            }
            ViewData["ActivePage"] = "MyPurchaseRequests";
            return View(vm);
        }

        // POST: Student/PrivateCourses/RequestPurchase
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestPurchase(int PrivateCourseId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var course = await _db.PrivateCourses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == PrivateCourseId && c.IsPublished);
            if (course == null) return NotFound();

            var existsCompleted = await _db.PurchaseRequests
                .AsNoTracking()
                .AnyAsync(p => p.PrivateCourseId == PrivateCourseId && p.StudentId == user.Id && p.Status == PurchaseStatus.Completed);

            if (existsCompleted)
            {
                TempData["Info"] = _localizer["Purchase.AlreadyCompleted"].Value;
                return RedirectToAction("Details", "PrivateCourses", new { area = "", id = PrivateCourseId });
            }

            var pending = await _db.PurchaseRequests
                .FirstOrDefaultAsync(p => p.PrivateCourseId == PrivateCourseId && p.StudentId == user.Id && p.Status == PurchaseStatus.Pending);

            if (pending != null)
            {
                TempData["Info"] = _localizer["Purchase.AlreadyPending"].Value;
                return RedirectToAction("Details", "PrivateCourses", new { area = "", id = PrivateCourseId });
            }

            _db.PurchaseRequests.Add(new PurchaseRequest
            {
                PrivateCourseId = PrivateCourseId,
                StudentId = user.Id,
                Amount = course.Price,
                RequestDateUtc = DateTime.UtcNow,
                Status = PurchaseStatus.Pending
            });
            await _db.SaveChangesAsync();
            TempData["Success"] = _localizer["Purchase.RequestSent"].Value;
            return RedirectToAction("Details", "PrivateCourses", new { area = "", id = PrivateCourseId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelPurchase(int PrivateCourseId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var pending = await _db.PurchaseRequests
                .FirstOrDefaultAsync(p => p.PrivateCourseId == PrivateCourseId && p.StudentId == user.Id && p.Status == PurchaseStatus.Pending);

            if (pending == null)
            {
                TempData["Error"] = _localizer["Purchase.NoPending"].Value;
                return RedirectToAction("Details", "PrivateCourses", new { area = "", id = PrivateCourseId });
            }

            pending.Status = PurchaseStatus.Cancelled;
            pending.RequestDateUtc = DateTime.UtcNow;

            _db.PurchaseRequests.Update(pending);
            await _db.SaveChangesAsync();

            TempData["Success"] = _localizer["Purchase.Cancelled"].Value;
            return RedirectToAction("Details", "PrivateCourses", new { area = "", id = PrivateCourseId });
        }

        // GET: Student/PrivateCourses/MyPurchases
        public async Task<IActionResult> MyPurchases()
        {
            var userId = _userManager.GetUserId(User);
            var purchases = await _db.PurchaseRequests
                .Where(p => p.StudentId == userId)
                .Include(p => p.PrivateCourse).ThenInclude(c => c.Teacher).ThenInclude(t => t.User)
                .OrderByDescending(p => p.RequestDateUtc)
                .ToListAsync();

            var vm = new MyPurchasesVm
            {
                Purchases = purchases.Select(p => new MyPurchaseItemVm
                {
                    Id = p.Id,
                    PrivateCourseId = p.PrivateCourseId,
                    CourseTitle = p.PrivateCourse?.Title ?? "",
                    RequestDateUtc = p.RequestDateUtc,
                    Status = p.Status,
                    TeacherName = p.PrivateCourse?.Teacher?.User?.FullName,
                    CoverPublicUrl = string.IsNullOrEmpty(p.PrivateCourse?.CoverImageUrl) ? null : _fileStorage.GetPublicUrlAsync(p.PrivateCourse.CoverImageUrl).Result
                }).ToList()
            };
            ViewData["ActivePage"] = "MyPurchaseRequests";
            return View(vm);
        }

        // GET: Student/PrivateCourses/Lesson/123  (view single lesson — only for purchased students)
        public async Task<IActionResult> Lesson(int id)
        {
            var lesson = await _db.PrivateLessons
                .Include(l => l.PrivateCourse).ThenInclude(c => c.PrivateModules)
                .Include(l => l.Files)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (lesson == null) return NotFound();

            var courseId = lesson.PrivateCourseId;
            var userId = _userManager.GetUserId(User);

            var purchase = await _db.PurchaseRequests.FirstOrDefaultAsync(p => p.PrivateCourseId == courseId && p.StudentId == userId && p.Status == PurchaseStatus.Completed);
            if (purchase == null)
            {
                TempData["Error"] = _localizer["Purchase.NotCompleted"].Value ?? "You must purchase the course to view lessons.";
                return RedirectToAction(nameof(Details), new { id = courseId });
            }

            // build lesson VM
            var vm = new PrivateLessonVm
            {
                Id = lesson.Id,
                Title = lesson.Title,
                YouTubeVideoId = lesson.YouTubeVideoId,
                VideoUrl = lesson.VideoUrl,
                Files = lesson.Files.Select(f => new FileResourceVm
                {
                    Id = f.Id,
                    Name = f.Name,
                    FileType = f.FileType,
                    PublicUrl = string.IsNullOrEmpty(f.StorageKey) ? f.FileUrl : null
                }).ToList()
            };

            // resolve file urls
            foreach (var fr in vm.Files)
            {
                var fileEntity = lesson.Files.FirstOrDefault(x => x.Id == fr.Id);
                if (fileEntity != null && !string.IsNullOrEmpty(fileEntity.StorageKey))
                {
                    fr.PublicUrl = await _fileStorage.GetPublicUrlAsync(fileEntity.StorageKey);
                }
                else if (!string.IsNullOrEmpty(fileEntity?.FileUrl))
                {
                    fr.PublicUrl = fileEntity.FileUrl;
                }
            }
            ViewData["ActivePage"] = "MyPurchaseRequests";
            return View(vm);
        }
    }
}

