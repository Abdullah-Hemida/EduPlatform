using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Web.Areas.Teacher.ViewModels;
using Edu.Web.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Edu.Web.Areas.Teacher.Controllers
{
    [Area("Teacher")]
    [Authorize(Roles = "Teacher")]
    public class ReactiveCoursesController : TeacherBaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fileStorage;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly ILogger<ReactiveCoursesController> _logger;
        private readonly IOptions<ReactiveCourseOptions> _rcOptions;

        public ReactiveCoursesController(
            ApplicationDbContext db,
            IFileStorageService fileStorage,
            UserManager<ApplicationUser> userManager,
            IStringLocalizer<SharedResource> localizer,
            ILogger<ReactiveCoursesController> logger,
            IOptions<ReactiveCourseOptions> rcOptions
            )
        {
            _db = db;
            _fileStorage = fileStorage;
            _userManager = userManager;
            _localizer = localizer;
            _logger = logger;
            _rcOptions = rcOptions;
        }
        // GET: Teacher/ReactiveCourses
        public async Task<IActionResult> Index(string show = "active") // "active" or "archived"
        {
            var teacherId = _userManager.GetUserId(User);
            show = (show ?? "active").ToLowerInvariant();

            var coursesQuery = _db.ReactiveCourses
                .AsNoTracking()
                .Where(c => c.TeacherId == teacherId);

            if (show == "active")
                coursesQuery = coursesQuery.Where(c => !c.IsArchived);
            else if (show == "archived")
                coursesQuery = coursesQuery.Where(c => c.IsArchived);

            var courses = await coursesQuery.OrderByDescending(c => c.Id).ToListAsync();

            // retention config
            var retentionDays = _rcOptions.Value.DeletionRetentionDays;
            var now = DateTime.UtcNow;

            var list = new List<ReactiveCourseListItemVm>();
            foreach (var c in courses)
            {
                // compute helper flags: check if any lessons/payments/enrollments exist
                var monthIds = await _db.ReactiveCourseMonths
                    .Where(m => m.ReactiveCourseId == c.Id)
                    .Select(m => m.Id)
                    .ToListAsync();

                var hasPayments = monthIds.Any() && await _db.ReactiveEnrollmentMonthPayments.AnyAsync(pm => monthIds.Contains(pm.ReactiveCourseMonthId));
                var hasEnrollments = await _db.ReactiveEnrollments.AnyAsync(e => e.ReactiveCourseId == c.Id);
                var hasLessons = await _db.ReactiveCourseLessons.AnyAsync(l => monthIds.Contains(l.ReactiveCourseMonthId));
                var anyMonthReady = await _db.ReactiveCourseMonths.AnyAsync(m => m.ReactiveCourseId == c.Id && m.IsReadyForPayment);

                // immediate delete allowed when course has no enrollments, no payments, no lessons, and no month is marked Ready
                var canImmediateDelete = !hasPayments && !hasEnrollments && !hasLessons && !anyMonthReady;

                // retention passed?
                var retentionPassed = c.ArchivedAtUtc.HasValue
                    ? c.ArchivedAtUtc.Value.AddDays(retentionDays) <= now
                    : c.EndDate.AddDays(retentionDays) <= now;

                var canPermanentlyDelete = c.IsArchived && retentionPassed && !hasPayments && !hasEnrollments;

                list.Add(new ReactiveCourseListItemVm
                {
                    Id = c.Id,
                    Title = c.Title,
                    DurationMonths = c.DurationMonths,
                    PricePerMonthLabel = c.PricePerMonth.ToEuro(),
                    Capacity = c.Capacity,
                    CoverPublicUrl = string.IsNullOrEmpty(c.CoverImageKey) ? null : await _fileStorage.GetPublicUrlAsync(c.CoverImageKey),
                    IsArchived = c.IsArchived,
                    ArchivedAtUtc = c.ArchivedAtUtc,
                    StartDate = c.StartDate,
                    EndDate = c.EndDate,
                    CanImmediateDelete = canImmediateDelete,
                    CanPermanentlyDelete = canPermanentlyDelete
                });
            }

            ViewData["ActivePage"] = "MyReactiveCourses";
            ViewData["Show"] = show; // for the view to set active tab
            return View(list);
        }

        // GET: Teacher/ReactiveCourses/Create
        public IActionResult Create()
        {
            ViewData["ActivePage"] = "MyReactiveCourses";
            var vm = new ReactiveCourseCreateVm();
            return View(vm);
        }

        // POST: Teacher/ReactiveCourses/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ReactiveCourseCreateVm vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var teacherId = _userManager.GetUserId(User);

            string? coverKey = null;
            if (vm.CoverImage != null && vm.CoverImage.Length > 0)
            {
                coverKey = await _fileStorage.SaveFileAsync(vm.CoverImage, "reactive-covers");
            }

            var duration = Math.Max(1, vm.DurationMonths);

            var course = new ReactiveCourse
            {
                TeacherId = teacherId,
                Title = vm.Title,
                Description = vm.Description,
                CoverImageKey = coverKey,
                IntroVideoUrl = vm.IntroVideoUrl,
                StartDate = vm.StartDate ?? DateTime.UtcNow,
                EndDate = vm.EndDate ?? DateTime.UtcNow.AddMonths(duration),
                DurationMonths = duration,
                PricePerMonth = vm.PricePerMonth,
                Capacity = vm.Capacity
            };

            _db.ReactiveCourses.Add(course);
            await _db.SaveChangesAsync();

            // Create months
            var months = new List<ReactiveCourseMonth>();
            for (int i = 1; i <= duration; i++)
            {
                months.Add(new ReactiveCourseMonth
                {
                    ReactiveCourseId = course.Id,
                    MonthIndex = i,
                    MonthStartUtc = course.StartDate.AddMonths(i - 1),
                    MonthEndUtc = course.StartDate.AddMonths(i),
                    IsReadyForPayment = false
                });
            }

            if (months.Any())
            {
                _db.ReactiveCourseMonths.AddRange(months);
                await _db.SaveChangesAsync();
            }

            TempData["Success"] = _localizer["CourseCreated"].Value;

            // 🔥 FIXED: always redirect to the Teacher Area Details page
            return RedirectToAction(
                actionName: nameof(Details),
                controllerName: "ReactiveCourses",
                routeValues: new { area = "Teacher", id = course.Id }
            );
        }

        // GET: Teacher/ReactiveCourses/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var teacherId = _userManager.GetUserId(User);
            var course = await _db.ReactiveCourses.FirstOrDefaultAsync(c => c.Id == id && c.TeacherId == teacherId);
            if (course == null) return NotFound();

            var vm = new ReactiveCourseEditVm
            {
                Id = course.Id,
                Title = course.Title,
                Description = course.Description,
                PricePerMonth = course.PricePerMonth,
                DurationMonths = course.DurationMonths,
                Capacity = course.Capacity,
                IntroVideoUrl = course.IntroVideoUrl,
                StartDate = course.StartDate,
                EndDate = course.EndDate,
                ExistingCoverPublicUrl = string.IsNullOrEmpty(course.CoverImageKey)
                    ? null
                    : await _fileStorage.GetPublicUrlAsync(course.CoverImageKey)
            };
            ViewData["ActivePage"] = "MyReactiveCourses";
            return View(vm);
        }

        // POST: Teacher/ReactiveCourses/Edit
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ReactiveCourseEditVm vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var teacherId = _userManager.GetUserId(User);
            var course = await _db.ReactiveCourses
                                  .Include(c => c.Months)
                                  .FirstOrDefaultAsync(c => c.Id == vm.Id && c.TeacherId == teacherId);
            if (course == null) return NotFound();

            // handle cover image replacement
            if (vm.NewCoverImage != null && vm.NewCoverImage.Length > 0)
            {
                if (!string.IsNullOrEmpty(course.CoverImageKey))
                {
                    await _fileStorage.DeleteFileAsync(course.CoverImageKey);
                }
                var key = await _fileStorage.SaveFileAsync(vm.NewCoverImage, "reactive-covers");
                course.CoverImageKey = key;
            }

            // update main fields
            course.Title = vm.Title;
            course.Description = vm.Description;
            course.PricePerMonth = vm.PricePerMonth;
            // if duration changed, adjust months
            var newDuration = Math.Max(1, vm.DurationMonths);
            if (newDuration != course.DurationMonths)
            {
                // if newDuration > old -> add months
                if (newDuration > course.DurationMonths)
                {
                    for (int i = course.DurationMonths + 1; i <= newDuration; i++)
                    {
                        course.Months.Add(new ReactiveCourseMonth
                        {
                            ReactiveCourseId = course.Id,
                            MonthIndex = i,
                            MonthStartUtc = course.StartDate.AddMonths(i - 1),
                            MonthEndUtc = course.StartDate.AddMonths(i),
                            IsReadyForPayment = false
                        });
                    }
                }
                else
                {
                    // newDuration < old -> remove trailing months only if they have no lessons and no payments
                    var toRemove = course.Months.Where(m => m.MonthIndex > newDuration).ToList();
                    foreach (var m in toRemove)
                    {
                        var hasLessons = await _db.ReactiveCourseLessons.AnyAsync(l => l.ReactiveCourseMonthId == m.Id);
                        var hasPayments = await _db.ReactiveEnrollmentMonthPayments.AnyAsync(pm => pm.ReactiveCourseMonthId == m.Id);
                        if (hasLessons || hasPayments)
                        {
                            // do not allow shrinking if data present
                            ModelState.AddModelError("", _localizer["CannotShrinkDurationHasData"].Value ?? "Cannot reduce duration because some months contain lessons or payments.");
                            return View(vm);
                        }
                        _db.ReactiveCourseMonths.Remove(m);
                        course.Months.Remove(m);
                    }
                }

                course.DurationMonths = newDuration;
                // adjust EndDate if necessary
                course.EndDate = course.StartDate.AddMonths(newDuration);
            }

            course.Capacity = vm.Capacity;
            course.IntroVideoUrl = vm.IntroVideoUrl;
            course.StartDate = vm.StartDate ?? course.StartDate;
            course.EndDate = vm.EndDate ?? course.StartDate.AddMonths(course.DurationMonths);

            _db.ReactiveCourses.Update(course);
            await _db.SaveChangesAsync();
            ViewData["ActivePage"] = "MyReactiveCourses";
            TempData["Success"] = _localizer["CourseUpdated"].Value;
            return RedirectToAction(
                actionName: nameof(Details),
                controllerName: "ReactiveCourses",
                routeValues: new { area = "Teacher", id = course.Id }
            );
        }

        // GET: Teacher/ReactiveCourses/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var teacherId = _userManager.GetUserId(User);
            var course = await _db.ReactiveCourses
                .Include(c => c.Months).ThenInclude(m => m.Lessons)
                .FirstOrDefaultAsync(c => c.Id == id && c.TeacherId == teacherId);

            if (course == null) return NotFound();

            var vm = new ReactiveCourseDetailsVm
            {
                Id = course.Id,
                CoverPublicUrl = string.IsNullOrEmpty(course.CoverImageKey) ? null : await _fileStorage.GetPublicUrlAsync(course.CoverImageKey),
                Title = course.Title,
                Description = course.Description,
                PricePerMonthLabel = course.PricePerMonth.ToEuro(),
                DurationMonths = course.DurationMonths,
                Capacity = course.Capacity,
                IntroVideoUrl = course.IntroVideoUrl,
                Months = course.Months
                            .OrderBy(m => m.MonthIndex)
                            .Select(m => new ReactiveCourseMonthVm
                            {
                                Id = m.Id,
                                MonthIndex = m.MonthIndex,
                                IsReadyForPayment = m.IsReadyForPayment,
                                LessonsCount = m.Lessons.Count,
                                Lessons = m.Lessons
                                            .OrderBy(l => l.ScheduledUtc)
                                            .Select(l => new ReactiveCourseLessonVm
                                            {
                                                Id = l.Id,
                                                Title = l.Title,
                                                ScheduledUtc = l.ScheduledUtc,
                                                MeetUrl = l.MeetUrl,
                                                Notes = l.Notes
                                            }).ToList()
                            }).ToList()
            };
            ViewData["ActivePage"] = "MyReactiveCourses";
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddLessonAjax([FromBody] ReactiveCourseLessonCreateVm vm)
        {
            var month = await _db.ReactiveCourseMonths.Include(m => m.ReactiveCourse).FirstOrDefaultAsync(m => m.Id == vm.ReactiveCourseMonthId);
            if (month == null) return Json(new { success = false, errors = new { general = _localizer["ReactiveCourse.MonthNotFound"].Value } });

            var teacherId = _userManager.GetUserId(User);
            if (month.ReactiveCourse?.TeacherId != teacherId) return Json(new { success = false, errors = new { general = _localizer["Forbid"].Value } });

            var errors = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(vm.Title)) errors["title"] = _localizer["ReactiveCourse.Validation.TitleRequired"].Value;
            if (!string.IsNullOrWhiteSpace(vm.MeetUrl))
            {
                if (!Uri.TryCreate(vm.MeetUrl.Trim(), UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
                    errors["meetUrl"] = _localizer["ReactiveCourse.Validation.MeetUrlInvalid"].Value;
            }
            if (errors.Any()) return Json(new { success = false, errors });

            var lesson = new ReactiveCourseLesson
            {
                ReactiveCourseMonthId = vm.ReactiveCourseMonthId,
                Title = vm.Title,
                ScheduledUtc = vm.ScheduledUtc,
                MeetUrl = string.IsNullOrWhiteSpace(vm.MeetUrl) ? null : vm.MeetUrl.Trim(),
                Notes = vm.Notes
            };

            _db.ReactiveCourseLessons.Add(lesson);
            await _db.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditLessonAjax([FromBody] ReactiveCourseLessonEditDto dto)
        {
            try
            {
                if (dto == null || dto.LessonId <= 0)
                    return Json(new { success = false, errors = new { general = _localizer["ReactiveCourse.InvalidLessonId"].Value } });

                // load lesson and related course for ownership check
                var lesson = await _db.ReactiveCourseLessons
                                      .Include(l => l.ReactiveCourseMonth)
                                        .ThenInclude(m => m.ReactiveCourse)
                                      .FirstOrDefaultAsync(l => l.Id == dto.LessonId);

                if (lesson == null)
                    return Json(new { success = false, errors = new { general = _localizer["ReactiveCourse.LessonNotFound"].Value } });

                var teacherId = _userManager.GetUserId(User);
                if (lesson.ReactiveCourseMonth?.ReactiveCourse?.TeacherId != teacherId)
                    return Json(new { success = false, errors = new { general = _localizer["Forbid"].Value } });

                // server-side validation
                var errors = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                if (string.IsNullOrWhiteSpace(dto.Title))
                    errors["title"] = _localizer["ReactiveCourse.Validation.TitleRequired"].Value;

                if (!string.IsNullOrWhiteSpace(dto.MeetUrl))
                {
                    if (!Uri.TryCreate(dto.MeetUrl.Trim(), UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
                        errors["meetUrl"] = _localizer["ReactiveCourse.Validation.MeetUrlInvalid"].Value;
                }

                if (errors.Any())
                    return Json(new { success = false, errors }); // serializer will produce camelCase keys by default

                // update lesson
                lesson.Title = dto.Title!.Trim();
                lesson.ScheduledUtc = dto.ScheduledUtc;
                lesson.MeetUrl = string.IsNullOrWhiteSpace(dto.MeetUrl) ? null : dto.MeetUrl.Trim();
                lesson.Notes = dto.Notes;

                _db.ReactiveCourseLessons.Update(lesson);
                await _db.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // log the exception (ensure you have an ILogger injected into controller)
                // _logger?.LogError(ex, "EditLessonAjax failed for teacher {TeacherId}", _userManager.GetUserId(User));
                // In dev you can return ex.Message (but avoid in production)
                return Json(new { success = false, errors = new { general = _localizer["Admin.ServerError"].Value } });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetMonthReadyAjax([FromBody] SetMonthReadyAjaxDto dto)
        {
            if (dto == null)
                return BadRequest(new { success = false, message = _localizer["Admin.BadRequest"].Value ?? "Invalid request" });

            var month = await _db.ReactiveCourseMonths
                .Include(m => m.ReactiveCourse)
                .Include(m => m.Lessons)
                .FirstOrDefaultAsync(m => m.Id == dto.MonthId);

            if (month == null)
                return Json(new { success = false, message = _localizer["ReactiveCourse.MonthNotFound"].Value });

            var teacherId = _userManager.GetUserId(User);
            if (month.ReactiveCourse!.TeacherId != teacherId)
                return Json(new { success = false, message = _localizer["Forbid"].Value });

            if (dto.Ready)
            {
                var missing = month.Lessons.Any(l => string.IsNullOrEmpty(l.MeetUrl));
                if (missing)
                    return Json(new { success = false, message = _localizer["AllLessonsMustHaveMeetUrlBeforeReady"].Value });
            }

            month.IsReadyForPayment = dto.Ready;
            _db.ReactiveCourseMonths.Update(month);

            _db.ReactiveCourseModerationLogs.Add(new ReactiveCourseModerationLog
            {
                ReactiveCourseId = month.ReactiveCourseId,
                ActorId = teacherId,
                ActorName = User.Identity?.Name,
                Action = dto.Ready ? "MonthReady" : "MonthUnready",
                Note = dto.Ready
                    ? _localizer["MonthMarkedReady", month.MonthIndex].Value
                    : _localizer["MonthMarkedNotReady", month.MonthIndex].Value,
                CreatedAtUtc = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            var successMsg = dto.Ready ? _localizer["MonthNowReady"].Value : _localizer["MonthNoLongerReady"].Value;

            return Json(new
            {
                success = true,
                message = successMsg,
                monthId = month.Id,
                isReady = month.IsReadyForPayment,
                monthIndex = month.MonthIndex
            });
        }
        // GET: Teacher/ReactiveCourses/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var teacherId = _userManager.GetUserId(User);
            var course = await _db.ReactiveCourses
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id && c.TeacherId == teacherId);

            if (course == null) return NotFound();

            // minimal VM for confirmation
            var vm = new ReactiveCourseDeleteVm
            {
                Id = course.Id,
                Title = course.Title,
                StartDate = course.StartDate,
                EndDate = course.EndDate,
                CoverPublicUrl = string.IsNullOrEmpty(course.CoverImageKey) ? null : await _fileStorage.GetPublicUrlAsync(course.CoverImageKey)             
            };

            ViewData["ActivePage"] = "MyReactiveCourses";
            return View(vm); // returns Views/Teacher/ReactiveCourses/Delete.cshtml
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, string? note, bool forceDelete = false)
        {
            var teacherId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(teacherId)) return Challenge();

            var course = await _db.ReactiveCourses
                .Include(c => c.Months)
                .FirstOrDefaultAsync(c => c.Id == id && c.TeacherId == teacherId);

            if (course == null) return NotFound();

            var monthIds = course.Months?.Select(m => m.Id).ToList() ?? new List<int>();

            // checks
            var hasPayments = monthIds.Any() && await _db.ReactiveEnrollmentMonthPayments.AnyAsync(pm => monthIds.Contains(pm.ReactiveCourseMonthId));
            var hasEnrollments = await _db.ReactiveEnrollments.AnyAsync(e => e.ReactiveCourseId == course.Id);
            var hasLessons = await _db.ReactiveCourseLessons.AnyAsync(l => monthIds.Contains(l.ReactiveCourseMonthId));
            var anyMonthReady = await _db.ReactiveCourseMonths.AnyAsync(m => m.ReactiveCourseId == course.Id && m.IsReadyForPayment);

            // block if payments exist
            if (hasPayments)
            {
                TempData["Error"] = _localizer["ReactiveCourse.Delete.HasPayments"].Value ?? "Cannot delete: some months have payments.";
                return RedirectToAction(
                    actionName: nameof(Index),
                    controllerName: "ReactiveCourses",
                    routeValues: new { area = "Teacher"}
                );
            }

            // block if enrollments exist
            if (hasEnrollments)
            {
                TempData["Error"] = _localizer["ReactiveCourse.Delete.HasEnrollments"].Value ?? "Cannot delete: students are enrolled in this course.";
                return RedirectToAction(
                   actionName: nameof(Index),
                   controllerName: "ReactiveCourses",
                   routeValues: new { area = "Teacher" }
               );
            }

            // immediate permanent delete if truly empty ("created by mistake")
            if (!hasLessons && !hasEnrollments && !hasPayments && !anyMonthReady)
            {
                var ok = await DeleteCoursePermanentlyAsync(course, note ?? "Deleted by teacher (empty course)", teacherId);
                if (ok)
                {
                    TempData["Success"] = _localizer["ReactiveCourse.Deleted"].Value ?? "Course permanently deleted.";
                    return RedirectToAction(nameof(Index));
                }
                TempData["Error"] = _localizer["Admin.OperationFailed"].Value ?? "Operation failed.";
                return RedirectToAction(
                    actionName: nameof(Index),
                    controllerName: "ReactiveCourses",
                    routeValues: new { area = "Teacher" }
                );
            }

            // retention policy
            var now = DateTime.UtcNow;
            var retentionDays = _rcOptions.Value.DeletionRetentionDays;
            var retentionCutoff = course.EndDate.AddDays(retentionDays);

            // If retention not passed and not forced => archive
            if (!course.IsArchived && (now < retentionCutoff) && !forceDelete)
            {
                course.IsArchived = true;
                course.ArchivedAtUtc = now;
                _db.ReactiveCourses.Update(course);

                _db.ReactiveCourseModerationLogs.Add(new ReactiveCourseModerationLog
                {
                    ReactiveCourseId = course.Id,
                    ActorId = teacherId,
                    ActorName = User.Identity?.Name,
                    Action = "Archived",
                    Note = note ?? $"Archived by teacher (retention {retentionDays} days)",
                    CreatedAtUtc = now
                });

                await _db.SaveChangesAsync();

                TempData["Success"] = _localizer["ReactiveCourse.Archived"].Value ?? "Course archived.";
                return RedirectToAction(
                    actionName: nameof(Index),
                    controllerName: "ReactiveCourses",
                    routeValues: new { area = "Teacher" }
                );
            }

            // retention passed OR forceDelete => permanent delete via helper
            if (now >= retentionCutoff || forceDelete)
            {
                var ok = await DeleteCoursePermanentlyAsync(course, note ?? "Deleted permanently (retention passed or forced)", teacherId);
                if (ok)
                {
                    TempData["Success"] = _localizer["ReactiveCourse.Deleted"].Value ?? "Course permanently deleted.";
                    return RedirectToAction(nameof(Index));
                }
                TempData["Error"] = _localizer["Admin.OperationFailed"].Value ?? "Operation failed.";
                return RedirectToAction(
                    actionName: nameof(Index),
                    controllerName: "ReactiveCourses",
                    routeValues: new { area = "Teacher" }
                );
            }

            // fallback (shouldn't reach)
            TempData["Error"] = _localizer["Admin.OperationFailed"].Value ?? "Operation failed.";
            return RedirectToAction(
               actionName: nameof(Index),
               controllerName: "ReactiveCourses",
               routeValues: new { area = "Teacher" }
           );
        }

        // PRIVATE HELPER: perform snapshot + permanent deletion (reusable)
        private async Task<bool> DeleteCoursePermanentlyAsync(ReactiveCourse course, string? note, string actorId)
        {
            if (course == null) return false;

            // create transaction
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // collect related ids/data to snapshot
                var monthIds = await _db.ReactiveCourseMonths
                    .Where(m => m.ReactiveCourseId == course.Id)
                    .Select(m => m.Id)
                    .ToListAsync();

                var months = await _db.ReactiveCourseMonths
                    .Where(m => m.ReactiveCourseId == course.Id)
                    .Include(m => m.Lessons)
                    .ToListAsync();

                var enrollments = await _db.ReactiveEnrollments
                    .Where(e => e.ReactiveCourseId == course.Id)
                    .Include(e => e.MonthPayments)
                    .ToListAsync();

                var payments = monthIds.Any()
                    ? await _db.ReactiveEnrollmentMonthPayments.Where(pm => monthIds.Contains(pm.ReactiveCourseMonthId)).ToListAsync()
                    : new List<ReactiveEnrollmentMonthPayment>();

                // Snapshot object (avoid cycles)
                var snapshotObj = new
                {
                    Course = new
                    {
                        course.Id,
                        course.Title,
                        course.Description,
                        course.StartDate,
                        course.EndDate,
                        course.DurationMonths,
                        course.PricePerMonth,
                        course.Capacity,
                        course.CoverImageKey,
                        course.IntroVideoUrl,
                        course.IsArchived,
                        course.ArchivedAtUtc
                    },
                    Months = months.Select(m => new
                    {
                        m.Id,
                        m.MonthIndex,
                        m.MonthStartUtc,
                        m.MonthEndUtc,
                        m.IsReadyForPayment,
                        Lessons = m.Lessons.Select(l => new { l.Id, l.Title, l.ScheduledUtc, l.MeetUrl, l.Notes })
                    }),
                    Enrollments = enrollments.Select(e => new
                    {
                        e.Id,
                        e.StudentId,
                        e.CreatedAtUtc,
                        e.IsApproved,
                        MonthPayments = e.MonthPayments.Select(pm => new { pm.Id, pm.ReactiveCourseMonthId, pm.Amount, pm.Status, pm.PaymentReference, pm.CreatedAtUtc })
                    }),
                    Payments = payments.Select(p => new { p.Id, p.ReactiveEnrollmentId, p.ReactiveCourseMonthId, p.Amount, p.Status, p.PaymentReference, p.CreatedAtUtc })
                };

                var json = System.Text.Json.JsonSerializer.Serialize(snapshotObj, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
                });

                // save snapshot to storage (best-effort)
                try
                {
                    var snapshotKey = $"reactive-course-deletes/course-{course.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}.json";
                    await _fileStorage.SaveTextFileAsync(snapshotKey, json);
                    // Optionally keep snapshotKey in moderation log note — omitted for brevity
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save snapshot for ReactiveCourse {CourseId}. Proceeding with deletion.", course.Id);
                }

                // delete cover image if present (best-effort)
                if (!string.IsNullOrEmpty(course.CoverImageKey))
                {
                    try
                    {
                        await _fileStorage.DeleteFileAsync(course.CoverImageKey);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete cover image {Key} for ReactiveCourse {CourseId}.", course.CoverImageKey, course.Id);
                    }
                }

                // add moderation log BEFORE removing (so we retain an audit entry, but confirm FK behavior)
                _db.ReactiveCourseModerationLogs.Add(new ReactiveCourseModerationLog
                {
                    ReactiveCourseId = course.Id,
                    ActorId = actorId,
                    ActorName = User.Identity?.Name,
                    Action = "DeletedPermanently",
                    Note = note ?? "Deleted permanently",
                    CreatedAtUtc = DateTime.UtcNow
                });

                // remove months (which will cascade lessons/payments if your FK configured so)
                if (months.Any())
                {
                    _db.ReactiveCourseMonths.RemoveRange(months);
                }

                // finally remove the course
                _db.ReactiveCourses.Remove(course);

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteCoursePermanentlyAsync failed for course {CourseId}", course?.Id);
                try { await tx.RollbackAsync(); } catch { /* swallow */ }
                return false;
            }
        }
    }
}





//// GET: Teacher/ReactiveCourses/AddMonth
//[HttpPost, ValidateAntiForgeryToken]
//public async Task<IActionResult> AddMonth(int courseId, int monthIndex, DateTime? monthStartUtc, DateTime? monthEndUtc)
//{
//    var teacherId = _userManager.GetUserId(User);
//    var course = await _db.ReactiveCourses.FirstOrDefaultAsync(c => c.Id == courseId && c.TeacherId == teacherId);
//    if (course == null) return NotFound();

//    var exists = await _db.ReactiveCourseMonths.AnyAsync(m => m.ReactiveCourseId == courseId && m.MonthIndex == monthIndex);
//    if (exists)
//    {
//        TempData["Error"] = _localizer["MonthAlreadyExists", monthIndex].Value;
//        return RedirectToAction(nameof(Details), new { id = courseId });
//    }

//    var month = new ReactiveCourseMonth
//    {
//        ReactiveCourseId = courseId,
//        MonthIndex = monthIndex,
//        MonthStartUtc = monthStartUtc ?? DateTime.UtcNow,
//        MonthEndUtc = monthEndUtc ?? DateTime.UtcNow.AddMonths(1),
//        IsReadyForPayment = false
//    };

//    _db.ReactiveCourseMonths.Add(month);
//    await _db.SaveChangesAsync();

//    TempData["Success"] = _localizer["MonthAdded", monthIndex].Value;
//    return RedirectToAction(nameof(Details), new { id = courseId });
//}

//// GET: Teacher/ReactiveCourses/AddLesson/{monthId}
//public async Task<IActionResult> AddLesson(int monthId)
//{
//    var month = await _db.ReactiveCourseMonths.Include(m => m.ReactiveCourse)
//        .FirstOrDefaultAsync(m => m.Id == monthId);
//    if (month == null) return NotFound();

//    var teacherId = _userManager.GetUserId(User);
//    if (month.ReactiveCourse!.TeacherId != teacherId) return Forbid();

//    var vm = new ReactiveCourseLessonCreateVm
//    {
//        ReactiveCourseId = month.ReactiveCourseId,
//        ReactiveCourseMonthId = monthId
//    };
//    ViewData["ActivePage"] = "MyReactiveCourses";
//    return View(vm);
//}

//// POST: Teacher/ReactiveCourses/AddLesson
//[HttpPost, ValidateAntiForgeryToken]
//public async Task<IActionResult> AddLesson(ReactiveCourseLessonCreateVm vm)
//{
//    if (string.IsNullOrWhiteSpace(vm.Title)) ModelState.AddModelError(nameof(vm.Title), _localizer["ReactiveCourse.Validation.TitleRequired"].Value);

//    if (!string.IsNullOrWhiteSpace(vm.MeetUrl))
//    {
//        if (!Uri.TryCreate(vm.MeetUrl.Trim(), UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
//            ModelState.AddModelError(nameof(vm.MeetUrl), _localizer["ReactiveCourse.Validation.MeetUrlInvalid"].Value);
//    }

//    var month = await _db.ReactiveCourseMonths.Include(m => m.ReactiveCourse).FirstOrDefaultAsync(m => m.Id == vm.ReactiveCourseMonthId);
//    if (month == null) return NotFound();
//    var teacherId = _userManager.GetUserId(User);
//    if (month.ReactiveCourse?.TeacherId != teacherId) return Forbid();

//    if (!ModelState.IsValid) return View(vm);

//    var lesson = new ReactiveCourseLesson
//    {
//        ReactiveCourseMonthId = vm.ReactiveCourseMonthId,
//        Title = vm.Title,
//        ScheduledUtc = vm.ScheduledUtc,
//        MeetUrl = string.IsNullOrWhiteSpace(vm.MeetUrl) ? null : vm.MeetUrl.Trim(),
//        Notes = vm.Notes
//    };

//    _db.ReactiveCourseLessons.Add(lesson);
//    await _db.SaveChangesAsync();

//    TempData["Success"] = _localizer["LessonAdded"].Value;
//    return RedirectToAction(nameof(Details), new { id = month.ReactiveCourseId });
//}

//// GET: Teacher/ReactiveCourses/EditLesson/{lessonId}
//public async Task<IActionResult> EditLesson(int lessonId)
//{
//    var lesson = await _db.ReactiveCourseLessons
//                          .Include(l => l.ReactiveCourseMonth).ThenInclude(m => m.ReactiveCourse)
//                          .FirstOrDefaultAsync(l => l.Id == lessonId);

//    if (lesson == null) return NotFound();

//    var teacherId = _userManager.GetUserId(User);
//    if (lesson.ReactiveCourseMonth?.ReactiveCourse?.TeacherId != teacherId) return Forbid();

//    var vm = new ReactiveCourseLessonCreateVm
//    {
//        ReactiveCourseId = lesson.ReactiveCourseMonth!.ReactiveCourseId,
//        ReactiveCourseMonthId = lesson.ReactiveCourseMonthId,
//        Title = lesson.Title ?? "",
//        ScheduledUtc = lesson.ScheduledUtc,
//        MeetUrl = lesson.MeetUrl,
//        Notes = lesson.Notes
//    };
//    ViewData["ActivePage"] = "MyReactiveCourses";
//    ViewData["LessonId"] = lessonId;
//    return View(vm);
//}

//// POST: Teacher/ReactiveCourses/EditLesson
//[HttpPost, ValidateAntiForgeryToken]
//public async Task<IActionResult> EditLesson(int lessonId, ReactiveCourseLessonCreateVm vm)
//{
//    var lesson = await _db.ReactiveCourseLessons.Include(l => l.ReactiveCourseMonth).ThenInclude(m => m.ReactiveCourse).FirstOrDefaultAsync(l => l.Id == lessonId);
//    if (lesson == null) return NotFound();

//    var teacherId = _userManager.GetUserId(User);
//    if (lesson.ReactiveCourseMonth?.ReactiveCourse?.TeacherId != teacherId) return Forbid();

//    if (string.IsNullOrWhiteSpace(vm.Title)) ModelState.AddModelError(nameof(vm.Title), _localizer["ReactiveCourse.Validation.TitleRequired"].Value);
//    if (!string.IsNullOrWhiteSpace(vm.MeetUrl))
//    {
//        if (!Uri.TryCreate(vm.MeetUrl.Trim(), UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
//            ModelState.AddModelError(nameof(vm.MeetUrl), _localizer["ReactiveCourse.Validation.MeetUrlInvalid"].Value);
//    }

//    if (!ModelState.IsValid)
//    {
//        ViewData["LessonId"] = lessonId;
//        return View(vm);
//    }

//    lesson.Title = vm.Title;
//    lesson.ScheduledUtc = vm.ScheduledUtc;
//    lesson.MeetUrl = string.IsNullOrWhiteSpace(vm.MeetUrl) ? null : vm.MeetUrl.Trim();
//    lesson.Notes = vm.Notes;

//    _db.ReactiveCourseLessons.Update(lesson);
//    await _db.SaveChangesAsync();

//    TempData["Success"] = _localizer["LessonUpdated"].Value;
//    return RedirectToAction(nameof(Details), new { id = lesson.ReactiveCourseMonth!.ReactiveCourseId });
//}

//// POST: Teacher/ReactiveCourses/SetMonthReady
//[HttpPost, ValidateAntiForgeryToken]
//public async Task<IActionResult> SetMonthReady(int monthId, bool ready)
//{
//    var month = await _db.ReactiveCourseMonths
//        .Include(m => m.ReactiveCourse)
//        .Include(m => m.Lessons)
//        .FirstOrDefaultAsync(m => m.Id == monthId);

//    if (month == null) return NotFound();
//    var teacherId = _userManager.GetUserId(User);
//    if (month.ReactiveCourse!.TeacherId != teacherId) return Forbid();

//    if (ready)
//    {
//        var missing = month.Lessons.Any(l => string.IsNullOrEmpty(l.MeetUrl));
//        if (missing)
//        {
//            TempData["Error"] = _localizer["AllLessonsMustHaveMeetUrlBeforeReady"].Value;
//            return RedirectToAction(nameof(Details), new { id = month.ReactiveCourseId });
//        }
//    }

//    month.IsReadyForPayment = ready;
//    _db.ReactiveCourseMonths.Update(month);

//    _db.ReactiveCourseModerationLogs.Add(new ReactiveCourseModerationLog
//    {
//        ReactiveCourseId = month.ReactiveCourseId,
//        ActorId = teacherId,
//        ActorName = User.Identity?.Name,
//        Action = ready ? "MonthReady" : "MonthUnready",
//        Note = ready
//            ? _localizer["MonthMarkedReady", month.MonthIndex].Value
//            : _localizer["MonthMarkedNotReady", month.MonthIndex].Value,
//        CreatedAtUtc = DateTime.UtcNow
//    });

//    await _db.SaveChangesAsync();

//    //TempData["Success"] = ready
//    //    ? _localizer["MonthNowReady"].Value
//    //    : _localizer["MonthNoLongerReady"].Value;

//    return RedirectToAction(nameof(Details), new { id = month.ReactiveCourseId });
//}