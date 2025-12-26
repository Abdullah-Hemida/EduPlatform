using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Edu.Domain.Entities
{
    public class StudentCurriculum
    {
        public string StudentId { get; set; } = null!; // FK -> Student.Id (ApplicationUser.Id)
        public int CurriculumId { get; set; }          // FK -> Curriculum.Id

        // navigation
        public Student? Student { get; set; }
        public Curriculum? Curriculum { get; set; }
    }

    public class StudentCurriculumConfiguration : IEntityTypeConfiguration<StudentCurriculum>
    {
        public void Configure(EntityTypeBuilder<StudentCurriculum> builder)
        {
            builder.HasKey(sc => new { sc.StudentId, sc.CurriculumId });

            builder.HasOne(sc => sc.Student)
                   .WithMany(s => s.StudentCurricula)
                   .HasForeignKey(sc => sc.StudentId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(sc => sc.Curriculum)
                   .WithMany(c => c.StudentCurricula)
                   .HasForeignKey(sc => sc.CurriculumId)
                   .OnDelete(DeleteBehavior.Cascade);

            // optional: index for quick lookups
            builder.HasIndex(sc => sc.CurriculumId);
        }
    }
}
