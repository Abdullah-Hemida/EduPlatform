namespace Edu.Web.ViewModels
{
    // OnlineSchoolIndexVm + supporting VMs
    public class OnlineSchoolIndexVm
    {
        public int? SelectedLevelId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 9;
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public HeroVm? SchoolHero { get; set; }
        public List<OnlineSchoolLevelVm> AllLevels { get; set; } = new();
        public List<OnlineCourseCardVm> Courses { get; set; } = new();
    }

    public class OnlineSchoolLevelVm
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int CourseCount { get; set; } = 0;
    }

    public class OnlineCourseCardVm
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string? CoverImageUrl { get; set; }
        public string? PricePerMonthLabel { get; set; }
        public int DurationMonths { get; set; }
        public int LevelId { get; set; }
        public int LessonCount { get; set; }
        public bool IsPublished { get; set; }
    }

    // Details VMs
    public class OnlineCoursePublicDetailsVm
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string? CoverImageUrl { get; set; }
        public string? IntroductionVideoUrl { get; set; }
        public string? PricePerMonthLabel { get; set; }
        public int DurationMonths { get; set; }
        public int LevelId { get; set; }
        public string? LevelName { get; set; }
        public string? TeacherName { get; set; }
        public List<OnlineCourseMonthPublicVm> Months { get; set; } = new();
        public List<OnlineCourseLessonPublicVm> Lessons { get; set; } = new();
    }

    public class OnlineCourseMonthPublicVm
    {
        public int Id { get; set; }
        public int MonthIndex { get; set; }
        public DateTime? MonthStartUtc { get; set; }
        public DateTime? MonthEndUtc { get; set; }
        public bool IsReadyForPayment { get; set; }
    }

    public class OnlineCourseLessonPublicVm
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Notes { get; set; }
        public string? MeetUrl { get; set; }
        public string? RecordedVideoUrl { get; set; }
        public DateTime? ScheduledUtc { get; set; }
        public int Order { get; set; }
        public List<OnlineCourseFilePublicVm> Files { get; set; } = new();
    }

    public class OnlineCourseFilePublicVm
    {
        public int Id { get; set; }
        public string FileName { get; set; } = "";
        public string? PublicUrl { get; set; }
    }
}
