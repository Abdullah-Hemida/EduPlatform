using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Edu.Domain.Entities;
public enum BookingStatus { Pending, Paid }

public class Booking
{
    public int Id { get; set; }
    public string? StudentId { get; set; }
    public string? TeacherId { get; set; }
    public int? SlotId { get; set; }         
    public DateTime RequestedDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAtUtc { get; set; }
    public string? MeetUrl { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public string? Notes { get; set; }
    public decimal Price { get; set; }
    public Student? Student { get; set; }
    public Teacher? Teacher { get; set; }
    public Slot? Slot { get; set; }
}

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Student).WithMany(s => s.Bookings).HasForeignKey(x => x.StudentId);
        b.HasOne(x => x.Teacher).WithMany(t => t.Bookings).HasForeignKey(x => x.TeacherId);
        b.HasOne(x => x.Slot).WithMany(s => s.Bookings).HasForeignKey(x => x.SlotId).OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.Price).HasColumnType("decimal(18,2)");
    }
}

