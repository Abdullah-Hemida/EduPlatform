using Edu.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Edu.Infrastructure.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> opts) : base(opts) { }

        // DbSets
        public DbSet<Teacher> Teachers { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<Admin> Admins { get; set; }

        public DbSet<Level> Levels { get; set; }
        public DbSet<Curriculum> Curricula { get; set; }
        public DbSet<SchoolModule> SchoolModules { get; set; }
        public DbSet<SchoolLesson> SchoolLessons { get; set; }

        public DbSet<Category> Categories { get; set; }
        public DbSet<PrivateModule> PrivateModules { get; set; }
        public DbSet<PrivateCourse> PrivateCourses { get; set; }
        public DbSet<PrivateLesson> PrivateLessons { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Slot> Slots { get; set; }
        public DbSet<PurchaseRequest> PurchaseRequests { get; set; }
        public DbSet<FileResource> FileResources { get; set; }
        public DbSet<CourseModerationLog> CourseModerationLogs { get; set; }
        public DbSet<BookingModerationLog> BookingModerationLogs { get; set; }
        public DbSet<ReactiveCourse> ReactiveCourses { get; set; }
        public DbSet<ReactiveCourseMonth> ReactiveCourseMonths { get; set; }
        public DbSet<ReactiveCourseLesson> ReactiveCourseLessons { get; set; }
        public DbSet<ReactiveEnrollment> ReactiveEnrollments { get; set; }
        public DbSet<ReactiveEnrollmentMonthPayment> ReactiveEnrollmentMonthPayments { get; set; }
        public DbSet<ReactiveCourseModerationLog> ReactiveCourseModerationLogs { get; set; }
        public DbSet<ReactiveEnrollmentLog> ReactiveEnrollmentLogs { get; set; } = null!;
        public DbSet<HomeSection> HomeSections { get; set; }
        public DbSet<HomeSectionTranslation> HomeSectionTranslations { get; set; }
        public DbSet<HomeSectionItem> HomeSectionItems { get; set; }
        public DbSet<HomeSectionItemTranslation> HomeSectionItemTranslations { get; set; }
        public DbSet<FooterContact> FooterContacts { get; set; }
        public DbSet<SocialLink> SocialLinks { get; set; }
        public DbSet<HeroSection> HeroSections { get; set; }
        public DbSet<StudentCurriculum> StudentCurricula { get; set; } = null!;
        public DbSet<OnlineCourse> OnlineCourses { get; set; }
        public DbSet<OnlineCourseMonth> OnlineCourseMonths { get; set; }
        public DbSet<OnlineCourseLesson> OnlineCourseLessons { get; set; }
        public DbSet<OnlineEnrollment> OnlineEnrollments { get; set; }
        public DbSet<OnlineEnrollmentMonthPayment> OnlineEnrollmentMonthPayments { get; set; }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.ApplyConfiguration(new ApplicationUserConfiguration());
            builder.ApplyConfiguration(new StudentConfiguration());
            builder.ApplyConfiguration(new TeacherConfiguration());
            builder.ApplyConfiguration(new AdminConfiguration());

            builder.ApplyConfiguration(new LevelConfiguration());
            builder.ApplyConfiguration(new CurriculumConfiguration());
            builder.ApplyConfiguration(new SchoolModuleConfiguration());
            builder.ApplyConfiguration(new SchoolLessonConfiguration());

            builder.ApplyConfiguration(new CategoryConfiguration());
            builder.ApplyConfiguration(new PrivateCourseConfiguration());
            builder.ApplyConfiguration(new PrivateModuleConfiguration());
            builder.ApplyConfiguration(new PrivateLessonConfiguration());

            builder.ApplyConfiguration(new PurchaseRequestConfiguration());

            builder.ApplyConfiguration(new BookingConfiguration());

            builder.ApplyConfiguration(new FileResourceConfiguration());
            builder.ApplyConfiguration(new CourseModerationLogConfiguration());
            builder.ApplyConfiguration(new BookingModerationLogConfiguration());
            builder.ApplyConfiguration(new ReactiveCourseConfiguration());
            builder.ApplyConfiguration(new SlotConfiguration());
            builder.ApplyConfiguration(new ReactiveEnrollmentConfiguration());
            builder.ApplyConfiguration(new ReactiveEnrollmentLogConfiguration());
            builder.ApplyConfiguration(new ReactiveCourseModerationLogConfiguration());
            builder.ApplyConfiguration(new ReactiveEnrollmentMonthPaymentConfiguration());
            builder.ApplyConfiguration(new HomeSectionConfiguration());
            builder.ApplyConfiguration(new HomeSectionTranslationConfiguration());
            builder.ApplyConfiguration(new HomeSectionItemConfiguration());
            builder.ApplyConfiguration(new HomeSectionItemTranslationConfiguration());
            builder.ApplyConfiguration(new FooterContactConfiguration());
            builder.ApplyConfiguration(new SocialLinkConfiguration());
            builder.ApplyConfiguration(new StudentCurriculumConfiguration());
            builder.ApplyConfiguration(new OnlineCourseConfiguration());
            builder.ApplyConfiguration(new OnlineCourseMonthConfiguration());
            builder.ApplyConfiguration(new OnlineCourseLessonConfiguration());
            builder.ApplyConfiguration(new OnlineEnrollmentConfiguration());
            builder.ApplyConfiguration(new OnlineEnrollmentMonthPaymentConfiguration());
        }
    }
}

//dotnet ef migrations add ModifiedforSchool --project src/Edu.Infrastructure/Edu.Infrastructure.csproj --startup-project src/Edu.Web/Edu.Web.csproj
//@Html.Raw(System.Text.Json.JsonSerializer.Serialize(L["Admin.Allowed"].Value ?? "Allowed"));