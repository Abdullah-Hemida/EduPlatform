// Edu.Domain/Entities/PrivateModule.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Edu.Domain.Entities
{
    public class PrivateModule
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public int Order { get; set; }

        // FK to PrivateCourse
        public int PrivateCourseId { get; set; }
        public PrivateCourse PrivateCourse { get; set; } = null!;

        // Navigation - lessons in this private module
        public ICollection<PrivateLesson> PrivateLessons { get; set; } = new List<PrivateLesson>();
    }

    public class PrivateModuleConfiguration : IEntityTypeConfiguration<PrivateModule>
    {
        public void Configure(EntityTypeBuilder<PrivateModule> builder)
        {
            builder.HasKey(m => m.Id);

            builder.Property(m => m.Title)
                   .IsRequired()
                   .HasMaxLength(250);

            builder.Property(m => m.Description).HasMaxLength(2000);
            builder.Property(m => m.Order).HasDefaultValue(0);

            builder.HasOne(m => m.PrivateCourse)
                   .WithMany(c => c.PrivateModules)   
                   .HasForeignKey(m => m.PrivateCourseId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(m => m.PrivateLessons)
                   .WithOne(l => l.PrivateModule)
                   .HasForeignKey(l => l.PrivateModuleId)
                   .OnDelete(DeleteBehavior.SetNull);
        }
    }
}


