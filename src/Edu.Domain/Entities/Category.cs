using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Edu.Domain.Entities
{
    public class Category
    {
        public int Id { get; set; }
        // multilingual names
        public string NameEn { get; set; } = string.Empty;
        public string NameIt { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public ICollection<PrivateCourse>? PrivateCourses { get; set; }
    }

    public class CategoryConfiguration : IEntityTypeConfiguration<Category>
    {
        public void Configure(EntityTypeBuilder<Category> builder)
        {
            builder.HasKey(c => c.Id);
            // multilingual columns limits
            builder.Property(c => c.NameEn).IsRequired().HasMaxLength(150);
            builder.Property(c => c.NameIt).IsRequired().HasMaxLength(150);
            builder.Property(c => c.NameAr).IsRequired().HasMaxLength(150);
        }
    }
}
