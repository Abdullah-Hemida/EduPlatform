//using Microsoft.EntityFrameworkCore;
//using Microsoft.EntityFrameworkCore.Metadata.Builders;

//namespace Edu.Domain.Entities
//{
//    public class CourseModerationLog
//    {
//        public int Id { get; set; }
//        public int PrivateCourseId { get; set; }
//        public string? AdminId { get; set; }
//        public string? Action { get; set; }   // e.g. "Approved", "Rejected", "Published", "Unpublished"
//        public string? Note { get; set; }     // optional admin note
//        public DateTime CreatedAtUtc { get; set; }

//        // Navigation
//        public PrivateCourse? PrivateCourse { get; set; }
//    }

//    public class CourseModerationLogConfiguration : IEntityTypeConfiguration<CourseModerationLog>
//    {
//        public void Configure(EntityTypeBuilder<CourseModerationLog> builder)
//        {
//            builder.HasKey(x => x.Id);

//            builder.Property(x => x.Action)
//                   .HasMaxLength(64);

//            builder.Property(x => x.Note)
//                   .HasMaxLength(4000); // adjust as you like

//            builder.Property(x => x.CreatedAtUtc)
//                   .IsRequired()
//                   .HasDefaultValueSql("GETUTCDATE()");

//            builder.HasOne(x => x.PrivateCourse)
//                   .WithMany(p => p.ModerationLogs)
//                   .HasForeignKey(x => x.PrivateCourseId)
//                   .OnDelete(DeleteBehavior.Cascade);

//            // Optional indexes to speed lookups / reporting
//            builder.HasIndex(x => x.PrivateCourseId);
//            builder.HasIndex(x => x.AdminId);
//            builder.HasIndex(x => x.CreatedAtUtc);
//        }
//    }
//}
