
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Edu.Domain.Entities
{
    public class ReactiveCourseLesson
    {
        public int Id { get; set; }
        public int ReactiveCourseMonthId { get; set; }
        public string? Title { get; set; }
        public DateTime? ScheduledUtc { get; set; }   // if teacher provides schedule
        public string? MeetUrl { get; set; }          // required to be set by teacher before ready
        public string? Notes { get; set; }
        public ReactiveCourseMonth? ReactiveCourseMonth { get; set; }
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
