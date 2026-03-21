using Edu.Domain.Entities;
using System;
using System.Collections.Generic;

namespace Edu.Web.Areas.Admin.ViewModels
{
    public class AdminEnrollmentIndexVm
    {
        public List<AdminEnrollmentListItemVm> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public string? Query { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    }

    public class AdminEnrollmentListItemVm
    {
        public int EnrollmentId { get; set; }
        public int CourseId { get; set; }
        public string CourseTitle { get; set; } = "";
        public string? StudentId { get; set; }
        public string? StudentFullName { get; set; }
        public string? StudentEmail { get; set; }
        public string? StudentPhone { get; set; }
        public string? PhoneWhatsapp { get; set; }
        public string? GuardianPhoneNumber { get; set; }
        public string? PhotoStorageKey { get; set; }
        public string? StudentPhotoUrl { get; set; }
        public string? PhotoFileUrlFallback { get; set; }

        public string? TeacherFullName { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public int PaidMonthsCount { get; set; }
        public int PendingMonthsCount { get; set; }
    }
    public class AdminEnrollmentDetailsVm
    {
        public int EnrollmentId { get; set; }
        public int CourseId { get; set; }
        public string CourseTitle { get; set; } = "";
        public string? StudentId { get; set; }
        public string? StudentName { get; set; }
        public string? StudentEmail { get; set; }
        public string? StudentPhone { get; set; }
        public string? GuardianPhoneNumber { get; set; }
        public string? PhoneWhatsapp { get; set; }
        public string? GuardianWhatsapp { get; set; }
        public string? PhotoStorageKey { get; set; }
        public string? PhotoFileUrlFallback { get; set; }
        public string? StudentPhotoUrl { get; set; }
        public string? TeacherFullName { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string? CourseCoverUrl { get; set; }
        public List<AdminEnrollmentMonthVm> Months { get; set; } = new();
    }
    public class AdminEnrollmentMonthVm
    {
        public int MonthId { get; set; }
        public int MonthIndex { get; set; }
        public int LessonsCount { get; set; }
        public List<AdminEnrollmentPaymentVm> Payments { get; set; } = new();
    }
    public class AdminEnrollmentPaymentVm
    {
        public int PaymentId { get; set; }
        public int MonthId { get; set; }
        public decimal Amount { get; set; }
        public string? AmountLabel { get; set; }
        public EnrollmentMonthPaymentStatus Status { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? PaidAtUtc { get; set; }
        public string? AdminNote { get; set; }
        public string? StudentId { get; set; }
    }
}

