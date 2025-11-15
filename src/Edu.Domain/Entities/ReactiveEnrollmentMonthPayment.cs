using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Edu.Domain.Entities
{
    public enum EnrollmentMonthPaymentStatus { Pending, Paid, Rejected, Cancelled }

    public class ReactiveEnrollmentMonthPayment
    {
        public int Id { get; set; }
        public int ReactiveEnrollmentId { get; set; }
        public int ReactiveCourseMonthId { get; set; }
        public decimal Amount { get; set; }
        public EnrollmentMonthPaymentStatus Status { get; set; } = EnrollmentMonthPaymentStatus.Pending;
        public string? AdminNote { get; set; }
        public string? PaymentReference { get; set; } // optional txn id / note
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAtUtc { get; set; }
        public ReactiveEnrollment? ReactiveEnrollment { get; set; }
        public ReactiveCourseMonth? ReactiveCourseMonth { get; set; }
    }
    public class ReactiveEnrollmentMonthPaymentConfiguration : IEntityTypeConfiguration<ReactiveEnrollmentMonthPayment>
    {
        public void Configure(EntityTypeBuilder<ReactiveEnrollmentMonthPayment> b)
        {
            b.HasKey(x => x.Id);

            // amount precision once
            b.Property(x => x.Amount).HasColumnType("decimal(18,2)");

            // index
            b.HasIndex(x => new { x.ReactiveEnrollmentId, x.ReactiveCourseMonthId });

            // FK to ReactiveCourseMonth - allow cascade deletion when a month is removed
            b.HasOne(x => x.ReactiveCourseMonth)
             .WithMany(m => m.MonthPayments) // ensure ReactiveCourseMonth has collection property MonthPayments
             .HasForeignKey(x => x.ReactiveCourseMonthId)
             .OnDelete(DeleteBehavior.Cascade);

            // FK to ReactiveEnrollment - DO NOT cascade delete to avoid multiple cascade path error
            b.HasOne(x => x.ReactiveEnrollment)
             .WithMany(e => e.MonthPayments) // ensure ReactiveEnrollment has collection property MonthPayments
             .HasForeignKey(x => x.ReactiveEnrollmentId)
             .OnDelete(DeleteBehavior.Restrict); // or DeleteBehavior.NoAction if you prefer
        }
    }
}
