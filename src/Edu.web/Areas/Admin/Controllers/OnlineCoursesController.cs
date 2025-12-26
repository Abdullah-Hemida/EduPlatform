using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Web.Areas.Admin.ViewModels;
using Edu.Web.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.Linq;

namespace Edu.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class OnlineCoursesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fileStorage;
        private readonly IStringLocalizer<SharedResource> _L;
        private readonly ILogger<OnlineCoursesController> _logger;

        public OnlineCoursesController(
            ApplicationDbContext db,
            IFileStorageService fileStorage,
            IStringLocalizer<SharedResource> localizer,
            ILogger<OnlineCoursesController> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _fileStorage = fileStorage ?? throw new ArgumentNullException(nameof(fileStorage));
            _L = localizer ?? throw new ArgumentNullException(nameof(localizer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // GET: Admin/OnlineCourses
        public async Task<IActionResult> Index()
        {
            var courses = await _db.OnlineCourses
                .AsNoTracking()
                .OrderByDescending(c => c.Id)
                .ToListAsync();

            var vm = courses.Select(c => new OnlineCourseListItemVm
            {
                Id = c.Id,
                Title = c.Title ?? "",
                Description = c.Description,
                PricePerMonthLabel = c.PricePerMonth.ToEuro(),
                DurationMonths = c.DurationMonths,
                IsPublished = c.IsPublished,
                CoverPublicUrl = string.IsNullOrEmpty(c.CoverImageKey) ? null : _fileStorage.GetPublicUrlAsync(c.CoverImageKey).Result // small sync use for projection
            }).ToList();

            ViewData["ActivePage"] = "OnlineCourses";
            return View(vm);
        }

        // GET: Admin/OnlineCourses/Create
        public async Task<IActionResult> Create()
        {
            var vm = new OnlineCourseCreateVm
            {
                DurationMonths = 1,
                Levels = await LoadLevelsSelectListAsync()
            };
            ViewData["ActivePage"] = "OnlineCourses";
            return View(vm);
        }

        // POST: Admin/OnlineCourses/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OnlineCourseCreateVm vm)
        {
            vm.Levels = await LoadLevelsSelectListAsync();

            if (!ModelState.IsValid) return View(vm);

            string? coverKey = null;
            if (vm.CoverImage != null && vm.CoverImage.Length > 0)
            {
                // save image to storage (folder "onlinecourse-covers")
                coverKey = await _fileStorage.SaveFileAsync(vm.CoverImage, "onlinecourse-covers");
            }

            var duration = Math.Max(1, vm.DurationMonths);

            var course = new OnlineCourse
            {
                Title = vm.Title,
                Description = vm.Description,
                CoverImageKey = coverKey,
                IntroductionVideoUrl = vm.IntroductionVideoUrl,
                PricePerMonth = vm.PricePerMonth,
                DurationMonths = duration,
                IsPublished = vm.IsPublished,
                LevelId = vm.LevelId,
                TeacherName = vm.TeacherName,
                CreatedAtUtc = DateTime.UtcNow
            };

            _db.OnlineCourses.Add(course);
            await _db.SaveChangesAsync();

            // create months
            var months = new List<OnlineCourseMonth>();
            for (int i = 1; i <= duration; i++)
            {
                months.Add(new OnlineCourseMonth
                {
                    OnlineCourseId = course.Id,
                    MonthIndex = i,
                    MonthStartUtc = null,
                    MonthEndUtc = null,
                    IsReadyForPayment = false
                });
            }
            if (months.Any())
            {
                _db.OnlineCourseMonths.AddRange(months);
                await _db.SaveChangesAsync();
            }

            TempData["Success"] = _L["OnlineCourse.Created"].Value ?? "Course created.";
            return RedirectToAction(nameof(Details), new { id = course.Id });
        }

        // GET: Admin/OnlineCourses/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var c = await _db.OnlineCourses.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound();

            var vm = new OnlineCourseEditVm
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description,
                IntroductionVideoUrl = c.IntroductionVideoUrl,
                PricePerMonth = c.PricePerMonth,
                DurationMonths = c.DurationMonths,
                LevelId = c.LevelId,
                TeacherName = c.TeacherName,
                IsPublished = c.IsPublished,
                ExistingCoverPublicUrl = string.IsNullOrEmpty(c.CoverImageKey) ? null : await _fileStorage.GetPublicUrlAsync(c.CoverImageKey),
                Levels = await LoadLevelsSelectListAsync()
            };
            ViewData["ActivePage"] = "OnlineCourses";
            return View(vm);
        }

        // POST: Admin/OnlineCourses/Edit
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(OnlineCourseEditVm vm)
        {
            vm.Levels = await LoadLevelsSelectListAsync();
            if (!ModelState.IsValid) return View(vm);

            var c = await _db.OnlineCourses
                .Include(x => x.Months)
                .FirstOrDefaultAsync(x => x.Id == vm.Id);

            if (c == null) return NotFound();

            // cover replacement
            if (vm.NewCoverImage != null && vm.NewCoverImage.Length > 0)
            {
                if (!string.IsNullOrEmpty(c.CoverImageKey))
                {
                    try { await _fileStorage.DeleteFileAsync(c.CoverImageKey); } catch { /* swallow */ }
                }
                c.CoverImageKey = await _fileStorage.SaveFileAsync(vm.NewCoverImage, "onlinecourse-covers");
            }

            // update fields
            c.Title = vm.Title;
            c.Description = vm.Description;
            c.IntroductionVideoUrl = vm.IntroductionVideoUrl;
            c.PricePerMonth = vm.PricePerMonth;
            var newDuration = Math.Max(1, vm.DurationMonths);
            if (newDuration != c.DurationMonths)
            {
                if (newDuration > c.DurationMonths)
                {
                    for (int i = c.DurationMonths + 1; i <= newDuration; i++)
                    {
                        c.Months.Add(new OnlineCourseMonth
                        {
                            OnlineCourseId = c.Id,
                            MonthIndex = i,
                            MonthStartUtc = null,
                            MonthEndUtc = null,
                            IsReadyForPayment = false
                        });
                    }
                }
                else
                {
                    // remove trailing months only if safe (no payments/lessons). If not safe, block (same logic as ReactiveCourses)
                    var toRemove = c.Months.Where(m => m.MonthIndex > newDuration).ToList();
                    foreach (var m in toRemove)
                    {
                        var hasLessons = await _db.OnlineCourseLessons.AnyAsync(l => l.OnlineCourseMonthId == m.Id);
                        var hasPayments = await _db.OnlineEnrollmentMonthPayments.AnyAsync(pm => pm.OnlineCourseMonthId == m.Id);
                        if (hasLessons || hasPayments)
                        {
                            ModelState.AddModelError("", _L["CannotShrinkDurationHasData"].Value ?? "Cannot reduce duration because some months contain lessons or payments.");
                            return View(vm);
                        }
                        _db.OnlineCourseMonths.Remove(m);
                        c.Months.Remove(m);
                    }
                }
                c.DurationMonths = newDuration;
            }

            c.LevelId = vm.LevelId;
            c.TeacherName = vm.TeacherName;
            c.IsPublished = vm.IsPublished;

            _db.OnlineCourses.Update(c);
            await _db.SaveChangesAsync();

            TempData["Success"] = _L["OnlineCourse.Updated"].Value ?? "Course updated.";
            return RedirectToAction(nameof(Details), new { id = c.Id });
        }

        // GET: Admin/OnlineCourses/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var course = await _db.OnlineCourses
                .Include(c => c.Months)
                .ThenInclude(m => m.MonthPayments)
                .Include(c => c.Lessons)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null) return NotFound();
            var lessonIds = course.Lessons.Select(l => l.Id).ToList();
            List<FileResource> files = new();
            if (lessonIds.Any())
            {
                files = await _db.FileResources
                                 .AsNoTracking()
                                 .Where(fr => fr.OnlineCourseLessonId != null && lessonIds.Contains(fr.OnlineCourseLessonId.Value))
                                 .ToListAsync();
            }

            var vm = new OnlineCourseDetailsVm
            {
                Id = course.Id,
                Title = course.Title ?? "",
                Description = course.Description,
                CoverPublicUrl = string.IsNullOrEmpty(course.CoverImageKey) ? null : await _fileStorage.GetPublicUrlAsync(course.CoverImageKey),
                IntroductionVideoUrl = course.IntroductionVideoUrl,
                TeacherName = course.TeacherName,
                PricePerMonthLabel = course.PricePerMonth.ToEuro(),
                DurationMonths = course.DurationMonths,
                LevelId = course.LevelId,
                IsPublished = course.IsPublished,
                Months = course.Months.OrderBy(m => m.MonthIndex).Select(m => new OnlineCourseMonthVm
                {
                    Id = m.Id,
                    MonthIndex = m.MonthIndex,
                    IsReadyForPayment = m.IsReadyForPayment,
                    LessonsCount = course.Lessons.Count(l => l.OnlineCourseMonthId == m.Id),
                }).ToList(),
                Lessons = course.Lessons.OrderBy(l => l.Order).Select(l =>
                {
                    var attached = files
                        .Where(f => f.OnlineCourseLessonId == l.Id)
                        .Select(ff => new OnlineCourseLessonFileVm
                        {
                            Id = ff.Id,
                            FileName = ff.Name ?? System.IO.Path.GetFileName(ff.FileUrl ?? ff.StorageKey ?? ""),
                            PublicUrl = !string.IsNullOrEmpty(ff.StorageKey) ? _fileStorage.GetPublicUrlAsync(ff.StorageKey).Result
                                        : (ff.FileUrl ?? null)
                        }).ToList();

                    return new OnlineCourseLessonVm
                    {
                        Id = l.Id,
                        Title = l.Title,
                        MeetUrl = l.MeetUrl,
                        RecordedVideoUrl = l.RecordedVideoUrl,
                        Notes = l.Notes,
                        ScheduledUtc = l.ScheduledUtc,
                        Order = l.Order,
                        OnlineCourseMonthId = l.OnlineCourseMonthId,
                        Attachments = attached
                    };
                }).ToList()
            };
            var level = await _db.Levels.FindAsync(course.LevelId);
            vm.LevelName = LocalizationHelpers.GetLocalizedLevelName(level);
            ViewData["ActivePage"] = "OnlineCourses";
            return View(vm);
        }

        // POST: Admin/OnlineCourses/AddMonth
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMonth(int courseId)
        {
            var course = await _db.OnlineCourses
                .Include(c => c.Months)
                .FirstOrDefaultAsync(c => c.Id == courseId);

            if (course == null) return NotFound();

            var nextIndex = (course.Months?.Any() ?? false) ? course.Months.Max(m => m.MonthIndex) + 1 : 1;
            var month = new OnlineCourseMonth
            {
                OnlineCourseId = courseId,
                MonthIndex = nextIndex,
                IsReadyForPayment = false
            };
            _db.OnlineCourseMonths.Add(month);
            await _db.SaveChangesAsync();

            TempData["Success"] = _L["OnlineCourse.MonthAdded"].Value ?? "Month added.";
            return RedirectToAction(nameof(Details), new { id = courseId });
        }

        // POST: Admin/OnlineCourses/AddLesson (supports attachments)
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddLesson(OnlineCourseLessonCreateVm vm)
        {
            // server side validation
            if (string.IsNullOrWhiteSpace(vm.Title))
                return BadRequest(new { success = false, message = _L["ReactiveCourse.Validation.TitleRequired"].Value });

            // optional: validate month belongs to course
            var month = await _db.OnlineCourseMonths.Include(m => m.OnlineCourse).FirstOrDefaultAsync(m => m.Id == vm.OnlineCourseMonthId);
            if (month == null) return BadRequest(new { success = false, message = _L["ReactiveCourse.MonthNotFound"].Value });

            var lesson = new OnlineCourseLesson
            {
                OnlineCourseId = month.OnlineCourseId,
                OnlineCourseMonthId = vm.OnlineCourseMonthId,
                Title = vm.Title.Trim(),
                MeetUrl = string.IsNullOrWhiteSpace(vm.MeetUrl) ? null : vm.MeetUrl.Trim(),
                RecordedVideoUrl = string.IsNullOrWhiteSpace(vm.RecordedVideoUrl) ? null : vm.RecordedVideoUrl.Trim(),
                Notes = vm.Notes,
                ScheduledUtc = vm.ScheduledUtc,
                Order = vm.Order
            };

            _db.OnlineCourseLessons.Add(lesson);
            await _db.SaveChangesAsync(); // need id for attachments

            // attachments
            if (vm.Attachments != null && vm.Attachments.Any())
            {
                foreach (var f in vm.Attachments)
                {
                    if (f == null || f.Length == 0) continue;
                    try
                    {
                        var key = await _fileStorage.SaveFileAsync(f, "onlinecourse-lesson-files");
                        var fr = new FileResource
                        {
                            OnlineCourseLessonId = lesson.Id,
                            StorageKey = key,
                            FileUrl = null,
                            Name = Path.GetFileName(f.FileName),
                            FileType = f.ContentType,
                            CreatedAtUtc = DateTime.UtcNow
                        };
                        _db.FileResources.Add(fr);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed saving lesson attachment");
                    }
                }
                await _db.SaveChangesAsync();
            }

            return Json(new { success = true, lessonId = lesson.Id });
        }

        // POST: Admin/OnlineCourses/EditLesson
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditLesson(OnlineCourseLessonEditVm vm)
        {
            if (vm == null || vm.Id <= 0) return BadRequest(new { success = false, message = _L["ReactiveCourse.InvalidLessonId"].Value });

            var lesson = await _db.OnlineCourseLessons.FirstOrDefaultAsync(l => l.Id == vm.Id);
            if (lesson == null) return NotFound();

            // update fields
            lesson.Title = vm.Title.Trim();
            lesson.MeetUrl = string.IsNullOrWhiteSpace(vm.MeetUrl) ? null : vm.MeetUrl.Trim();
            lesson.RecordedVideoUrl = string.IsNullOrWhiteSpace(vm.RecordedVideoUrl) ? null : vm.RecordedVideoUrl.Trim();
            lesson.Notes = vm.Notes;
            lesson.ScheduledUtc = vm.ScheduledUtc;
            lesson.Order = vm.Order;

            _db.OnlineCourseLessons.Update(lesson);
            await _db.SaveChangesAsync();

            if (vm.NewAttachments != null && vm.NewAttachments.Any())
            {
                foreach (var f in vm.NewAttachments)
                {
                    if (f == null || f.Length == 0) continue;
                    try
                    {
                        var key = await _fileStorage.SaveFileAsync(f, "onlinecourse-lesson-files");
                        var fr = new FileResource
                        {
                            OnlineCourseLessonId = lesson.Id,
                            StorageKey = key,
                            FileUrl = null,
                            Name = Path.GetFileName(f.FileName),
                            FileType = f.ContentType,
                            CreatedAtUtc = DateTime.UtcNow
                        };
                        _db.FileResources.Add(fr);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed saving lesson attachment");
                    }
                }
                await _db.SaveChangesAsync();
            }

            return Json(new { success = true });
        }

        // POST: Admin/OnlineCourses/DeleteLesson
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteLesson(int lessonId)
        {
            if (lessonId <= 0)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, message = _L["Admin.BadRequest"].Value ?? "Invalid request." });

                return NotFound();
            }

            var lesson = await _db.OnlineCourseLessons
                .Include(l => l.OnlineCourse)
                .FirstOrDefaultAsync(l => l.Id == lessonId);

            if (lesson == null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, message = _L["Admin.NotFound"].Value ?? "Not found." });

                return NotFound();
            }

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // delete attachments from storage and DB
                var files = await _db.FileResources.Where(f => f.OnlineCourseLessonId == lessonId).ToListAsync();
                foreach (var fr in files)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(fr.StorageKey))
                            await _fileStorage.DeleteFileAsync(fr.StorageKey);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed deleting lesson attachment storageKey={Key}", fr.StorageKey);
                        // continue: we still remove DB rows
                    }
                }
                if (files.Any())
                    _db.FileResources.RemoveRange(files);

                // then remove the lesson itself
                _db.OnlineCourseLessons.Remove(lesson);
                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                // success response
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = true, lessonId = lessonId });

                TempData["Success"] = _L["OnlineCourse.LessonDeleted"].Value ?? "Lesson deleted.";
                return RedirectToAction(nameof(Details), new { area = "Admin", id = lesson.OnlineCourseId });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Failed deleting lesson {LessonId}", lessonId);

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, message = _L["Admin.OperationFailed"].Value ?? "Operation failed." });

                TempData["Error"] = _L["Admin.OperationFailed"].Value ?? "Operation failed.";
                return RedirectToAction(nameof(Details), new { area = "Admin", id = lesson.OnlineCourseId });
            }
        }

        // POST: Admin/OnlineCourses/SetMonthReady
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SetMonthReady(int monthId, bool ready)
        {
            var month = await _db.OnlineCourseMonths.Include(m => m.Lessons).FirstOrDefaultAsync(m => m.Id == monthId);
            if (month == null) return NotFound();

            if (ready)
            {
                var missing = month.Lessons.Any(l => string.IsNullOrEmpty(l.MeetUrl));
                if (missing) { TempData["Error"] = _L["AllLessonsMustHaveMeetUrlBeforeReady"].Value ?? "All lessons must have Meet URL before marking ready."; return RedirectToAction(nameof(Details), new { id = month.OnlineCourseId }); }
            }

            month.IsReadyForPayment = ready;
            _db.OnlineCourseMonths.Update(month);
            await _db.SaveChangesAsync();

            TempData["Success"] = ready ? _L["OnlineCourse.MonthNowReady"].Value ?? "Month now ready" : _L["OnlineCourse.MonthNoLongerReady"].Value ?? "Month not ready";
            return RedirectToAction(nameof(Details), new { id = month.OnlineCourseId });
        }

        // GET: Admin/OnlineCourses/GetCover/5
        // Redirect to the public URL (so <img src="/Admin/OnlineCourses/GetCover/5"> works)
        [HttpGet]
        public async Task<IActionResult> GetCover(int id)
        {
            var c = await _db.OnlineCourses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (c == null || string.IsNullOrEmpty(c.CoverImageKey)) return NotFound();
            var url = await _fileStorage.GetPublicUrlAsync(c.CoverImageKey);
            if (string.IsNullOrEmpty(url)) return NotFound();
            return Redirect(url);
        }

        // POST: Admin/OnlineCourses/Delete/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var course = await _db.OnlineCourses
                .Include(c => c.Months)
                .Include(c => c.Lessons)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null) return NotFound();

            // checks: don't delete if payments or enrollments exist (policy same as reactive)
            var monthIds = course.Months.Select(m => m.Id).ToList();
            var hasPayments = monthIds.Any() && await _db.OnlineEnrollmentMonthPayments.AnyAsync(pm => monthIds.Contains(pm.OnlineCourseMonthId));
            var hasEnrollments = await _db.OnlineEnrollments.AnyAsync(e => e.OnlineCourseId == id);
            var hasLessons = course.Lessons.Any();
            var lessonIds = course.Lessons.Select(l => l.Id).ToList();

            if (hasPayments || hasEnrollments)
            {
                TempData["Error"] = _L["OnlineCourse.DeleteBlockedHasData"].Value ?? "Cannot delete: data exists.";
                return RedirectToAction(nameof(Details), new { id = id });
            }

            // delete attachments & cover
            var files = await _db.FileResources.Where(f => f.OnlineCourseLessonId != null && lessonIds.Contains(f.OnlineCourseLessonId.Value)).ToListAsync();
            foreach (var f in files)
            {
                try { if (!string.IsNullOrEmpty(f.StorageKey)) await _fileStorage.DeleteFileAsync(f.StorageKey); } catch { }
            }
            _db.FileResources.RemoveRange(files);

            if (!string.IsNullOrEmpty(course.CoverImageKey))
            {
                try { await _fileStorage.DeleteFileAsync(course.CoverImageKey); } catch { }
            }

            _db.OnlineCourseLessons.RemoveRange(course.Lessons);
            _db.OnlineCourseMonths.RemoveRange(course.Months);
            _db.OnlineCourses.Remove(course);
            await _db.SaveChangesAsync();

            TempData["Success"] = _L["OnlineCourse.Deleted"].Value ?? "Course deleted.";
            return RedirectToAction(nameof(Index));
        }

        #region Helpers

        private async Task<List<SelectListItem>> LoadLevelsSelectListAsync()
        {
            var levels = await _db.Levels.AsNoTracking().OrderBy(l => l.Order).ToListAsync();
            var list = levels.Select(l => new SelectListItem
            {
                Value = l.Id.ToString(),
                Text = LocalizationHelpers.GetLocalizedLevelName(l)
            }).ToList();
            return list;
        }

        #endregion
    }
}


