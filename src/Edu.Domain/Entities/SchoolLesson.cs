// src/Edu.Domain/Entities/Lesson.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Edu.Domain.Entities
{
    public class SchoolLesson
    {
        public int Id { get; set; }
        public int CurriculumId { get; set; }            // REQUIRED
        public int? ModuleId { get; set; }               // OPTIONAL
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public string? YouTubeVideoId { get; set; }
        public string? VideoUrl { get; set; }
        public bool IsFree { get; set; }
        public int Order { get; set; }

        public Curriculum? Curriculum { get; set; }
        public SchoolModule? SchoolModule { get; set; }

        public ICollection<FileResource>? Files { get; set; } = new List<FileResource>();
    }

    public class SchoolLessonConfiguration : IEntityTypeConfiguration<SchoolLesson>
    {
        public void Configure(EntityTypeBuilder<SchoolLesson> builder)
        {
            builder.HasKey(l => l.Id);

            builder.Property(l => l.Title)
                   .IsRequired()
                   .HasMaxLength(200);

            // Every lesson belongs to a Curriculum
            builder.HasOne(l => l.Curriculum)
                   .WithMany(c => c.SchoolLessons)
                   .HasForeignKey(l => l.CurriculumId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Module assignment is optional
            builder.HasOne(l => l.SchoolModule)
                   .WithMany(m => m.SchoolLessons)
                   .HasForeignKey(l => l.ModuleId)
                   .OnDelete(DeleteBehavior.SetNull);
        }
    }
}

