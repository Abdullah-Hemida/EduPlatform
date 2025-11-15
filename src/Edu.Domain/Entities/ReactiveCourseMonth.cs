using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Edu.Domain.Entities
{
    public class ReactiveCourseMonth
    {
        public int Id { get; set; }
        public int ReactiveCourseId { get; set; }
        public int MonthIndex { get; set; } // 1..DurationMonths
        public DateTime MonthStartUtc { get; set; } // optional
        public DateTime MonthEndUtc { get; set; }   // optional
        public bool IsReadyForPayment { get; set; } = false; // teacher toggles when all lessons added
        public ICollection<ReactiveCourseLesson> Lessons { get; set; } = new List<ReactiveCourseLesson>();
        public ICollection <ReactiveEnrollmentMonthPayment> MonthPayments { get; set; } = new List<ReactiveEnrollmentMonthPayment>();
        public ReactiveCourse? ReactiveCourse { get; set; }
    }
    public class ReactiveCourseMonthConfiguration : IEntityTypeConfiguration<ReactiveCourseMonth>
    {
        public void Configure(EntityTypeBuilder<ReactiveCourseMonth> b)
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.ReactiveCourseId, x.MonthIndex }).IsUnique();
            b.HasOne(x => x.ReactiveCourse).WithMany(c => c.Months).HasForeignKey(x => x.ReactiveCourseId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
