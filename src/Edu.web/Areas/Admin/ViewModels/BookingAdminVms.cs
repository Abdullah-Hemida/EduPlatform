using Edu.Domain.Entities;

namespace Edu.Web.Areas.Admin.ViewModels
{
    public class AdminBookingListItemVm
    {
        public int Id { get; set; }
        public string? StudentName { get; set; }
        public string? StudentEmail { get; set; }
        public string? StudentPhoneNumber { get; set; }
        public string? TeacherName { get; set; }
        public DateTime RequestedDateUtc { get; set; }
        public BookingStatus Status { get; set; }
        public int? SlotId { get; set; }
        public DateTime? SlotStartUtc { get; set; }
        public DateTime? SlotEndUtc { get; set; }
        public string? SlotTimes { get; set; }
        public string? Notes { get; set; }
        public decimal? Price { get; set; }
        public string? PriceLabel { get; set; }
    }

    public class AdminBookingsIndexVm
    {
        public List<AdminBookingListItemVm> Bookings { get; set; } = new();
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalCount { get; set; } = 0;
        public string? StatusFilter { get; set; }

        /// <summary>
        /// Tab: "upcoming" | "past" | "all"
        /// </summary>
        public string Tab { get; set; } = "upcoming";

        // convenience
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    }

    public class AdminBookingDetailsVm
    {
        public int Id { get; set; }
        public int? SlotId { get; set; }
        public string? SlotTimes { get; set; }
        public string? StudentName { get; set; }
        public string? StudentEmail { get; set; }
        public string? StudentPhoneNumber { get; set; }
        public string GuardianPhoneNumber { get; set; } = string.Empty;
        public string? TeacherName { get; set; }
        public BookingStatus Status { get; set; }
        public DateTime RequestedDateUtc { get; set; }
        public decimal? Price { get; set; }
        public string? PriceLabel { get; set; }
        public string? MeetUrl { get; set; }
        public string? Notes { get; set; }
        public List<BookingModerationLog> ModerationLogs { get; set; } = new();
    }
    public class MarkPaidAdminInput
    {
        public int Id { get; set; }
        public decimal? Amount { get; set; }
        public string? PaymentRef { get; set; }
    }
}




