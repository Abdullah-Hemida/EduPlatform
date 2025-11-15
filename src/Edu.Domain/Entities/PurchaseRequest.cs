using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Edu.Domain.Entities
{
    public class PurchaseRequest
    {
        public int Id { get; set; }
        public string? StudentId { get; set; }
        public int PrivateCourseId { get; set; }
        public DateTime RequestDateUtc { get; set; }
        public PurchaseStatus Status { get; set; }
        public string? AdminNote { get; set; }
        public decimal Amount { get; set; }

        public Student? Student { get; set; }
        public PrivateCourse? PrivateCourse { get; set; }
    }
    public enum PurchaseStatus { Pending, Completed, Rejected, Cancelled }

    public class PurchaseRequestConfiguration : IEntityTypeConfiguration<PurchaseRequest>
    {
        public void Configure(EntityTypeBuilder<PurchaseRequest> builder)
        {
            builder.HasKey(p => p.Id);
            builder.HasOne(p => p.Student)
                   .WithMany(s => s.PurchaseRequests)
                   .HasForeignKey(p => p.StudentId);

            builder.HasOne(x => x.PrivateCourse).WithMany(pc => pc.PurchaseRequests).HasForeignKey(x => x.PrivateCourseId);
            builder.Property(x => x.Amount).HasColumnType("decimal(18,2)");
        }
    }
}
