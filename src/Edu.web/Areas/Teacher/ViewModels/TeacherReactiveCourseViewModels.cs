
namespace Edu.Web.Areas.Teacher.ViewModels
{
    public class ReactiveCourseListItemVm
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public int DurationMonths { get; set; }
        public string? PricePerMonthLabel { get; set; }
        public int Capacity { get; set; }
        public string? CoverPublicUrl { get; set; }

        // new
        public bool IsArchived { get; set; }
        public DateTime? ArchivedAtUtc { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        // helpers for UI
        public bool CanPermanentlyDelete { get; set; } // retention-based
        public bool CanImmediateDelete { get; set; } // created-by-mistake: no lessons/enrollments/payments
    }


    public class ReactiveCourseCreateVm
    {
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public IFormFile? CoverImage { get; set; }
        public string? IntroVideoUrl { get; set; }
        public decimal PricePerMonth { get; set; }
        public int DurationMonths { get; set; } = 1;
        public int Capacity { get; set; } = 0;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class ReactiveCourseEditVm : ReactiveCourseCreateVm
    {
        public int Id { get; set; }
        public IFormFile? NewCoverImage { get; set; }

        public string? ExistingCoverPublicUrl { get; set; }
    }

    public class ReactiveCourseDetailsVm
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }

        public string? CoverPublicUrl { get; set; }
        public string? IntroVideoUrl { get; set; }
        public decimal PricePerMonth { get; set; }
        public string? PricePerMonthLabel { get; set; }
        public int DurationMonths { get; set; }
        public int Capacity { get; set; }
        public List<ReactiveCourseMonthVm> Months { get; set; } = new();
    }
    public class ReactiveCourseDeleteVm
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? CoverPublicUrl { get; set; }
        public string Note { get; set; } = "";
    }
    public class ReactiveCourseMonthVm
    {
        public int Id { get; set; }
        public int MonthIndex { get; set; }
        public bool IsReadyForPayment { get; set; }
        public int LessonsCount { get; set; }

        public List<ReactiveCourseLessonVm> Lessons { get; set; } = new();
    }

    public class ReactiveCourseLessonVm
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public DateTime? ScheduledUtc { get; set; }
        public string? MeetUrl { get; set; }
        public string? Notes { get; set; }
    }

    public class ReactiveCourseLessonCreateVm
    {
        public int ReactiveCourseId { get; set; }
        public int ReactiveCourseMonthId { get; set; }
        public string Title { get; set; } = "";
        public DateTime? ScheduledUtc { get; set; }
        public string? MeetUrl { get; set; }
        public string? Notes { get; set; }
    }

    // ReactiveCourseLessonEditDto.cs
    public class ReactiveCourseLessonEditDto
    {
        public int LessonId { get; set; }
        public int ReactiveCourseId { get; set; }
        public int ReactiveCourseMonthId { get; set; }
        public string? Title { get; set; }
        public DateTime? ScheduledUtc { get; set; }
        public string? MeetUrl { get; set; }
        public string? Notes { get; set; }
    }
    public class SetMonthReadyAjaxDto
    {
        public int MonthId { get; set; }
        public bool Ready { get; set; }
    }
}
