using System;
using System.Collections.Generic;
using Edu.Domain.Entities;

namespace Edu.Web.Areas.Admin.ViewModels
{
    // Index page VM
    public class SchoolEnrollmentIndexVm
    {
        public List<SchoolEnrollmentListItemVm> Items { get; set; } = new();
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalCount { get; set; }
        public string? Query { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    }

    public class SchoolEnrollmentListItemVm
    {
        public int EnrollmentId { get; set; }
        public int CourseId { get; set; }
        public string CourseTitle { get; set; } = string.Empty;
        public string? StudentId { get; set; }
        public string? StudentFullName { get; set; }
        public string? StudentEmail { get; set; }
        public string? StudentPhone { get; set; }
        public string? PhoneWhatsapp { get; set; }
        public string? GuardianPhoneNumber { get; set; }
        public string? PhotoStorageKey { get; set; }
        public string? StudentPhotoUrl { get; set; }
        public string? TeacherFullName { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public int PaidMonthsCount { get; set; }
        public int PendingMonthsCount { get; set; }
    }

    // Details page VM
    public class SchoolEnrollmentDetailsVm
    {
        public int EnrollmentId { get; set; }
        public int CourseId { get; set; }
        public string CourseTitle { get; set; } = string.Empty;
        public string? StudentId { get; set; }
        public string? StudentName { get; set; }
        public string? StudentEmail { get; set; }
        public string? StudentPhone { get; set; }
        public string? GuardianPhoneNumber { get; set; }
        public string? PhoneWhatsapp { get; set; }
        public string? GuardianWhatsapp { get; set; }
        public string? PhotoStorageKey { get; set; }
        public string? StudentPhotoUrl { get; set; }
        public string? TeacherFullName { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public List<SchoolEnrollmentMonthVm> Months { get; set; } = new();
    }

    public class SchoolEnrollmentMonthVm
    {
        public int MonthId { get; set; }
        public int MonthIndex { get; set; }
        public int LessonsCount { get; set; }
        public List<SchoolEnrollmentPaymentVm> Payments { get; set; } = new();
    }

    public class SchoolEnrollmentPaymentVm
    {
        public int PaymentId { get; set; }
        public int MonthId { get; set; }
        public decimal Amount { get; set; }
        public string? AmountLabel { get; set; }
        public OnlineEnrollmentMonthPaymentStatus Status { get; set; } = OnlineEnrollmentMonthPaymentStatus.Pending;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? PaidAtUtc { get; set; }
        public string? AdminNote { get; set; }
        public string? StudentId { get; set; }
    }
}
