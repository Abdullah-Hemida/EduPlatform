// StudentReactiveCourseDetailsVm.cs
using Edu.Domain.Entities;
namespace Edu.Web.Areas.Student.ViewModels
{
    public class StudentReactiveCourseDetailsVm
    {
        public int CourseId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? CoverPublicUrl { get; set; }
        public string? IntroVideoUrl { get; set; }
        public string? IntroYouTubeId { get; set; }
        public string? TeacherName { get; set; }

        public DateTime StartDateUtc { get; set; }
        public DateTime EndDateUtc { get; set; }
        public int DurationMonths { get; set; }
        public decimal PricePerMonth { get; set; }
        public string? PricePerMonthLabel { get; set; }
        public int Capacity { get; set; }
        public int CurrentEnrolledCount { get; set; }

        // Enrollment high-level flags
        public bool IsEnrolled { get; set; }
        public bool HasPendingEnrollment { get; set; }   // any pending month payment exists
        public bool HasAnyPaidMonth { get; set; }
        public int? EnrollmentId { get; set; }

        // UI helper: can student cancel their enrollment?
        // They may cancel only if enrolled AND no pending month payments AND no paid months (business rule)
        public bool CanCancelEnrollment => IsEnrolled && !HasPendingEnrollment && !HasAnyPaidMonth;

        public List<StudentCourseMonthVm> Months { get; set; } = new();
    }

    public class StudentCourseMonthVm
    {
        public int Id { get; set; }
        public int MonthIndex { get; set; }
        public DateTime MonthStartUtc { get; set; }
        public DateTime MonthEndUtc { get; set; }
        public bool IsReadyForPayment { get; set; }
        public int LessonsCount { get; set; }

        // Per-student status
        public EnrollmentMonthPaymentStatus? MyPaymentStatus { get; set; }
        public int? PaymentId { get; set; }               // DB id of the payment row (if exists)
        public bool CanRequestPayment { get; set; }       // computed
        public bool CanCancelPayment { get; set; }        // only for pending
        public bool CanViewLessons { get; set; }          // only if paid (or free preview)
        public bool HasPaidPayment { get; set; } = false;
        public List<StudentCourseLessonVm> Lessons { get; set; } = new();

        // optional helper for client
        public bool HasPayment => PaymentId.HasValue;
    }

    public class StudentCourseLessonVm
    {
        public int Id { get; set; }
        public int ReactiveCourseMonthId { get; set; }
        public string? Title { get; set; }
        public DateTime? ScheduledUtc { get; set; }
        public string? MeetUrl { get; set; }
        public string? RecordedVideoUrl { get; set; }
        public string? Notes { get; set; }
        public List<ReactiveCourseLessonFileVm> Files { get; set; } = new();
    }
    public class ReactiveCourseLessonFileVm
    {
        public int Id { get; set; }
        public string FileName { get; set; } = "";
        public string? PublicUrl { get; set; }
    }
    public class MyEnrollmentVm
    {
        public int EnrollmentId { get; set; }
        public int CourseId { get; set; }
        public string? CourseTitle { get; set; }
        public string? CoverPublicUrl { get; set; }
        public List<MyEnrollmentMonthVm> Months { get; set; } = new();
    }

    public class MyEnrollmentMonthVm
    {
        public int MonthId { get; set; }
        public int MonthIndex { get; set; }
        public bool IsReadyForPayment { get; set; }
        public EnrollmentMonthPaymentStatus? PaymentStatus { get; set; }
    }
}


//public class StudentReactiveCourseDetailsVm
//{
//    public int Id { get; set; }
//    public string? Title { get; set; }
//    public string? Description { get; set; }
//    public string? CoverPublicUrl { get; set; }
//    public string? IntroVideoUrl { get; set; }
//    public decimal PricePerMonth { get; set; }
//    public string? PricePerMonthLabel { get; set; }
//    public int DurationMonths { get; set; }
//    public int Capacity { get; set; }
//    public List<StudentCourseMonthVm> Months { get; set; } = new();

//    // new flags (precomputed in controller)
//    public bool IsEnrolled { get; set; }
//    public bool HasPendingEnrollment { get; set; }
//    public bool HasAnyPaidMonth { get; set; }
//}

//public class StudentCourseMonthVm
//{
//    public int Id { get; set; }
//    public int MonthIndex { get; set; }
//    public bool IsReadyForPayment { get; set; }
//    public int LessonsCount { get; set; }
//    public EnrollmentMonthPaymentStatus? MyPaymentStatus { get; set; }
//    public List<StudentCourseLessonVm> Lessons { get; set; } = new();

//    // new helper: whether user is allowed to request payment now (computed server-side)
//    public bool CanRequestPayment { get; set; }
//}

//public class StudentCourseLessonVm
//{
//    public int Id { get; set; }
//    public string? Title { get; set; }
//    public DateTime? ScheduledUtc { get; set; }
//    public string? MeetUrl { get; set; }
//    public string? Notes { get; set; }
//}

//public class MyEnrollmentVm
//{
//    public int EnrollmentId { get; set; }
//    public int CourseId { get; set; }
//    public string? CourseTitle { get; set; }
//    public string? CoverPublicUrl { get; set; }
//    public List<MyEnrollmentMonthVm> Months { get; set; } = new();
//}

//public class MyEnrollmentMonthVm
//{
//    public int MonthId { get; set; }
//    public int MonthIndex { get; set; }
//    public bool IsReadyForPayment { get; set; }
//    public EnrollmentMonthPaymentStatus? PaymentStatus { get; set; }
//}

//public class ViewMonthVm
//{
//    public int CourseId { get; set; }
//    public string? CourseTitle { get; set; }
//    public int MonthIndex { get; set; }
//    public List<StudentCourseLessonVm> Lessons { get; set; } = new();
//}