

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Edu.Domain.Entities
{
    public class ReactiveCourse
    {
        public int Id { get; set; }
        public string? TeacherId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? CoverImageKey { get; set; }   // storage key (IFileStorageService)
        public string? IntroVideoUrl { get; set; }   // optional teacher-provided video or YouTube URL
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int DurationMonths { get; set; } 
        public decimal PricePerMonth { get; set; }
        public int Capacity { get; set; } = 0;
        public Teacher Teacher { get; set; } = null!;
        public ICollection<ReactiveCourseMonth> Months { get; set; } = new List<ReactiveCourseMonth>();
        public ICollection<ReactiveEnrollment> Enrollments { get; set; } = new List<ReactiveEnrollment>();
        // New soft-archive fields
        public bool IsArchived { get; set; } = false;
        public DateTime? ArchivedAtUtc { get; set; } = null;
    }
    public class ReactiveCourseConfiguration : IEntityTypeConfiguration<ReactiveCourse>
    {
        public void Configure(EntityTypeBuilder<ReactiveCourse> b)
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.TeacherId);
            b.Property(x => x.PricePerMonth).HasColumnType("decimal(18,2)");
            b.Property(x => x.DurationMonths).HasDefaultValue(1);
        }
    }
}
