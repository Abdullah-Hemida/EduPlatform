
using Edu.Infrastructure.Helpers;
using System.Globalization;

namespace Edu.Web.Areas.Teacher.ViewModels
{
    // Main dashboard viewmodel
    public class TeacherDashboardVm
    {
        // summary cards
        public int TotalCourses { get; set; }
        public int PublishedCourses { get; set; }
        public int UnpublishedCourses => TotalCourses - PublishedCourses;
        public int UpcomingBookingsCount { get; set; }
        public int PendingPurchaseRequests { get; set; }

        // lists
        public List<CourseSummaryVm> Courses { get; set; } = new();
        public List<BookingSummaryVm> UpcomingBookings { get; set; } = new();
        public List<PurchaseRequestSummaryVm> RecentPurchaseRequests { get; set; } = new();

        // reactive courses + earnings
        public List<ReactiveCourseSummaryVm> ReactiveCourses { get; set; } = new();
        public decimal TotalEarnings { get; set; } = 0m;
        public string? TotalEarningsLabel { get; set; }
        public List<MonthEarningVm> MonthlyEarnings { get; set; } = new();

        // convenience: prepare display labels
        public void PrepareLabels()
        {
            TotalEarningsLabel = TotalEarnings.ToEuro();
            foreach (var m in MonthlyEarnings)
            {
                m.AmountLabel = m.Amount.ToEuro();
                m.Label = CultureInfo.CurrentUICulture.DateTimeFormat.GetAbbreviatedMonthName(m.Month) + " " + m.Year;
            }
        }
    }

    // Existing small VMs
    public class CourseSummaryVm
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? CoverImageUrl { get; set; }
        public bool IsPublished { get; set; }
        public decimal? Price { get; set; }
        public string? PriceLabel { get; set; }
        public int LessonCount { get; set; }
        public string? CategoryName { get; set; }
    }

    public class BookingSummaryVm
    {
        public int Id { get; set; }
        public DateTime RequestedDateUtc { get; set; }
        public string? StudentName { get; set; }
        public string? StudentEmail { get; set; }
        public string? Status { get; set; }
        public string? MeetUrl { get; set; }
        public decimal Price { get; set; }
        public string? PriceLabel { get; set; }
    }

    public class PurchaseRequestSummaryVm
    {
        public int Id { get; set; }
        public int PrivateCourseId { get; set; }
        public string? CourseTitle { get; set; }
        public string? StudentName { get; set; }
        public DateTime RequestDateUtc { get; set; }
        public string Status { get; set; } = "Pending";
        public decimal? Amount { get; set; }
        public string? AmountLabel { get; set; }
    }

    // Reactive course summary
    public class ReactiveCourseSummaryVm
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? CoverPublicUrl { get; set; }
        public int DurationMonths { get; set; }
        public decimal PricePerMonth { get; set; }
        public string? PricePerMonthLabel { get; set; }
        public int EnrollmentsCount { get; set; }
        public int MonthsReadyCount { get; set; }
    }

    // Monthly earning
    public class MonthEarningVm
    {
        public int Year { get; set; }
        public int Month { get; set; } // 1-12
        public decimal Amount { get; set; }
        public string? AmountLabel { get; set; }
        public string? Label { get; set; } // e.g. "Oct 2025"
    }

    // Formatting helper extension - if ToEuro exists in helpers this isn't necessary.
    // public static class MoneyExtensions
    // {
    //     public static string ToEuro(this decimal value) => value.ToString("C", CultureInfo.GetCultureInfo("it-IT"));
    // }
}


