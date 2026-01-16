
//using Microsoft.EntityFrameworkCore;
//using Microsoft.EntityFrameworkCore.Metadata.Builders;

//namespace Edu.Domain.Entities
//{
//    public class ReactiveEnrollmentLog
//    {
//        public int Id { get; set; }
//        public int ReactiveCourseId { get; set; }
//        public int EnrollmentId { get; set; }
//        public string? ActorId { get; set; }
//        public string? ActorName { get; set; }
//        public string? Action { get; set; } // "RequestedPayment","MonthPaid","MonthRejected"
//        public string? Note { get; set; }
//        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
//    }
//    public class ReactiveEnrollmentLogConfiguration : IEntityTypeConfiguration<ReactiveEnrollmentLog>
//    {
//        public void Configure(EntityTypeBuilder<ReactiveEnrollmentLog> b)
//        {
//            b.HasKey(x => x.Id);
//            b.HasIndex(x => x.ReactiveCourseId);
//            b.Property(x => x.Action).HasMaxLength(200);
//            b.Property(x => x.ActorName).HasMaxLength(200);
//        }
//    }
//}
