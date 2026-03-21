using System.ComponentModel.DataAnnotations;

namespace Edu.Web.ViewModels
{
    public class ReactiveCourseIndexVm
    {
        public string? Query { get; set; }
        public List<ReactiveCourseListItemVm> Courses { get; set; } = new();
    }

    public class ReactiveCourseListItemVm
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? ShortDescription { get; set; }
        public string? CoverPublicUrl { get; set; }
        public string? TeacherName { get; set; }
        public int DurationMonths { get; set; }
        public string? PricePerMonthLabel { get; set; }
        public int Capacity { get; set; }
        public DateTime StartDateUtc { get; set; }
        public DateTime EndDateUtc { get; set; }
        public int MonthsCount { get; set; }
        public string? IntroVideoUrl { get; set; }
    }

    public class ReactiveCourseDetailsVm
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? CoverPublicUrl { get; set; }
        public string? IntroVideoUrl { get; set; }        // original URL/key
        public string? IntroYouTubeId { get; set; }       // parsed YouTube id (nullable)
        public string? TeacherName { get; set; }
        public string? PricePerMonthLabel { get; set; }
        public int DurationMonths { get; set; }
        public int Capacity { get; set; }
        public DateTime StartDateUtc { get; set; }
        public DateTime EndDateUtc { get; set; }

        public List<ReactiveCourseMonthVm> Months { get; set; } = new();

        // current user state
        public bool IsEnrolled { get; set; } = false;
        public bool IsPaidEnrollment { get; set; } = false;
        public bool HasPendingEnrollment { get; set; } = false;
    }

    public class ReactiveCourseMonthVm
    {
        public int Id { get; set; }
        public int MonthIndex { get; set; }
        public DateTime MonthStartUtc { get; set; }
        public DateTime MonthEndUtc { get; set; }
        public bool IsReadyForPayment { get; set; }
        public int LessonsCount { get; set; }
        public int MonthPaymentsCount { get; set; }
    }

    // Enrollment request vm (posted to student area)
    public class ReactiveEnrollmentRequestVm
    {
        [Required]
        public int ReactiveCourseId { get; set; }

        public string? Note { get; set; }
    }
}

