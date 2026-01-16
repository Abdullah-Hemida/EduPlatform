
//using Microsoft.EntityFrameworkCore;
//using Microsoft.EntityFrameworkCore.Metadata.Builders;

//namespace Edu.Domain.Entities
//{
//    public class ReactiveCourseModerationLog
//    {
//        public int Id { get; set; }
//        public int ReactiveCourseId { get; set; }
//        public string? ActorId { get; set; }
//        public string? ActorName { get; set; }
//        public string? Action { get; set; } // e.g. "MonthReady","MonthUnready","MonthDeleted"
//        public string? Note { get; set; }
//        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
//        public ReactiveCourse? ReactiveCourse { get; set; }
//    }
//    public class ReactiveCourseModerationLogConfiguration : IEntityTypeConfiguration<ReactiveCourseModerationLog>
//    {
//        public void Configure(EntityTypeBuilder<ReactiveCourseModerationLog> b)
//        {
//            b.HasKey(x => x.Id);
//            b.HasIndex(x => x.ReactiveCourseId);
//            b.Property(x => x.ActorName).HasMaxLength(200);
//            b.Property(x => x.Action).HasMaxLength(200);
//        }
//    }
//}
