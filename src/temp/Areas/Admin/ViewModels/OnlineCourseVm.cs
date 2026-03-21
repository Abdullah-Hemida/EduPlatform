using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Edu.Web.Areas.Admin.ViewModels
{
    public class OnlineCourseListItemVm
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string? CoverPublicUrl { get; set; }
        public string PricePerMonthLabel { get; set; } = "";
        public int DurationMonths { get; set; }
        public bool IsPublished { get; set; }
    }

    public class OnlineCourseCreateVm
    {
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public IFormFile? CoverImage { get; set; }
        public string? IntroductionVideoUrl { get; set; }
        public decimal PricePerMonth { get; set; }
        public int DurationMonths { get; set; } = 1;
        public int LevelId { get; set; }
        public string? TeacherName { get; set; }
        public bool IsPublished { get; set; } = false;

        // helper for form select
        public List<SelectListItem>? Levels { get; set; }
    }

    public class OnlineCourseEditVm : OnlineCourseCreateVm
    {
        public int Id { get; set; }
        public IFormFile? NewCoverImage { get; set; }
        public string? ExistingCoverPublicUrl { get; set; }
    }

    public class OnlineCourseDetailsVm
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string? CoverPublicUrl { get; set; }
        public string? IntroductionVideoUrl { get; set; }
        public string? TeacherName { get; set; }
        public string PricePerMonthLabel { get; set; } = "";
        public int DurationMonths { get; set; }
        public int LevelId { get; set; }
        public string? LevelName { get; set; }
        public bool IsPublished { get; set; }
        public List<OnlineCourseMonthVm> Months { get; set; } = new();
        public List<OnlineCourseLessonVm> Lessons { get; set; } = new();
    }

    public class OnlineCourseMonthVm
    {
        public int Id { get; set; }
        public int MonthIndex { get; set; }
        public bool IsReadyForPayment { get; set; }
        public int LessonsCount { get; set; }
    }

    public class OnlineCourseLessonVm
    {
        public int Id { get; set; }
        public int? OnlineCourseMonthId { get; set; }
        public string Title { get; set; } = "";
        public string? MeetUrl { get; set; }
        public string? RecordedVideoUrl { get; set; }
        public string? Notes { get; set; }
        public DateTime? ScheduledUtc { get; set; }
        public int Order { get; set; }
        public List<OnlineCourseLessonFileVm> Attachments { get; set; } = new();
    }

    public class OnlineCourseLessonFileVm
    {
        public int Id { get; set; }
        public string FileName { get; set; } = "";
        public string? PublicUrl { get; set; }
        public string? DownloadUrl { get; set; }
    }

    public class OnlineCourseLessonCreateVm
    {
        public int OnlineCourseMonthId { get; set; }
        public string Title { get; set; } = "";
        public string? MeetUrl { get; set; }
        public string? RecordedVideoUrl { get; set; }
        public string? Notes { get; set; }
        public DateTime? ScheduledUtc { get; set; }
        public int Order { get; set; }
        public List<IFormFile>? Attachments { get; set; }
    }

    public class OnlineCourseLessonEditVm : OnlineCourseLessonCreateVm
    {
        public int Id { get; set; }
        public List<IFormFile>? NewAttachments { get; set; }
    }
}

