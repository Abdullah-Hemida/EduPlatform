using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Edu.Domain.Entities
{
    public class Teacher
    {
        public string Id { get; set; } = null!; // same as ApplicationUser.Id
        public string JobTitle { get; set; } = string.Empty;
        public string? ShortBio { get; set; }
        public string? CVUrl { get; set; }
        public string? CVStorageKey { get; set; }
        public string? IntroVideoUrl { get; set; }
        public TeacherStatus Status { get; set; } = TeacherStatus.Pending;

        // Navigation
        public ApplicationUser? User { get; set; }

        // Optional navigations
        public ICollection<PrivateCourse> PrivateCourses { get; set; } = new List<PrivateCourse>();
        public ICollection<Slot> Slots { get; set; } = new List<Slot>();
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
        public ICollection<ReactiveCourse> ReactiveCourses { get; set; } = new List<ReactiveCourse>();
    }
    public enum TeacherStatus { Pending, Approved, Rejected }

    public class TeacherConfiguration : IEntityTypeConfiguration<Teacher>
    {
        public void Configure(EntityTypeBuilder<Teacher> builder)
        {
            builder.HasKey(t => t.Id);

            builder.HasOne(t => t.User)
                   .WithOne(u => u.TeacherProfile)
                   .HasForeignKey<Teacher>(t => t.Id)
                   .IsRequired();

            builder.Property(t => t.JobTitle).HasMaxLength(150);
            builder.Property(t => t.ShortBio).HasMaxLength(2000);
            builder.Property(t => t.CVUrl).HasMaxLength(500);
            builder.Property(t => t.IntroVideoUrl).HasMaxLength(200);
        }
    }
}
