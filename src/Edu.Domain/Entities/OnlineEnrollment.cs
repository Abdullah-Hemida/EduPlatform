using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Edu.Domain.Entities
{
    public class OnlineEnrollment
    {
        public int Id { get; set; }
        public int OnlineCourseId { get; set; }
        public string? StudentId { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public bool IsApproved { get; set; } = false;
        public OnlineCourse? OnlineCourse { get; set; }
        public Student? Student { get; set; }
        public ICollection<OnlineEnrollmentMonthPayment> MonthPayments { get; set; } = new List<OnlineEnrollmentMonthPayment>();
    }

    public class OnlineEnrollmentConfiguration : IEntityTypeConfiguration<OnlineEnrollment>
    {
        public void Configure(EntityTypeBuilder<OnlineEnrollment> b)
        {
            b.HasKey(x => x.Id);

            b.HasIndex(x => new { x.OnlineCourseId, x.StudentId })
             .IsUnique();

            // ❗ Do NOT cascade — admin must control deletions
            b.HasOne(e => e.OnlineCourse)
             .WithMany(c => c.Enrollments)
             .HasForeignKey(e => e.OnlineCourseId)
             .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

