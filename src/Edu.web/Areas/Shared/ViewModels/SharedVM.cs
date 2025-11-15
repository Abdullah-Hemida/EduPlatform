using Edu.Domain.Entities;

namespace Edu.Web.Areas.Shared.ViewModels
{
    public class SlotListItemVm
    {
        public int Id { get; set; }
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public int Capacity { get; set; }
        public int AvailableSeats { get; set; } // calculated
        public decimal? Price { get; set; }
        public string? PriceLabel { get; set; }
        public string? LocationUrl { get; set; }
        public string? TeacherId { get; set; }
    }

    public class CreateBookingVm
    {
        public int SlotId { get; set; }
        public string? Notes { get; set; }
    }

    public class BookingListItemVm
    {
        public int Id { get; set; }
        public int? SlotId { get; set; }

        // slot date/time in UTC (useful for join / cancel logic)
        public DateTime? SlotStartUtc { get; set; }
        public DateTime? SlotEndUtc { get; set; }

        public string? SlotTimes { get; set; }
        public string? CourseOrSlotTitle { get; set; }
        public string? StudentId { get; set; }
        public string? StudentName { get; set; }
        public string? StudentPhoneNumber { get; set; }
        public string? TeacherId { get; set; }
        public string? TeacherName { get; set; }
        public BookingStatus Status { get; set; }
        public DateTime RequestedDateUtc { get; set; }
        public decimal? Price { get; set; }
        public string? PriceLabel { get; set; }
        public string? MeetUrl { get; set; }

        // UI helpers (computed server-side)
        public bool CanJoin { get; set; }
        public bool CanCancel { get; set; }

        // convenience: human-friendly Start local time (computed in controller)
        public string? SlotStartLocalString { get; set; }
    }


    public class BookingDetailsVm
    {
        public int Id { get; set; }
        public int? SlotId { get; set; }
        public string? SlotTimes { get; set; }
        public string? StudentId { get; set; }
        public string? StudentName { get; set; }
        public string? StudentPhoneNumber { get; set; }
        public string? StudentEmail { get; set; }
        public string? TeacherId { get; set; }
        public string? TeacherName { get; set; }
        public BookingStatus Status { get; set; }
        public DateTime RequestedDateUtc { get; set; }
        public decimal? Price { get; set; }
        public string? PriceLabel { get; set; }
        public string? MeetUrl { get; set; }
        public string? Notes { get; set; }
        public List<BookingModerationLog>? ModerationLogs { get; set; } = new();
    }

    public class UpdateMeetUrlVm
    {
        public int BookingId { get; set; }
        public string? MeetUrl { get; set; }
    }

    public class BookingModerationInputVm
    {
        public int BookingId { get; set; }
        public string Action { get; set; } = ""; // "MarkPaid","MeetUrlUpdated"
        public string? Note { get; set; }
    }
}
