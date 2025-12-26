using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Edu.Domain.Entities
{
    public class OnlineCourseLesson
    {
        public int Id { get; set; }
        public int OnlineCourseId { get; set; }
        public int? OnlineCourseMonthId { get; set; }
        public int Order { get; set; } = 0;
        public string Title { get; set; } = string.Empty;
        public string? MeetUrl { get; set; }
        public string? RecordedVideoUrl { get; set; }
        public string? Notes { get; set; }
        public DateTime? ScheduledUtc { get; set; }
        public OnlineCourse? OnlineCourse { get; set; }
        public OnlineCourseMonth? OnlineCourseMonth { get; set; }
        public ICollection<FileResource> Files { get; set; } = new List<FileResource>();
    }

    public class OnlineCourseLessonConfiguration : IEntityTypeConfiguration<OnlineCourseLesson>
    {
        public void Configure(EntityTypeBuilder<OnlineCourseLesson> b)
        {
            b.HasKey(x => x.Id);

            b.Property(x => x.Title)
             .IsRequired()
             .HasMaxLength(500);

            b.Property(x => x.MeetUrl)
             .HasMaxLength(1000);

            b.Property(x => x.RecordedVideoUrl)
             .HasMaxLength(1000);

            // ✅ Month → Lessons (cascade)
            b.HasOne(l => l.OnlineCourseMonth)
             .WithMany(m => m.Lessons)
             .HasForeignKey(l => l.OnlineCourseMonthId)
             .OnDelete(DeleteBehavior.Cascade);

            // ✅ Course → Lessons (NO cascade – critical)
            b.HasOne(l => l.OnlineCourse)
             .WithMany(c => c.Lessons)
             .HasForeignKey(l => l.OnlineCourseId)
             .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

