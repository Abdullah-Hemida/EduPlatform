using Edu.Domain.Entities;

namespace Edu.Web.Areas.Student.ViewModels
{
    public class OnlineStudentCourseDetailsVm
    {
        public int CourseId { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string? CoverPublicUrl { get; set; }
        public string? IntroVideoUrl { get; set; }
        public string? IntroYouTubeId { get; set; }
        public decimal PricePerMonth { get; set; }
        public string? PricePerMonthLabel { get; set; }
        public int DurationMonths { get; set; }
        public List<OnlineStudentCourseMonthVm> Months { get; set; } = new();
        public bool IsEnrolled { get; set; }
        public bool HasPendingEnrollment { get; set; }
        public bool HasAnyPaidMonth { get; set; }
        public int? EnrollmentId { get; set; }
    }

    public class OnlineStudentCourseMonthVm
    {
        public int Id { get; set; }
        public int MonthIndex { get; set; }
        public DateTime? MonthStartUtc { get; set; }
        public DateTime? MonthEndUtc { get; set; }
        public bool IsReadyForPayment { get; set; }
        public int LessonsCount { get; set; }
        public int? PaymentId { get; set; }
        public OnlineEnrollmentMonthPaymentStatus? MyPaymentStatus { get; set; }
        public bool CanRequestPayment { get; set; }
        public bool CanCancelPayment { get; set; }
        public bool CanViewLessons { get; set; }
        public List<OnlineStudentCourseLessonVm> Lessons { get; set; } = new();
    }

    public class OnlineStudentCourseLessonVm
    {
        public int Id { get; set; }
        public int? OnlineCourseMonthId { get; set; }
        public string Title { get; set; } = "";
        public DateTime? ScheduledUtc { get; set; }
        public string? MeetUrl { get; set; }
        public string? RecordedVideoUrl { get; set; }
        public string? Notes { get; set; }
        public List<OnlineStudentCourseFileVm> Files { get; set; } = new();
    }

    public class OnlineStudentCourseFileVm
    {
        public int Id { get; set; }
        public string FileName { get; set; } = "";
        public string? PublicUrl { get; set; }
    }

    public class OnlineMyEnrollmentVm
    {
        public int EnrollmentId { get; set; }
        public int CourseId { get; set; }
        public string CourseTitle { get; set; } = "";
        public List<OnlineMyEnrollmentMonthVm> Months { get; set; } = new();
    }

    public class OnlineMyEnrollmentMonthVm
    {
        public int MonthId { get; set; }
        public int MonthIndex { get; set; }
        public bool IsReadyForPayment { get; set; }
        public OnlineEnrollmentMonthPaymentStatus? PaymentStatus { get; set; }
    }
}
