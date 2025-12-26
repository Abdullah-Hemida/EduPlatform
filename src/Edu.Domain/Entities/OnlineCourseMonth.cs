using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Edu.Domain.Entities
{
    public class OnlineCourseMonth
    {
        public int Id { get; set; }
        public int OnlineCourseId { get; set; }
        public int MonthIndex { get; set; } // 1..DurationMonths
        public DateTime? MonthStartUtc { get; set; }
        public DateTime? MonthEndUtc { get; set; }
        public bool IsReadyForPayment { get; set; } = false;
        public OnlineCourse? OnlineCourse { get; set; }
        public ICollection<OnlineCourseLesson> Lessons { get; set; } = new List<OnlineCourseLesson>();
        public ICollection<OnlineEnrollmentMonthPayment> MonthPayments { get; set; } = new List<OnlineEnrollmentMonthPayment>();
    }

    public class OnlineCourseMonthConfiguration : IEntityTypeConfiguration<OnlineCourseMonth>
    {
        public void Configure(EntityTypeBuilder<OnlineCourseMonth> b)
        {
            b.HasKey(x => x.Id);

            b.HasIndex(x => new { x.OnlineCourseId, x.MonthIndex })
             .IsUnique();

            b.HasOne(m => m.OnlineCourse)
             .WithMany(c => c.Months)
             .HasForeignKey(m => m.OnlineCourseId)
             .OnDelete(DeleteBehavior.Cascade);
        }
    }
}

