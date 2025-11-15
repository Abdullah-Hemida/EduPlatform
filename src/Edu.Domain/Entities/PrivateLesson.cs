using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Edu.Domain.Entities
{
    public class PrivateLesson
    {
        public int Id { get; set; }
        public int PrivateCourseId { get; set; }
        public int? PrivateModuleId { get; set; }
        public string Title { get; set; } = null!;
        public string? YouTubeVideoId { get; set; }
        public string? VideoUrl { get; set; }
        public int Order { get; set; }

        public PrivateCourse? PrivateCourse { get; set; }
        public PrivateModule? PrivateModule { get; set; }
        public ICollection<FileResource> Files { get; set; } = new List<FileResource>();
    }


    public class PrivateLessonConfiguration : IEntityTypeConfiguration<PrivateLesson>
    {
        public void Configure(EntityTypeBuilder<PrivateLesson> builder)
        {
            builder.HasKey(l => l.Id);

            builder.Property(l => l.Title).IsRequired().HasMaxLength(300);
            builder.Property(l => l.YouTubeVideoId).HasMaxLength(100);
            builder.Property(l => l.Order).HasDefaultValue(0);

            builder.HasOne(l => l.PrivateCourse)
                   .WithMany(c => c.PrivateLessons)
                   .HasForeignKey(l => l.PrivateCourseId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(l => l.PrivateModule)
                   .WithMany(m => m.PrivateLessons)
                   .HasForeignKey(l => l.PrivateModuleId)
                   .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
