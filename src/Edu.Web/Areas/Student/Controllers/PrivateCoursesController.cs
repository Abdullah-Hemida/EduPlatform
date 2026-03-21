using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Web.Areas.Student.ViewModels;
using Edu.Web.Helpers;
using Edu.Web.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

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
        private readonly INotificationService _notifier;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<PrivateCoursesController> _logger;

        public PrivateCoursesController(
            ApplicationDbContext db,
            IFileStorageService fileStorage,
            UserManager<ApplicationUser> userManager,
            IStringLocalizer<SharedResource> localizer,
            INotificationService notifier,
            IWebHostEnvironment env,
            ILogger<PrivateCoursesController> logger)
        {
            _db = db;
            _fileStorage = fileStorage;
            _userManager = userManager;
            _localizer = localizer;
            _notifier = notifier;
            _env = env;
            _logger = logger;
        }

        // GET: Student/PrivateCourses/Details/5  
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken = default)
        {
            // lightweight projection including category localized parts (avoid loading big graph)
            var courseBasic = await _db.PrivateCourses
                .AsNoTracking()
                .Where(c => c.Id == id)
                .Select(pc => new
                {
                    pc.Id,
                    pc.Title,
                    pc.Description,
                    pc.CategoryId,
                    CategoryNameEn = pc.Category != null ? pc.Category.NameEn : null,
                    CategoryNameIt = pc.Category != null ? pc.Category.NameIt : null,
                    CategoryNameAr = pc.Category != null ? pc.Category.NameAr : null,
                    pc.Price,
                    pc.IsPublished,
                    pc.CoverImageKey,
                    pc.TeacherId,
                    // projection of teacher info (may be null)
                    TeacherFullName = pc.Teacher != null && pc.Teacher.User != null ? pc.Teacher.User.FullName : null,
                    TeacherEmail = pc.Teacher != null && pc.Teacher.User != null ? pc.Teacher.User.Email : null
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (courseBasic == null) return NotFound();

            var userId = _userManager.GetUserId(User);

            // determine if current (authenticated Student) purchased the course
            var isPurchased = false;
            if (!string.IsNullOrEmpty(userId))
            {
                isPurchased = await _db.PurchaseRequests
                    .AsNoTracking()
                    .AnyAsync(pr => pr.PrivateCourseId == id && pr.StudentId == userId && pr.Status == PurchaseStatus.Completed, cancellationToken);
            }

            var courseVm = new PrivateCourseDetailsVm
            {
                Id = courseBasic.Id,
                Title = courseBasic.Title,
                Description = courseBasic.Description,
                CategoryId = courseBasic.CategoryId,
                CategoryNameEn = courseBasic.CategoryNameEn,
                CategoryNameIt = courseBasic.CategoryNameIt,
                CategoryNameAr = courseBasic.CategoryNameAr,
                PriceLabel = courseBasic.Price.ToEuro(),
                Price = courseBasic.Price,
                IsPublished = courseBasic.IsPublished,
                CoverImageKey = courseBasic.CoverImageKey,
                Teacher = new PrivateCourseTeacherVm
                {
                    Id = courseBasic.TeacherId,
                    FullName = courseBasic.TeacherFullName,
                    Email = courseBasic.TeacherEmail
                },
                IsPurchased = isPurchased
            };

            // --- NEW: populate convenient flat TeacherName used by views that expect it ---
            courseVm.TeacherName = courseVm.Teacher?.FullName ?? courseVm.Teacher?.Email ?? string.Empty;

            // set localized CategoryName
            courseVm.CategoryName = LocalizationHelpers.GetLocalizedCategoryName(new Edu.Domain.Entities.Category
            {
                NameEn = courseVm.CategoryNameEn ?? string.Empty,
                NameIt = courseVm.CategoryNameIt ?? string.Empty,
                NameAr = courseVm.CategoryNameAr ?? string.Empty
            });

            // resolve cover public url (best-effort)
            if (!string.IsNullOrEmpty(courseVm.CoverImageKey))
            {
                try
                {
                    courseVm.CoverPublicUrl = await _fileStorage.GetPublicUrlAsync(courseVm.CoverImageKey, TimeSpan.FromHours(1));
                }
                catch
                {
                    courseVm.CoverPublicUrl = null;
                }
            }

            // load modules + lessons + files in 2 queries (modules + lessons-with-files)
            var modules = await _db.PrivateModules
                .AsNoTracking()
                .Where(m => m.PrivateCourseId == id)
                .OrderBy(m => m.Order)
                .ToListAsync(cancellationToken);

            var lessonsWithFiles = await _db.PrivateLessons
                .AsNoTracking()
                .Where(l => l.PrivateCourseId == id)
                .Include(l => l.Files)
                .ToListAsync(cancellationToken);

            // --- NEW: set total lessons count (includes standalone lessons) ---
            courseVm.TotalLessons = lessonsWithFiles.Count;

            // group lessons by module id (null => 0)
            var lessonsByModule = lessonsWithFiles
                .GroupBy(l => l.PrivateModuleId ?? 0)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Order).ToList());

            // helper: normalize urls (~/ -> Url.Content, ensure root-relative or absolute)
            string? NormalizeUrl(string? u)
            {
                if (string.IsNullOrWhiteSpace(u)) return null;
                u = u.Trim().Trim('"', '\'');
                if (u.StartsWith("~")) u = Url.Content(u);
                if (Uri.TryCreate(u, UriKind.Absolute, out _)) return u;
                if (!u.StartsWith("/")) u = "/" + u.TrimStart('/');
                return u;
            }

            // map modules & lessons to VMs and collect file keys to resolve in batch
            var fileKeys = new List<string>();
            foreach (var m in modules)
            {
                // --- NEW: use lessonsByModule to get the lesson count for this module ---
                var lessonCountForModule = lessonsByModule.TryGetValue(m.Id, out var lessonsForThisModule)
                    ? lessonsForThisModule.Count
                    : 0;

                var moduleVm = new PrivateModuleVm
                {
                    Id = m.Id,
                    Title = m.Title,
                    Order = m.Order,
                    LessonCount = lessonCountForModule
                };

                if (lessonsForThisModule != null)
                {
                    foreach (var l in lessonsForThisModule)
                    {
                        var lessonVm = new PrivateLessonVm
                        {
                            Id = l.Id,
                            Title = l.Title,
                            YouTubeVideoId = l.YouTubeVideoId,
                            VideoUrl = l.VideoUrl,
                            PrivateModuleId = l.PrivateModuleId,
                            PrivateCourseId = l.PrivateCourseId,
                            Order = l.Order
                        };

                        if (l.Files != null)
                        {
                            foreach (var f in l.Files)
                            {
                                lessonVm.Files.Add(new FileResourceVm
                                {
                                    Id = f.Id,
                                    Name = f.Name,
                                    StorageKey = f.StorageKey,
                                    FileUrl = f.FileUrl,
                                    FileType = f.FileType
                                });
                                if (!string.IsNullOrEmpty(f.StorageKey ?? f.FileUrl))
                                    fileKeys.Add(f.StorageKey ?? f.FileUrl);
                            }
                        }

                        moduleVm.Lessons.Add(lessonVm);
                    }
                }

                courseVm.Modules.Add(moduleVm);
            }

            // standalone lessons (moduleId == null -> key 0)
            if (lessonsByModule.TryGetValue(0, out var standalone))
            {
                foreach (var l in standalone)
                {
                    var lvm = new PrivateLessonVm
                    {
                        Id = l.Id,
                        Title = l.Title,
                        YouTubeVideoId = l.YouTubeVideoId,
                        VideoUrl = l.VideoUrl,
                        PrivateModuleId = l.PrivateModuleId,
                        PrivateCourseId = l.PrivateCourseId,
                        Order = l.Order
                    };

                    if (l.Files != null)
                    {
                        foreach (var f in l.Files)
                        {
                            lvm.Files.Add(new FileResourceVm
                            {
                                Id = f.Id,
                                Name = f.Name,
                                StorageKey = f.StorageKey,
                                FileUrl = f.FileUrl,
                                FileType = f.FileType
                            });
                            if (!string.IsNullOrEmpty(f.StorageKey ?? f.FileUrl))
                                fileKeys.Add(f.StorageKey ?? f.FileUrl);
                        }
                    }

                    courseVm.StandaloneLessons.Add(lvm);
                }
            }

            // Resolve distinct file keys -> public urls (parallel)
            var distinctKeys = fileKeys.Where(k => !string.IsNullOrEmpty(k)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (distinctKeys.Any())
            {
                try
                {
                    var urlTasks = distinctKeys.Select(k => _fileStorage.GetPublicUrlAsync(k!)).ToArray();
                    var urls = await Task.WhenAll(urlTasks);

                    // map key -> normalized url (or fallback to the key)
                    var map = distinctKeys.Select((k, i) => new { Key = k, Url = NormalizeUrl(urls[i]) ?? NormalizeUrl(k) })
                                          .ToDictionary(x => x.Key!, x => x.Url, StringComparer.OrdinalIgnoreCase);

                    // assign PublicUrl and DownloadUrl for each file VM
                    foreach (var m in courseVm.Modules)
                    {
                        foreach (var l in m.Lessons)
                        {
                            foreach (var f in l.Files)
                            {
                                var key = f.StorageKey ?? f.FileUrl;
                                if (!string.IsNullOrEmpty(key) && map.TryGetValue(key, out var pu) && !string.IsNullOrEmpty(pu))
                                    f.PublicUrl = pu;
                                else
                                    f.PublicUrl = NormalizeUrl(f.FileUrl ?? f.StorageKey);

                                // server fallback - safe endpoint that enforces access
                                f.DownloadUrl = Url.Action("Download", "FileResources", new { area = "Admin", id = f.Id });
                            }
                        }
                    }

                    foreach (var l in courseVm.StandaloneLessons)
                    {
                        foreach (var f in l.Files)
                        {
                            var key = f.StorageKey ?? f.FileUrl;
                            if (!string.IsNullOrEmpty(key) && map.TryGetValue(key, out var pu) && !string.IsNullOrEmpty(pu))
                                f.PublicUrl = pu;
                            else
                                f.PublicUrl = NormalizeUrl(f.FileUrl ?? f.StorageKey);

                            f.DownloadUrl = Url.Action("Download", "FileResources", new { area = "Admin", id = f.Id });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Some file public URL lookups failed for course {CourseId}", id);
                    // fallback code (unchanged)...
                    foreach (var m in courseVm.Modules)
                    {
                        foreach (var l in m.Lessons)
                        {
                            foreach (var f in l.Files)
                            {
                                f.PublicUrl = NormalizeUrl(f.FileUrl ?? f.StorageKey);
                                f.DownloadUrl = Url.Action("Download", "FileResources", new { area = "Admin", id = f.Id });
                            }
                        }
                    }

                    foreach (var l in courseVm.StandaloneLessons)
                    {
                        foreach (var f in l.Files)
                        {
                            f.PublicUrl = NormalizeUrl(f.FileUrl ?? f.StorageKey);
                            f.DownloadUrl = Url.Action("Download", "FileResources", new { area = "Admin", id = f.Id });
                        }
                    }
                }
            }
            else
            {
                // no keys to resolve — normalize existing values and add DownloadUrl fallback
                foreach (var m in courseVm.Modules)
                    foreach (var l in m.Lessons)
                        foreach (var f in l.Files)
                        {
                            f.PublicUrl = NormalizeUrl(f.FileUrl ?? f.StorageKey);
                            f.DownloadUrl = Url.Action("Download", "FileResources", new { area = "Admin", id = f.Id });
                        }

                foreach (var l in courseVm.StandaloneLessons)
                    foreach (var f in l.Files)
                    {
                        f.PublicUrl = NormalizeUrl(f.FileUrl ?? f.StorageKey);
                        f.DownloadUrl = Url.Action("Download", "FileResources", new { area = "Admin", id = f.Id });
                    }
            }

            ViewData["ActivePage"] = "MyPurchaseRequests";
            return View(courseVm);
        }

        // POST: Student/PrivateCourses/RequestPurchase
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestPurchase(int PrivateCourseId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var course = await _db.PrivateCourses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == PrivateCourseId && c.IsPublished);
            if (course == null) return NotFound();

            var existsCompleted = await _db.PurchaseRequests.AsNoTracking()
                .AnyAsync(p => p.PrivateCourseId == PrivateCourseId && p.StudentId == user.Id && p.Status == PurchaseStatus.Completed);
            if (existsCompleted)
            {
                TempData["Info"] = "Purchase.AlreadyCompleted";
                return RedirectToAction("Details", new { id = PrivateCourseId });
            }

            var pending = await _db.PurchaseRequests.FirstOrDefaultAsync(p => p.PrivateCourseId == PrivateCourseId && p.StudentId == user.Id && p.Status == PurchaseStatus.Pending);
            if (pending != null)
            {
                TempData["Info"] = "Purchase.AlreadyPending";
                return RedirectToAction("Details", new { id = PrivateCourseId });
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

            // notify admins (fire-and-forget / awaited in dev)
            await NotifyFireAndForgetAsync(async () =>
            {
                await _notifier.NotifyAllAdminsAsync(
                    "Email.Admin.Purchase.Requested.Subject",
                    "Email.Admin.Purchase.Requested.Body",
                    async admin => new object[] { course.Title, user.FullName ?? user.UserName ?? user.Email ?? user.Id, course.Price.ToString("C"), PrivateCourseId }
                );
            });

            TempData["Success"] = "Purchase.RequestSent";
            return RedirectToAction("Details", new { id = PrivateCourseId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelPurchase(int PrivateCourseId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var pending = await _db.PurchaseRequests.FirstOrDefaultAsync(p => p.PrivateCourseId == PrivateCourseId && p.StudentId == user.Id && p.Status == PurchaseStatus.Pending);
            if (pending == null)
            {
                TempData["Error"] = "Purchase.NoPending";
                return RedirectToAction("Details", new { id = PrivateCourseId });
            }

            pending.Status = PurchaseStatus.Cancelled;
            pending.RequestDateUtc = DateTime.UtcNow;
            _db.PurchaseRequests.Update(pending);
            await _db.SaveChangesAsync();

            // notify admins
            await NotifyFireAndForgetAsync(async () =>
            {
                await _notifier.NotifyAllAdminsAsync(
                    "Email.Admin.Purchase.Cancelled.Subject",
                    "Email.Admin.Purchase.Cancelled.Body",
                    async admin => new object[] { pending.PrivateCourse?.Title ?? "", user.FullName ?? user.UserName ?? user.Email ?? user.Id, PrivateCourseId }
                );
            });

            TempData["Success"] = "Purchase.Cancelled";
            return RedirectToAction("Details", new { id = PrivateCourseId });
        }

        // GET: Student/PrivateCourses/MyPurchases
        public async Task<IActionResult> MyPurchases()
        {
            var userId = _userManager.GetUserId(User);

            var purchases = await _db.PurchaseRequests
                .AsNoTracking()
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
                    Amount = p.Amount,
                    AmountLabel = p.Amount.ToEuro(),
                    // CoverImageKey will be resolved below
                    CoverPublicUrl = null
                }).ToList()
            };

            // resolve cover public urls in batch (avoid blocking calls)
            var coverKeys = purchases
                .Select(p => p.PrivateCourse?.CoverImageKey)
                .Where(k => !string.IsNullOrEmpty(k))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (coverKeys.Any())
            {
                try
                {
                    var tasks = coverKeys.Select(k => _fileStorage.GetPublicUrlAsync(k!)).ToArray();
                    var urls = await Task.WhenAll(tasks);
                    var map = coverKeys.Select((k, i) => new { Key = k, Url = urls[i] }).ToDictionary(x => x.Key!, x => x.Url);

                    foreach (var item in vm.Purchases)
                    {
                        var course = purchases.FirstOrDefault(p => p.Id == item.Id)?.PrivateCourse;
                        var key = course?.CoverImageKey;
                        if (!string.IsNullOrEmpty(key) && map.TryGetValue(key, out var pu))
                            item.CoverPublicUrl = pu;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed resolving some cover urls in MyPurchases");
                    // fall back: leave null
                }
            }

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
                // set TempData key instead of localized string
                TempData["Error"] = "Purchase.NotCompleted";
                return RedirectToAction(nameof(Details), new { id = courseId });
            }

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
                    StorageKey = f.StorageKey,
                    FileUrl = f.FileUrl
                }).ToList()
            };

            // resolve file public urls
            var keys = vm.Files.Select(f => f.StorageKey ?? f.FileUrl).Where(k => !string.IsNullOrEmpty(k)).Distinct().ToList();
            if (keys.Any())
            {
                try
                {
                    var tasks = keys.Select(k => _fileStorage.GetPublicUrlAsync(k!)).ToArray();
                    var urls = await Task.WhenAll(tasks);
                    var map = keys.Select((k, i) => new { Key = k, Url = urls[i] }).ToDictionary(x => x.Key!, x => x.Url);

                    foreach (var f in vm.Files)
                    {
                        var key = f.StorageKey ?? f.FileUrl;
                        if (!string.IsNullOrEmpty(key) && map.TryGetValue(key, out var pu))
                            f.PublicUrl = pu;
                        else
                            f.PublicUrl = key;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed resolving lesson files for lesson {LessonId}", lesson.Id);
                    foreach (var f in vm.Files)
                        f.PublicUrl = f.FileUrl ?? f.StorageKey;
                }
            }

            ViewData["ActivePage"] = "MyPurchaseRequests";
            return View(vm);
        }

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
                _logger.LogError(ex, "NotifyFireAndForget failed");
            }
        }
    }

}




