
namespace Edu.Web.Areas.Admin.ViewModels
{
    public class AdminDashboardVm
    {
        // summary cards
        public int TotalUsers { get; set; }
        public int TotalTeachers { get; set; }
        public int TotalStudents { get; set; }
        public int PendingTeacherApplications { get; set; }

        public int TotalPrivateCourses { get; set; }
        public int TotalCurricula { get; set; }

        public int TotalReactiveCourses { get; set; }

        public int PendingPurchaseRequests { get; set; }
        public int PendingReactiveEnrollments { get; set; }
        public int PendingBookings { get; set; }

        // recent lists
        public List<BookingSummaryVm> RecentBookings { get; set; } = new();
        public List<PurchaseRequestSummaryVm> RecentPurchaseRequests { get; set; } = new();
        public List<ReactiveEnrollmentSummaryVm> RecentReactiveEnrollments { get; set; } = new();
        public List<TeacherSummaryVm> RecentTeacherApplications { get; set; } = new();
    }

    public class BookingSummaryVm
    {
        public int Id { get; set; }
        public DateTime RequestedDateUtc { get; set; }
        public string? StudentName { get; set; }
        public string? TeacherName { get; set; }
        public string? Status { get; set; }
        public string? MeetUrl { get; set; }
    }

    public class PurchaseRequestSummaryVm
    {
        public int Id { get; set; }
        public int PrivateCourseId { get; set; }
        public string? CourseTitle { get; set; }
        public string? StudentName { get; set; }
        public string? TeacherName { get; set; }
        public DateTime RequestDateUtc { get; set; }
        public string? Status { get; set; }
        public decimal? Amount { get; set; }
    }

    public class ReactiveEnrollmentSummaryVm
    {
        public int Id { get; set; }
        public int ReactiveCourseId { get; set; }
        public string? CourseTitle { get; set; }
        public string? StudentName { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public bool IsApproved { get; set; }
        public bool IsPaid { get; set; }
    }

    public class TeacherSummaryVm
    {
        public string Id { get; set; } = null!;
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Status { get; set; }
    }
}

