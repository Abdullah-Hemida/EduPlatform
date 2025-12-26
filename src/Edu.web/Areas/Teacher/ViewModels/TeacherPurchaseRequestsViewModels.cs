
using Edu.Domain.Entities;

namespace Edu.Web.Areas.Teacher.ViewModels
{
    public class PurchaseRequestListVm
    {
        public string? Query { get; set; }
        public PurchaseStatus? FilterStatus { get; set; }
        public List<PurchaseRequestItemVm> Requests { get; set; } = new List<PurchaseRequestItemVm>();

        // Paging
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; } = 0;
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 1;
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

