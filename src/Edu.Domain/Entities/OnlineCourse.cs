
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Edu.Domain.Entities
{
    public class OnlineCourse
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? CoverImageKey { get; set; }
        public string? IntroductionVideoUrl { get; set; }
        public decimal PricePerMonth { get; set; }
        public int DurationMonths { get; set; } = 1;
        public bool IsPublished { get; set; } = false;
        public int LevelId { get; set; }
        public string? TeacherName { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public ICollection<OnlineCourseMonth> Months { get; set; } = new List<OnlineCourseMonth>();
        public ICollection<OnlineCourseLesson> Lessons { get; set; } = new List<OnlineCourseLesson>();
        public ICollection<OnlineEnrollment> Enrollments { get; set; } = new List<OnlineEnrollment>();
    }

    public class OnlineCourseConfiguration : IEntityTypeConfiguration<OnlineCourse>
    {
        public void Configure(EntityTypeBuilder<OnlineCourse> b)
        {
            b.HasKey(x => x.Id);

            b.HasIndex(x => x.LevelId);

            b.Property(x => x.PricePerMonth)
             .HasColumnType("decimal(18,2)");

            b.Property(x => x.DurationMonths)
             .HasDefaultValue(1);

            b.Property(x => x.IntroductionVideoUrl)
             .HasMaxLength(1000);

            b.Property(x => x.TeacherName)
             .HasMaxLength(200);

            // ✅ Course → Months (cascade)
            b.HasMany(c => c.Months)
             .WithOne(m => m.OnlineCourse)
             .HasForeignKey(m => m.OnlineCourseId)
             .OnDelete(DeleteBehavior.Cascade);

            // ✅ Course → Lessons (NO cascade → prevents multiple cascade paths)
            b.HasMany(c => c.Lessons)
             .WithOne(l => l.OnlineCourse)
             .HasForeignKey(l => l.OnlineCourseId)
             .OnDelete(DeleteBehavior.Restrict);

            // ✅ Course → Enrollments (NO cascade – safer)
            b.HasMany(c => c.Enrollments)
             .WithOne(e => e.OnlineCourse)
             .HasForeignKey(e => e.OnlineCourseId)
             .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

