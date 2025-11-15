using Edu.Infrastructure.Data;
using Edu.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Edu.Web.Areas.Student.ViewModels;
using Edu.Application.IServices;
using Edu.Web.Resources;
using Edu.Infrastructure.Services;
using Edu.Infrastructure.Helpers;

namespace Edu.Web.Areas.Student.Controllers
{
    [Area("Student")]
    [Authorize(Roles = "Student")]
    public class ReactiveCoursesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fileStorage;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IEmailSender _emailSender;

        public ReactiveCoursesController(
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

        // GET: Student/ReactiveCourses
        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var courses = await _db.ReactiveCourses
                .Include(c => c.Months)
                .OrderByDescending(c => c.Id)
                .ToListAsync();

            var vm = new List<StudentReactiveCourseListItemVm>();
            foreach (var c in courses)
            {
                string? cover = null;
                if (!string.IsNullOrEmpty(c.CoverImageKey))
                {
                    cover = await _fileStorage.GetPublicUrlAsync(c.CoverImageKey);
                }

                vm.Add(new StudentReactiveCourseListItemVm
                {
                    Id = c.Id,
                    Title = c.Title,
                    Description = c.Description,
                    PricePerMonthLabel = c.PricePerMonth.ToEuro(),
                    DurationMonths = c.DurationMonths,
                    Capacity = c.Capacity,
                    CoverPublicUrl = cover,
                    ReadyMonthsCount = c.Months.Count(m => m.IsReadyForPayment)
                });
            }

            return View(vm);
        }

        // GET: Student/ReactiveCourses/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var course = await _db.ReactiveCourses
                .Include(c => c.Months).ThenInclude(m => m.Lessons)
                .Include(c => c.Enrollments)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null) return NotFound();

            string? cover = null;
            if (!string.IsNullOrEmpty(course.CoverImageKey))
            {
                cover = await _fileStorage.GetPublicUrlAsync(course.CoverImageKey);
            }

            var studentId = _userManager.GetUserId(User);
            var enrollment = await _db.ReactiveEnrollments
                .Include(e => e.MonthPayments)
                .FirstOrDefaultAsync(e => e.ReactiveCourseId == course.Id && e.StudentId == studentId);

            var vm = new StudentReactiveCourseDetailsVm
            {
                Id = course.Id,
                Title = course.Title,
                Description = course.Description,
                CoverPublicUrl = cover,
                PricePerMonthLabel = course.PricePerMonth.ToEuro(),
                DurationMonths = course.DurationMonths,
                Capacity = course.Capacity,
                IntroVideoUrl = course.IntroVideoUrl,
                Months = course.Months.OrderBy(m => m.MonthIndex).Select(m => new StudentCourseMonthVm
                {
                    Id = m.Id,
                    MonthIndex = m.MonthIndex,
                    IsReadyForPayment = m.IsReadyForPayment,
                    LessonsCount = m.Lessons.Count,
                    MyPaymentStatus = enrollment == null ? null :
                        enrollment.MonthPayments.FirstOrDefault(mp => mp.ReactiveCourseMonthId == m.Id)?.Status,
                    Lessons = m.Lessons.OrderBy(l => l.ScheduledUtc).Select(l => new StudentCourseLessonVm
                    {
                        Id = l.Id,
                        Title = l.Title,
                        ScheduledUtc = l.ScheduledUtc,
                        MeetUrl = null // intentionally null for now; will show only if paid
                    }).ToList()
                }).ToList()
            };

            // if student has paid for any month, fill meet urls for that month
            if (enrollment != null)
            {
                foreach (var monthVm in vm.Months)
                {
                    var mp = enrollment.MonthPayments.FirstOrDefault(x => x.ReactiveCourseMonthId == monthVm.Id && x.Status == EnrollmentMonthPaymentStatus.Paid);
                    if (mp != null)
                    {
                        // populate meet urls from DB lessons
                        var lessons = await _db.ReactiveCourseLessons.Where(l => l.ReactiveCourseMonthId == monthVm.Id).OrderBy(l => l.ScheduledUtc).ToListAsync();
                        foreach (var lessonVm in monthVm.Lessons)
                        {
                            var dbLesson = lessons.FirstOrDefault(l => l.Id == lessonVm.Id);
                            if (dbLesson != null) lessonVm.MeetUrl = dbLesson.MeetUrl;
                        }
                    }
                }
            }

            return View(vm);
        }
    }
}

