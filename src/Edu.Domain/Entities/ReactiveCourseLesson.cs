
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Edu.Domain.Entities
{
    public class ReactiveCourseLesson
    {
        public int Id { get; set; }
        public int ReactiveCourseMonthId { get; set; }
        public string? Title { get; set; }
        public DateTime? ScheduledUtc { get; set; }
        public string? MeetUrl { get; set; }
        public string? RecordedVideoUrl { get; set; }
        public string? Notes { get; set; }
        public ReactiveCourseMonth? ReactiveCourseMonth { get; set; }
        public ICollection<FileResource> Files { get; set; } = new List<FileResource>();
    }
    public class ReactiveCourseLessonConfiguration : IEntityTypeConfiguration<ReactiveCourseLesson>
    {
        public void Configure(EntityTypeBuilder<ReactiveCourseLesson> b)
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.ReactiveCourseMonthId);
            b.Property(x => x.Title).HasMaxLength(500);
        }
    }
}
