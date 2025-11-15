using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Edu.Domain.Entities
{
    public class FileResource
    {
        public int Id { get; set; }

        // optional foreign keys
        public int? SchoolLessonId { get; set; }
        public int? PrivateLessonId { get; set; }

        // provider-agnostic storage key (recommended to store)
        public string? StorageKey { get; set; }

        // optional quick public url (may be null; prefer resolving via file storage service)
        public string? FileUrl { get; set; }

        // human-friendly original file name
        public string? Name { get; set; }

        // content type (mime)
        public string? FileType { get; set; }

        // timestamps
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }

        // navigations
        public SchoolLesson? SchoolLesson { get; set; }
        public PrivateLesson? PrivateLesson { get; set; }
    }

    public class FileResourceConfiguration : IEntityTypeConfiguration<FileResource>
    {
        public void Configure(EntityTypeBuilder<FileResource> builder)
        {
            builder.HasKey(f => f.Id);

            // Optional: set max lengths to reasonable values
            builder.Property(f => f.StorageKey).HasMaxLength(1000).IsUnicode(false);
            builder.Property(f => f.FileUrl).HasMaxLength(2000).IsUnicode(false);
            builder.Property(f => f.Name).HasMaxLength(512).IsUnicode(true);
            builder.Property(f => f.FileType).HasMaxLength(200).IsUnicode(false);

            // timestamps: store as datetime2
            builder.Property(f => f.CreatedAtUtc)
                   .HasColumnType("datetime2")
                   // SQL default for new rows (use SYSUTCDATETIME for UTC on SQL Server)
                   .HasDefaultValueSql("SYSUTCDATETIME()")
                   .IsRequired();

            builder.Property(f => f.UpdatedAtUtc).HasColumnType("datetime2").IsRequired(false);

            // SchoolLesson relationship
            builder.HasOne(f => f.SchoolLesson)
                   .WithMany(l => l.Files)
                   .HasForeignKey(f => f.SchoolLessonId)
                   .IsRequired(false)
                   .OnDelete(DeleteBehavior.SetNull)
                   .HasConstraintName("FK_FileResource_SchoolLesson");

            // PrivateLesson relationship - be explicit
            builder.HasOne(f => f.PrivateLesson)
                   .WithMany(l => l.Files)
                   .HasForeignKey(f => f.PrivateLessonId)
                   .IsRequired(false)
                   .OnDelete(DeleteBehavior.SetNull)
                   .HasConstraintName("FK_FileResource_PrivateLesson");

            // optional index for fast lookup by storage key
            builder.HasIndex(f => f.StorageKey).HasDatabaseName("IX_FileResource_StorageKey");
        }
    }
}


