
using Edu.Domain.Entities;

namespace Edu.Web.Areas.Teacher.ViewModels
{
    public class PurchaseRequestListVm
    {
        public string? Query { get; set; }
        public PurchaseStatus? FilterStatus { get; set; }
        public List<PurchaseRequestItemVm> Requests { get; set; } = new();
    }

    public class PurchaseRequestItemVm
    {
        public int Id { get; set; }
        public int PrivateCourseId { get; set; }
        public string? CourseTitle { get; set; }
        public string? CourseCoverUrl { get; set; }
        public string? StudentName { get; set; }
        public string? StudentEmail { get; set; }
        public DateTime RequestDateUtc { get; set; }
        public PurchaseStatus Status { get; set; } = PurchaseStatus.Pending;
        public decimal Amount { get; set; }
        public string? AmountLabel { get; set; }
    }

    public class PurchaseRequestDetailsVm
    {
        public int Id { get; set; }
        public int PrivateCourseId { get; set; }
        public string? CourseTitle { get; set; }
        public string? CourseCoverUrl { get; set; }
        public string? CategoryName { get; set; }
        public string? StudentId { get; set; }
        public string? StudentName { get; set; }
        public string? StudentEmail { get; set; }
        public DateTime RequestDateUtc { get; set; }
        public PurchaseStatus Status { get; set; } = PurchaseStatus.Pending;
        public string? AdminNote { get; set; }
        public decimal Amount { get; set; }
        public string? AmountLabel { get; set; }
    }
}

