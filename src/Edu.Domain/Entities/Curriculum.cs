using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Edu.Domain.Entities
{
    public class Curriculum
    {
        public int Id { get; set; }
        public int LevelId { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public string? CoverImageUrl { get; set; }
        public string? CoverImageKey { get; set; }
        public int Order { get; set; }

        public Level? Level { get; set; }

        // Modules inside this curriculum
        public ICollection<SchoolModule> SchoolModules { get; set; } = new List<SchoolModule>();

        // Lessons directly under Curriculum (no module)
        public ICollection<SchoolLesson> SchoolLessons { get; set; } = new List<SchoolLesson>();
        public ICollection<StudentCurriculum> StudentCurricula { get; set; } = new List<StudentCurriculum>();

    }

    public class CurriculumConfiguration : IEntityTypeConfiguration<Curriculum>
    {
        public void Configure(EntityTypeBuilder<Curriculum> builder)
        {
            builder.HasKey(c => c.Id);

            builder.HasOne(c => c.Level)
                   .WithMany(l => l.Curricula)
                   .HasForeignKey(c => c.LevelId);

            builder.Property(c => c.Title)
                   .IsRequired()
                   .HasMaxLength(200);
        }
    }
}

