// File: Areas/Admin/Controllers/PrivateCoursesController.cs
using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Infrastructure.Services;
using Edu.Web.Areas.Admin.ViewModels;
using Edu.Web.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;


namespace Edu.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class PrivateCoursesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<PrivateCoursesController> _logger;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IEmailSender _emailSender;
        private readonly IFileStorageService _fileStorage;

        public PrivateCoursesController(
            ApplicationDbContext db,
            IFileStorageService fileStorage,
            UserManager<ApplicationUser> userManager,
            ILogger<PrivateCoursesController> logger,
            IStringLocalizer<SharedResource> localizer,
            IEmailSender emailSender)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _fileStorage = fileStorage ?? throw new ArgumentNullException(nameof(fileStorage));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _localizer = localizer;
            _emailSender = emailSender;
        }

        // GET: Admin/PrivateCourses 
        // Query params: q (search), categoryId (nullable), showPublished (nullable bool), showAll (bool), forChildren (nullable bool), page (int)
        public async Task<IActionResult> Index(string? q, int? categoryId, bool? showPublished, bool showAll = false, bool? forChildren = null, int page = 1)
        {
            const int PageSize = 12;
            ViewData["ActivePage"] = "PrivateCourses";

            // Base query
            var baseQuery = _db.PrivateCourses
                .AsNoTracking()
                .AsQueryable();

            if (!showAll)
            {
                baseQuery = baseQuery.Where(pc => pc.IsPublishRequested == true);
            }
            if (categoryId.HasValue)
            {
                baseQuery = baseQuery.Where(pc => pc.CategoryId == categoryId.Value);
            }
            if (showPublished.HasValue)
            {
                baseQuery = baseQuery.Where(pc => pc.IsPublished == showPublished.Value);
            }

            // NEW: filter for children/adults
            if (forChildren.HasValue)
            {
                baseQuery = baseQuery.Where(pc => pc.IsForChildren == forChildren.Value);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                baseQuery = baseQuery.Where(pc =>
                    (pc.Title != null && EF.Functions.Like(pc.Title, $"%{term}%")) ||
                    (pc.Description != null && EF.Functions.Like(pc.Description, $"%{term}%")) ||
                    (pc.Category != null && pc.Category.Name != null && EF.Functions.Like(pc.Category.Name, $"%{term}%")) ||
                    (pc.Teacher != null && pc.Teacher.User != null &&
                        ((pc.Teacher.User.FullName != null && EF.Functions.Like(pc.Teacher.User.FullName, $"%{term}%")) ||
                         (pc.Teacher.User.Email != null && EF.Functions.Like(pc.Teacher.User.Email, $"%{term}%"))))
                );
            }

            baseQuery = baseQuery.OrderByDescending(pc => pc.Id);

            // Project only DB-translatable fields
            var projected = baseQuery.Select(pc => new PrivateCourseListItemVm
            {
                Id = pc.Id,
                Title = pc.Title,
                CategoryId = pc.CategoryId,
                CategoryName = pc.Category != null ? pc.Category.Name : null,
                Price = pc.Price,                       // numeric, EF-friendly
                IsPublished = pc.IsPublished,
                IsPublishRequested = pc.IsPublishRequested,
                // <-- store the storage key column (not a public url)
                CoverImageKey = pc.CoverImageKey,
                TeacherId = pc.TeacherId,
                TeacherName = pc.Teacher != null && pc.Teacher.User != null ? pc.Teacher.User.FullName : null,
                TeacherEmail = pc.Teacher != null && pc.Teacher.User != null ? pc.Teacher.User.Email : null,
                IsForChildren = pc.IsForChildren
            });

            // Paginate efficiently
            var paged = await PaginatedList<PrivateCourseListItemVm>.CreateAsync(projected, page, PageSize);

            // After materialization: compute price labels and batch resolve cover public URLs
            if (paged != null && paged.Any())
            {
                // 1) Compute PriceLabel in-memory
                foreach (var it in paged)
                {
                    try
                    {
                        it.PriceLabel = it.Price.ToEuro();
                    }
                    catch
                    {
                        it.PriceLabel = it.Price.ToString("0.##");
                    }
                }

                // 2) Batch resolve distinct cover keys to public URLs
                var keys = paged.Select(x => x.CoverImageKey).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
                var resolved = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (var k in keys)
                {
                    try
                    {
                        resolved[k!] = await _fileStorage.GetPublicUrlAsync(k!);
                    }
                    catch
                    {
                        // fallback to key (or null) — keep original value so image can still attempt to use it
                        resolved[k!] = k;
                    }
                }

                foreach (var it in paged)
                {
                    if (!string.IsNullOrEmpty(it.CoverImageKey) && resolved.TryGetValue(it.CoverImageKey!, out var pub))
                        it.PublicCoverUrl = pub;
                    else
                        it.PublicCoverUrl = null;
                }
            }

            var vm = new PrivateCourseIndexVm
            {
                Query = q,
                CategoryId = categoryId,
                ShowPublished = showPublished,
                ShowAll = showAll,
                PageIndex = paged.PageIndex,
                PageSize = paged.PageSize,
                TotalCount = paged.TotalCount,
                Courses = paged,
                ForChildren = forChildren
            };

            // categories for filter
            var categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.CategorySelect = new SelectList(categories, "Id", "Name", categoryId);

            // showPublished select items
            ViewBag.ShowPublishedSelect = new List<SelectListItem>
    {
        new SelectListItem { Value = "true", Text = _localizer != null ? _localizer["Admin.Published"].Value : "Published", Selected = showPublished == true },
        new SelectListItem { Value = "false", Text = _localizer != null ? _localizer["Admin.Unpublished"].Value : "Unpublished", Selected = showPublished == false }
    };

            // ForChildren select
            ViewBag.ForChildrenSelect = new List<SelectListItem>
    {
        new SelectListItem { Value = "", Text = _localizer != null ? _localizer["Common.All"].Value : "All", Selected = forChildren == null },
        new SelectListItem { Value = "true", Text = _localizer != null ? _localizer["Teacher.ForChildren"].Value : "For children", Selected = forChildren == true },
        new SelectListItem { Value = "false", Text = _localizer != null ? _localizer["Teacher.ForAdults"].Value : "For adults", Selected = forChildren == false }
    };

            ViewData["ActivePage"] = "Private Courses";
            return View(vm);
        }


        // GET: Admin/PrivateCourses/Details/5
        public async Task<IActionResult> Details(int id)
        {
            // 1) Basic course projection (lightweight, no collection includes)
            var courseBasic = await _db.PrivateCourses
                .AsNoTracking()
                .Where(pc => pc.Id == id)
                .Select(pc => new
                {
                    pc.Id,
                    pc.Title,
                    pc.Description,
                    pc.CategoryId,
                    CategoryName = pc.Category != null ? pc.Category.Name : null,
                    pc.Price,
                    pc.IsPublished,
                    pc.CoverImageKey,
                    pc.TeacherId,
                    TeacherFullName = pc.Teacher != null && pc.Teacher.User != null ? pc.Teacher.User.FullName : null,
                    TeacherEmail = pc.Teacher != null && pc.Teacher.User != null ? pc.Teacher.User.Email : null
                })
                .FirstOrDefaultAsync();

            if (courseBasic == null) return NotFound();

            // Map the simple fields into VM
            var courseVm = new PrivateCourseDetailsVm
            {
                Id = courseBasic.Id,
                Title = courseBasic.Title,
                Description = courseBasic.Description,
                CategoryId = courseBasic.CategoryId,
                CategoryName = courseBasic.CategoryName,
                PriceLabel = courseBasic.Price.ToEuro(),
                IsPublished = courseBasic.IsPublished,
                CoverImageKey = courseBasic.CoverImageKey,
                Teacher = new TeacherVm
                {
                    Id = courseBasic.TeacherId,
                    FullName = courseBasic.TeacherFullName,
                    Email = courseBasic.TeacherEmail
                }
            };
            // Resolve public cover url (best-effort)
            if (!string.IsNullOrEmpty(courseVm.CoverImageKey))
            {
                try
                {
                    courseVm.PublicCoverUrl = await _fileStorage.GetPublicUrlAsync(courseVm.CoverImageKey);
                }
                catch { courseVm.PublicCoverUrl = null; }
            }
            // 2) Load modules (sequential)
            var modules = await _db.PrivateModules
                .AsNoTracking()
                .Where(m => m.PrivateCourseId == id)
                .OrderBy(m => m.Order)
                .ToListAsync();

            // 3) Load lessons with files (sequential)
            var lessonsWithFiles = await _db.PrivateLessons
                .AsNoTracking()
                .Where(l => l.PrivateCourseId == id)
                .Include(l => l.Files)
                .ToListAsync();

            // 4) Load moderation logs (sequential)
            var moderationLogs = await _db.CourseModerationLogs // adjust table name if different: CourseModerationLogs or similar
                .AsNoTracking()
                .Where(l => l.PrivateCourseId == id)
                .OrderByDescending(l => l.CreatedAtUtc)
                .ToListAsync();

            // 5) Build admin lookup for moderation logs to avoid per-log DB calls
            var adminIds = moderationLogs
                .Select(l => l.AdminId)
                .Where(aid => !string.IsNullOrEmpty(aid))
                .Distinct()
                .ToList();

            var adminLookup = new Dictionary<string, (string FullName, string Email)>();
            if (adminIds.Any())
            {
                var admins = await _db.Users
                    .AsNoTracking()
                    .Where(u => adminIds.Contains(u.Id))
                    .Select(u => new { u.Id, u.FullName, u.Email })
                    .ToListAsync();

                adminLookup = admins.ToDictionary(a => a.Id, a => (a.FullName ?? "", a.Email ?? ""));
            }

            // 6) Map modules + lessons into VMs
            // prepare a dictionary of lessons by module id for quick grouping
            var lessonsByModule = lessonsWithFiles
                .GroupBy(l => l.PrivateModuleId ?? 0)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Order).ToList());

            foreach (var m in modules)
            {
                var moduleVm = new PrivateModuleVm
                {
                    Id = m.Id,
                    Title = m.Title,
                    Description = m.Description,
                    Order = m.Order,
                    Lessons = new List<PrivateLessonVm>()
                };

                if (lessonsByModule.TryGetValue(m.Id, out var lessonsForModule))
                {
                    foreach (var l in lessonsForModule)
                    {
                        var lessonVm = new PrivateLessonVm
                        {
                            Id = l.Id,
                            Title = l.Title,
                            Order = l.Order,
                            YouTubeVideoId = l.YouTubeVideoId,
                            VideoUrl = l.VideoUrl,
                            Files = l.Files?.Select(f => new FileResourceVm
                            {
                                Id = f.Id,
                                Name = f.Name,
                                StorageKey = (f.GetType().GetProperty("StorageKey") != null)
                                    ? (string?)f.GetType().GetProperty("StorageKey")!.GetValue(f)
                                    : (string?)null,
                                FileUrl = f.FileUrl,
                                FileType = f.FileType
                            }).ToList() ?? new List<FileResourceVm>()
                        };

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
                        Order = l.Order,
                        YouTubeVideoId = l.YouTubeVideoId,
                        VideoUrl = l.VideoUrl,
                        Files = l.Files?.Select(f => new FileResourceVm
                        {
                            Id = f.Id,
                            Name = f.Name,
                            StorageKey = (f.GetType().GetProperty("StorageKey") != null)
                                ? (string?)f.GetType().GetProperty("StorageKey")!.GetValue(f)
                                : (string?)null,
                            FileUrl = f.FileUrl,
                            FileType = f.FileType
                        }).ToList() ?? new List<FileResourceVm>()
                    };

                    courseVm.StandaloneLessons.Add(lvm);
                }
            }

            // 7) Map moderation logs into VMs using the adminLookup
            courseVm.ModerationLogs = moderationLogs.Select(log =>
            {
                var adminName = log.AdminId != null && adminLookup.TryGetValue(log.AdminId, out var adm) && !string.IsNullOrEmpty(adm.FullName)
                    ? adm.FullName
                    : (adminLookup.TryGetValue(log.AdminId ?? "", out var adm2) ? adm2.Email : log.AdminId);

                var adminEmail = log.AdminId != null && adminLookup.TryGetValue(log.AdminId, out var adm3) ? adm3.Email : null;

                return new CourseModerationLogVm
                {
                    Id = log.Id,
                    Action = log.Action,
                    Note = log.Note,
                    AdminId = log.AdminId,
                    AdminName = adminName,
                    AdminEmail = adminEmail,
                    CreatedAtUtc = log.CreatedAtUtc
                };
            }).ToList();

            // 9) Resolve file public URLs in batch (distinct keys)
            var fileKeys = new List<string>();
            foreach (var m in courseVm.Modules)
            {
                foreach (var l in m.Lessons)
                {
                    if (l.Files == null) continue;
                    foreach (var f in l.Files)
                    {
                        var key = f.StorageKey ?? f.FileUrl;
                        if (!string.IsNullOrEmpty(key)) fileKeys.Add(key);
                    }
                }
            }
            foreach (var l in courseVm.StandaloneLessons)
            {
                if (l.Files == null) continue;
                foreach (var f in l.Files)
                {
                    var key = f.StorageKey ?? f.FileUrl;
                    if (!string.IsNullOrEmpty(key)) fileKeys.Add(key);
                }
            }

            var distinctKeys = fileKeys.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var k in distinctKeys)
            {
                try
                {
                    var pub = await _fileStorage.GetPublicUrlAsync(k, TimeSpan.FromHours(1));
                    map[k] = pub;
                }
                catch
                {
                    map[k] = k;
                }
            }

            // populate FileResourceVm.PublicUrl
            foreach (var m in courseVm.Modules)
            {
                foreach (var l in m.Lessons)
                {
                    if (l.Files == null) continue;
                    foreach (var f in l.Files)
                    {
                        var key = f.StorageKey ?? f.FileUrl;
                        f.PublicUrl = key != null && map.TryGetValue(key, out var p) ? p : key;
                    }
                }
            }
            foreach (var l in courseVm.StandaloneLessons)
            {
                if (l.Files == null) continue;
                foreach (var f in l.Files)
                {
                    var key = f.StorageKey ?? f.FileUrl;
                    f.PublicUrl = key != null && map.TryGetValue(key, out var p) ? p : key;
                }
            }

            ViewData["ActivePage"] = "Private Courses";
            return View(courseVm);
        }

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Approve(int id)
        //{
        //    var course = await _db.PrivateCourses.Include(pc => pc.Teacher).ThenInclude(t => t.User).FirstOrDefaultAsync(pc => pc.Id == id);
        //    if (course == null) return NotFound();

        //    course.IsPublished = true;
        //    course.IsPublishRequested = false; // clear request
        //    var adminId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        //    _db.CourseModerationLogs.Add(new CourseModerationLog
        //    {
        //        PrivateCourseId = course.Id,
        //        AdminId = adminId,
        //        Action = "Approved",
        //        Note = null,
        //        CreatedAtUtc = DateTime.UtcNow
        //    });

        //    try
        //    {
        //        await _db.SaveChangesAsync();

        //        // Send email to teacher if email available
        //        var teacherEmail = course.Teacher?.User?.Email;
        //        if (!string.IsNullOrEmpty(teacherEmail))
        //        {
        //            var subject = $"Your course '{course.Title}' was approved";
        //            var body = $"Hello {course.Teacher?.User?.FullName ?? ""},<br/><br/>" +
        //                       $"Your course <strong>{course.Title}</strong> has been approved by the admin and is now live on the platform.<br/><br/>" +
        //                       "Regards,<br/>Edu Platform Team";
        //            try { await _emailSender.SendEmailAsync(teacherEmail, subject, body); }
        //            catch (Exception ex) { _logger.LogWarning(ex, "Failed sending approval email for course {CourseId}", id); }
        //        }

        //        TempData["Success"] = "Course approved and published.";
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error approving course {CourseId}", id);
        //        TempData["Error"] = "Unable to approve course.";
        //    }

        //    return RedirectToAction(nameof(Details), new { id });
        //}


        //// POST: Admin/PrivateCourses/Reject/5
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Reject(int id, string? adminNote)
        //{
        //    var course = await _db.PrivateCourses.Include(pc => pc.Teacher).ThenInclude(t => t.User).FirstOrDefaultAsync(pc => pc.Id == id);
        //    if (course == null) return NotFound();

        //    course.IsPublished = false;
        //    course.IsPublishRequested = false;

        //    var adminId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        //    _db.CourseModerationLogs.Add(new CourseModerationLog
        //    {
        //        PrivateCourseId = course.Id,
        //        AdminId = adminId,
        //        Action = "Rejected",
        //        Note = adminNote,
        //        CreatedAtUtc = DateTime.UtcNow
        //    });

        //    try
        //    {
        //        await _db.SaveChangesAsync();

        //        // send email to teacher with note
        //        var teacherEmail = course.Teacher?.User?.Email;
        //        if (!string.IsNullOrEmpty(teacherEmail))
        //        {
        //            var subject = $"Your course '{course.Title}' was not approved";
        //            var body = $"Hello {course.Teacher?.User?.FullName ?? ""},<br/><br/>" +
        //                       $"Your course <strong>{course.Title}</strong> was not approved by our admin.<br/><br/>" +
        //                       $"Admin note: <blockquote>{System.Net.WebUtility.HtmlEncode(adminNote ?? "No note provided")}</blockquote><br/>" +
        //                       "Please review the note and update your course accordingly.<br/><br/>Regards,<br/>Edu Platform Team";
        //            try { await _emailSender.SendEmailAsync(teacherEmail, subject, body); }
        //            catch (Exception ex) { _logger.LogWarning(ex, "Failed sending rejection email for course {CourseId}", id); }
        //        }
        //        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
        //        {
        //            return Json(new { success = true });
        //        }

        //        TempData["Success"] = "Course rejected.";
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error rejecting course {CourseId}", id);
        //        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
        //        {
        //            return StatusCode(500, new { success = false, message = "Unable to update course." });
        //        }
        //        TempData["Error"] = "Unable to update course.";
        //    }

        //    return RedirectToAction(nameof(Details), new { id });
        //}

        // POST: Admin/PrivateCourses/TogglePublish/5 (AJAX-friendly)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePublish(int id)
        {
            var course = await _db.PrivateCourses.FindAsync(id);
            if (course == null) return NotFound();

            course.IsPublished = !course.IsPublished;

            // log action
            var adminId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            _db.CourseModerationLogs.Add(new Edu.Domain.Entities.CourseModerationLog
            {
                PrivateCourseId = course.Id,
                AdminId = adminId,
                Action = course.IsPublished ? "Published" : "Unpublished",
                Note = null,
                CreatedAtUtc = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            return Json(new { success = true, isPublished = course.IsPublished });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var course = await _db.PrivateCourses.FindAsync(id);
            if (course == null) return NotFound();

            _db.PrivateCourses.Remove(course);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Course deleted.";
            return RedirectToAction(nameof(Index));
        }

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> RequestPublish(int id)
        //{
        //    var user = await _userManager.GetUserAsync(User);
        //    if (user == null) return Challenge();

        //    var course = await _db.PrivateCourses.FindAsync(id);
        //    if (course == null) return NotFound();
        //    if (course.TeacherId != user.Id) return Forbid();

        //    course.IsPublishRequested = !course.IsPublishRequested;

        //    _db.CourseModerationLogs.Add(new CourseModerationLog
        //    {
        //        PrivateCourseId = course.Id,
        //        AdminId = user.Id,
        //        Action = course.IsPublishRequested ? "PublishRequested" : "PublishRequestCancelled",
        //        Note = null,
        //        CreatedAtUtc = DateTime.UtcNow
        //    });

        //    await _db.SaveChangesAsync();

        //    TempData["Success"] = course.IsPublishRequested
        //        ? _localizer["Admin.PublishRequestSent"].Value
        //        : _localizer["Admin.PublishRequestCancelledMsg"].Value;

        //    return RedirectToAction("Details", new { id });
        //}
    }
}
