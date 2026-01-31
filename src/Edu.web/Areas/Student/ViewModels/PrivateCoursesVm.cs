using Edu.Domain.Entities;

namespace Edu.Web.Areas.Student.ViewModels
{
    public class PrivateCourseListItemVm
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }

        // storage key (may be null) and resolved public url for UI
        public string? CoverImageKey { get; set; }
        public string? CoverPublicUrl { get; set; }

        public string? TeacherName { get; set; }

        // category
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }

        public decimal? Price { get; set; }
        public string? PriceLabel { get; set; }
    }

    public class PrivateCourseDetailsVm
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }

        // storage key and public url
        public string? CoverImageKey { get; set; }
        public string? CoverPublicUrl { get; set; }

        // teacher
        public string? TeacherId { get; set; }
        public string? TeacherName { get; set; }
        public string? TeacherEmail { get; set; }

        // category (store localized parts and selected/localized name)
        public int? CategoryId { get; set; }
        public string? CategoryNameEn { get; set; }
        public string? CategoryNameIt { get; set; }
        public string? CategoryNameAr { get; set; }
        public string? CategoryName { get; set; } // final localized label chosen at controller/view

        public decimal? Price { get; set; }
        public string? PriceLabel { get; set; }

        public bool IsPublished { get; set; }
        public bool IsPurchased { get; set; } // whether current student has completed purchase
        public int TotalLessons { get; set; }

        // module/lesson structure
        public List<PrivateModuleVm> Modules { get; set; } = new();
        public List<PrivateLessonVm> StandaloneLessons { get; set; } = new();

        // conveniences for rendering
        public Dictionary<int, List<PrivateLessonVm>>? LessonsByModule { get; set; } = new();

        // teacher helper
        public PrivateCourseTeacherVm? Teacher { get; set; }
    }

    public class PrivateModuleVm
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public int Order { get; set; }
        public int LessonCount { get; set; }
        public List<PrivateLessonVm> Lessons { get; set; } = new();
    }

    public class PrivateLessonVm
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string? YouTubeVideoId { get; set; }   // extracted id if available
        public string? VideoUrl { get; set; }         // fallback direct url
        public int? PrivateCourseId { get; set; }
        public int? PrivateModuleId { get; set; }
        public int Order { get; set; }

        // attached files
        public List<FileResourceVm> Files { get; set; } = new();
    }

    public class FileResourceVm
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? StorageKey { get; set; }
        public string? FileUrl { get; set; }
        public string? FileType { get; set; }
        public string? PublicUrl { get; set; }    // resolved provider / normalized URL
        public string? DownloadUrl { get; set; }  // server fallback to FileResources/Download
    }

    public class PurchaseRequestCreateVm
    {
        public int PrivateCourseId { get; set; }
        public string? Note { get; set; }
    }

    public class MyPurchasesVm
    {
        public List<MyPurchaseItemVm> Purchases { get; set; } = new();
    }

    public class MyPurchaseItemVm
    {
        public int Id { get; set; } // PurchaseRequest id
        public int PrivateCourseId { get; set; }
        public string CourseTitle { get; set; } = null!;
        public DateTime RequestDateUtc { get; set; }

        public PurchaseStatus Status { get; set; }

        public string? TeacherName { get; set; }

        // cover (resolved)
        public string? CoverPublicUrl { get; set; }

        // optional amount info (useful in UI)
        public decimal? Amount { get; set; }
        public string? AmountLabel { get; set; }
    }

    public class PrivateCourseTeacherVm
    {
        public string? Id { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
    }
}


