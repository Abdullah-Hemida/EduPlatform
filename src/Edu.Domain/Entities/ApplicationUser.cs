using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Edu.Domain.Entities
{
    public class ApplicationUser : IdentityUser
    {
        // Common profile fields for all users
        public string FullName { get; set; } = string.Empty;
        public string? PhotoUrl { get; set; } 
        public string? PhotoStorageKey { get; set; }
        public DateTime? DateOfBirth { get; set; }

        // Soft-delete flag
        public bool IsDeleted { get; set; } = false;

        // Navigation
        public virtual Teacher? TeacherProfile { get; set; }
        public virtual Student? StudentProfile { get; set; }
        public virtual Admin? AdminProfile { get; set; }
    }
    public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
    {
        public void Configure(EntityTypeBuilder<ApplicationUser> builder)
        {
            // Identity sets primary key and columns; we only further configure lengths/defaults.
            builder.Property(u => u.FullName)
                   .HasMaxLength(200)
                   .IsRequired();

            builder.Property(u => u.PhotoUrl)
                   .HasMaxLength(500);

            // DateOfBirth stays nullable; nothing else required, but you could set column type
            builder.Property(u => u.DateOfBirth)
                   .HasColumnType("date"); // optional: store only date portion

            builder.Property(u => u.IsDeleted)
                   .HasDefaultValue(false);

            // If you want index for fast search by FullName / Email / PhoneNumber:
            builder.HasIndex(u => u.FullName);
            builder.HasIndex(u => u.Email).IsUnique(false); // Identity already enforces unique email if configured
        }
    }
}
