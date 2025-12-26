using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Edu.Domain.Entities
{
    public enum OnlineEnrollmentMonthPaymentStatus { Pending, Paid, Rejected, Cancelled }

    public class OnlineEnrollmentMonthPayment
    {
        public int Id { get; set; }
        public int OnlineEnrollmentId { get; set; }
        public int OnlineCourseMonthId { get; set; }
        public decimal Amount { get; set; }
        public OnlineEnrollmentMonthPaymentStatus Status { get; set; } = OnlineEnrollmentMonthPaymentStatus.Pending;
        public string? AdminNote { get; set; }
        public string? PaymentReference { get; set; } // optional txn id / note
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAtUtc { get; set; }

        public OnlineEnrollment? OnlineEnrollment { get; set; }
        public OnlineCourseMonth? OnlineCourseMonth { get; set; }
    }

    public class OnlineEnrollmentMonthPaymentConfiguration : IEntityTypeConfiguration<OnlineEnrollmentMonthPayment>
    {
        public void Configure(EntityTypeBuilder<OnlineEnrollmentMonthPayment> b)
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            b.HasIndex(x => new { x.OnlineEnrollmentId, x.OnlineCourseMonthId });
            b.HasOne(x => x.OnlineCourseMonth).WithMany(m => m.MonthPayments).HasForeignKey(x => x.OnlineCourseMonthId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.OnlineEnrollment).WithMany(e => e.MonthPayments).HasForeignKey(x => x.OnlineEnrollmentId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
