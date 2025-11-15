using Edu.Domain.Entities;
using Edu.Web.Resources;
using System.ComponentModel.DataAnnotations;

namespace Edu.Web.Areas.Admin.ViewModels
{
    public class PendingPaymentsVm
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public List<PendingPaymentItemVm> Payments { get; set; } = new();
    }

    public class PendingPaymentItemVm
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public string? CourseTitle { get; set; }
        public string? CourseCoverPublicUrl { get; set; }
        public int MonthIndex { get; set; }
        public string? StudentId { get; set; }
        public string? StudentDisplayName { get; set; }
        public decimal Amount { get; set; }
        public string AmountLabel { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class PaymentDetailsVm
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public string? CourseTitle { get; set; }
        public string? CourseCoverPublicUrl { get; set; }
        public int MonthIndex { get; set; }
        public string? StudentId { get; set; }
        public string? StudentDisplayName { get; set; }
        public decimal Amount { get; set; }
        public string? AmountLabel { get; set; }
        public EnrollmentMonthPaymentStatus Status { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string? Notes { get; set; }
    }

    public class MarkPaidInputModel
    {
        public int PaymentId { get; set; }
        public decimal Amount { get; set; }
        public string? PaymentReference { get; set; }
    }

    public class RejectPaymentInputModel
    {
        public int PaymentId { get; set; }
        public string Reason { get; set; } = "";
    }
}