
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edu.Domain.Entities
{
    public class ReactiveEnrollment
    {
        public int Id { get; set; }
        public int ReactiveCourseId { get; set; }
        public string? StudentId { get; set; }   // ApplicationUser.Id
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public bool IsApproved { get; set; } = false; // Admin approves enrollment (optional)
        public ReactiveCourse? ReactiveCourse { get; set; }
        public Student? Student { get; set; }   // your Student entity
        public ICollection<ReactiveEnrollmentMonthPayment> MonthPayments { get; set; } = new List<ReactiveEnrollmentMonthPayment>();

        // Computed helper property (not mapped to DB)
        [NotMapped]
        public bool IsPaid =>
            MonthPayments != null &&
            MonthPayments.Any() && // consider enrollment "paid" only if there are month payments and all of them are Paid
            MonthPayments.All(m => m.Status == EnrollmentMonthPaymentStatus.Paid);
    }

    public class ReactiveEnrollmentConfiguration : IEntityTypeConfiguration<ReactiveEnrollment>
    {
        public void Configure(EntityTypeBuilder<ReactiveEnrollment> b)
        {
            b.HasKey(x => x.Id);

            b.HasIndex(x => new { x.ReactiveCourseId, x.StudentId }).IsUnique();

            b.HasOne(x => x.ReactiveCourse)
             .WithMany(c => c.Enrollments)
             .HasForeignKey(x => x.ReactiveCourseId)
             .OnDelete(DeleteBehavior.Cascade);

            // Optional: configure navigation collection
            b.Navigation(x => x.MonthPayments).AutoInclude(false);
        }
    }
}
