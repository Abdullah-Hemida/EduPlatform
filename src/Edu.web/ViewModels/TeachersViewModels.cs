
namespace Edu.Web.ViewModels
{
    public class TeacherIndexVm
    {
        public string? Query { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public List<TeacherCardVm> Teachers { get; set; } = new();
    }

    public class TeacherCardVm
    {
        public string Id { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? PhotoUrl { get; set; }
        public string JobTitle { get; set; } = string.Empty;
        public string? ShortBio { get; set; }
        public string? IntroVideoUrl { get; set; }
        public int PrivateCoursesCount { get; set; }
        public int ReactiveCoursesCount { get; set; }
    }

    public class TeacherDetailsVm
    {
        public string Id { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? PhotoUrl { get; set; }
        public string JobTitle { get; set; } = string.Empty;
        public string? ShortBio { get; set; }
        public string? CVUrl { get; set; }
        public string? IntroVideoUrl { get; set; }
        public DateTime? DateOfBirth { get; set; }

        public List<SlotVm> Slots { get; set; } = new();
        public List<ReactiveCourseCardVm> ReactiveCourses { get; set; } = new();
        public List<PrivateCourseCardVm> PrivateCourses { get; set; } = new();
    }

    public class SlotVm
    {
        public int Id { get; set; }
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public decimal Price { get; set; }
        public string? PriceLabel { get; set; }
        public string? LocationUrl { get; set; }

        /// <summary>
        /// true if the current (authenticated) user already has a booking for this slot
        /// (Pending or Paid).
        /// </summary>
        public bool IsBooked { get; set; }

        /// <summary>
        /// If current user has a booking this is the Booking.Id for that booking (useful for cancel forms).
        /// </summary>
        public int? CurrentBookingId { get; set; }

        /// <summary>
        /// number of remaining seats (capacity - occupied)
        /// </summary>
        public int AvailableSeats { get; set; }
    }


    public class ReactiveCourseCardVm
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? CoverImageUrl { get; set; }
        public string? IntroVideoUrl { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal PricePerMonth { get; set; }
        public string? PricePerMonthLabel { get; set; }
        public int MonthsCount { get; set; }
    }

    public class PrivateCourseCardVm
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? CoverImageUrl { get; set; }
        public decimal? Price { get; set; }
        public string? PriceLabel { get; set; }
    }
    public class CreateBookingAjaxInput
    {
        public int SlotId { get; set; }
        public string? Notes { get; set; }
    }

}

