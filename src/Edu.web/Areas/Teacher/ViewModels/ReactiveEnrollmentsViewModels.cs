
using Edu.Domain.Entities;

namespace Edu.Web.Areas.Teacher.ViewModels
{
    // ViewModel for list projection (lightweight)
    public class TeacherEnrollmentViewModel
    {
        public int Id { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string? StudentPhone { get; set; }
        public string? PhoneWhatsapp { get; set; }
        public string? StudentImageKey { get; set; }  // key stored in DB
        public string? StudentImageUrl { get; set; }  // resolved public URL
        public string CourseTitle { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public int PaidMonths { get; set; }
        public int TotalMonths { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    // Details VM
    public class ReactiveEnrollmentDetailsVm
    {
        public int Id { get; set; }
        public int ReactiveCourseId { get; set; }
        public string? CourseTitle { get; set; }
        public string? CourseCoverUrl { get; set; } // public URL
        public string? StudentId { get; set; }
        public string? StudentName { get; set; }
        public string? StudentEmail { get; set; }
        public string? StudentPhoneNumber { get; set; }
        public string? GuardianPhoneNumber { get; set; }
        public string? PhoneWhatsapp { get; set; }
        public string? GuardianWhatsapp { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public bool IsApproved { get; set; }
        public bool IsPaid { get; set; }
        public List<MonthPaymentVm> MonthPayments { get; set; } = new();
        public decimal TotalPaid { get; set; }
        public string? TotalPaidLabel { get; set; }
    }
    public class MonthPaymentVm
    {
        public int Id { get; set; }
        public int ReactiveCourseMonthId { get; set; }
        public int MonthIndex { get; set; }
        public decimal Amount { get; set; }
        public string? AmountLabel { get; set; }
        public EnrollmentMonthPaymentStatus Status { get; set; }
        public string? AdminNote { get; set; }
        public string? PaymentReference { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? PaidAtUtc { get; set; }
    }
}



