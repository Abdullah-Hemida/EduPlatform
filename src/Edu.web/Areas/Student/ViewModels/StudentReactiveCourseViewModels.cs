using Edu.Domain.Entities;
using Edu.Web.Resources;
using System.ComponentModel.DataAnnotations;

namespace Edu.Web.Areas.Student.ViewModels
{
    public class StudentReactiveCourseListItemVm
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? CoverPublicUrl { get; set; }
        public decimal PricePerMonth { get; set; }
        public string? PricePerMonthLabel { get; set; }
        public int DurationMonths { get; set; }
        public int Capacity { get; set; }
        public int ReadyMonthsCount { get; set; }
    }

    public class StudentReactiveCourseDetailsVm
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
        public List<StudentCourseMonthVm> Months { get; set; } = new();
    }

    public class StudentCourseMonthVm
    {
        public int Id { get; set; }
        public int MonthIndex { get; set; }
        public bool IsReadyForPayment { get; set; }
        public int LessonsCount { get; set; }
        public EnrollmentMonthPaymentStatus? MyPaymentStatus { get; set; }
        public List<StudentCourseLessonVm> Lessons { get; set; } = new();
    }

    public class StudentCourseLessonVm
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public DateTime? ScheduledUtc { get; set; }
        public string? MeetUrl { get; set; }
        public string? Notes { get; set; }
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

    public class ViewMonthVm
    {
        public int CourseId { get; set; }
        public string? CourseTitle { get; set; }
        public int MonthIndex { get; set; }
        public List<StudentCourseLessonVm> Lessons { get; set; } = new();
    }
}

