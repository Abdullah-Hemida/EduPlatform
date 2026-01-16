using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Edu.Domain.Entities
{
    public class PrivateCourse
    {
        public int Id { get; set; }
        public string? TeacherId { get; set; }
        public int CategoryId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? CoverImageUrl { get; set; }
        public string? CoverImageKey { get; set; }
        public decimal Price { get; set; } 
        public bool IsPublished { get; set; }
        public bool IsPublishRequested { get; set; }
        public bool IsForChildren { get; set; }
        public Teacher? Teacher { get; set; }
        public Category? Category { get; set; }
        public ICollection<PrivateModule> PrivateModules { get; set; } = new List<PrivateModule>();
        public ICollection<PrivateLesson>? PrivateLessons { get; set; }
        public ICollection<PurchaseRequest> PurchaseRequests { get; set; } = new List<PurchaseRequest>();
    }

    public class PrivateCourseConfiguration : IEntityTypeConfiguration<PrivateCourse>
    {
        public void Configure(EntityTypeBuilder<PrivateCourse> builder)
        {
            builder.HasKey(c => c.Id);
            builder.HasOne(c => c.Teacher)
                   .WithMany(t => t.PrivateCourses)
                   .HasForeignKey(c => c.TeacherId);

            builder.HasOne(c => c.Category)
                   .WithMany(cat => cat.PrivateCourses)
                   .HasForeignKey(c => c.CategoryId);
            builder.Property(x => x.Price).HasColumnType("decimal(18,2)");
        }
    }
}
