using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Edu.Domain.Entities
{
    public class SchoolModule
    {
        public int Id { get; set; }
        public int CurriculumId { get; set; }
        public string Title { get; set; } = null!;
        public int Order { get; set; }

        public Curriculum? Curriculum { get; set; }
        public ICollection<SchoolLesson> SchoolLessons { get; set; } = new List<SchoolLesson>();
    }

    public class SchoolModuleConfiguration : IEntityTypeConfiguration<SchoolModule>
    {
        public void Configure(EntityTypeBuilder<SchoolModule> builder)
        {
            builder.HasKey(m => m.Id);

            builder.HasOne(m => m.Curriculum)
                   .WithMany(c => c.SchoolModules)
                   .HasForeignKey(m => m.CurriculumId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Property(m => m.Title)
                   .IsRequired()
                   .HasMaxLength(200);
        }
    }
}

