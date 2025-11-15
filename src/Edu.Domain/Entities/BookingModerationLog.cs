
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Edu.Domain.Entities
{
    // BookingModerationLog (shared log)
    public class BookingModerationLog
    {
        public int Id { get; set; }
        public int? BookingId { get; set; }                // for one-off session bookings
        public int? ReactiveEnrollmentId { get; set; }     // for enrollments
        public string? ActorId { get; set; }               // admin/teacher id
        public string? ActorName { get; set; }             // store display name
        public string? Action { get; set; }                // e.g. "Accepted","Rejected","MarkedPaid","MeetUrlUpdated"
        public string? Note { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
    public class BookingModerationLogConfiguration : IEntityTypeConfiguration<BookingModerationLog>
    {
        public void Configure(EntityTypeBuilder<BookingModerationLog> b)
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.BookingId);
            b.HasIndex(x => x.ReactiveEnrollmentId);
        }
    }
}

