// File: Areas/Admin/ViewModels/PurchaseRequestsViewModels.cs
using Edu.Domain.Entities;
using System;
using System.Collections.Generic;

namespace Edu.Web.Areas.Admin.ViewModels
{
    public class PurchaseRequestListItemVm
    {
        public int Id { get; set; }
        public string? StudentId { get; set; }
        public string? StudentName { get; set; }
        public string? StudentPhone { get; set; }
        public string? GuardianPhoneNumber { get; set; }
        public string? PhoneWhatsapp { get; set; }
        public string? GuardianWhatsapp { get; set; }
        public int PrivateCourseId { get; set; }
        public string? CourseTitle { get; set; }
        public string? TeacherName { get; set; }

        public DateTime RequestDateUtc { get; set; }

        // Use the domain enum here — easier and safer than strings
        public PurchaseStatus Status { get; set; } = PurchaseStatus.Pending;

        public decimal? Amount { get; set; }
        public string? AmountLabel { get; set; }
        public string? AdminNote { get; set; }
    }

    public class PurchaseRequestsIndexViewModel
    {
        public List<PurchaseRequestListItemVm> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }

        // Keep the filter as int? (null = all). Controller will interpret it as PurchaseStatus when needed.
        public int? Status { get; set; }
        public string? Search { get; set; }

        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }
}


