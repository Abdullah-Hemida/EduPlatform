using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Edu.Domain.Entities
{
    public class Slot
    {
        public int Id { get; set; }
        public string? TeacherId { get; set; }
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public int Capacity { get; set; } = 1;
        public decimal Price { get; set; }
        public bool IsBooked { get; set; } = false;
        public string? LocationUrl { get; set; }
        public byte[]? RowVersion { get; set; }
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }

    public class SlotConfiguration : IEntityTypeConfiguration<Slot>
    {
        public void Configure(EntityTypeBuilder<Slot> b)
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => x.TeacherId);
            b.HasIndex(x => x.StartUtc);
            b.Property(x => x.Capacity).HasDefaultValue(1);
            b.Property(x => x.Price).HasColumnType("decimal(18,2)");
        }
    }
}
