
using Edu.Infrastructure.Helpers;

namespace Edu.Web.Areas.Admin.ViewModels
{
    // Admin area / Models
    public class PrivateCourseIndexVm
    {
        public string? Query { get; set; }
        public int? CategoryId { get; set; }
        public bool? ShowPublished { get; set; }
        public bool ShowAll { get; set; } = false;
        public bool? ForChildren { get; set; }

        // Pagination metadata (optional duplicates PaginatedList's values for Razor convenience)
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }

        // The paginated set of items
        public PaginatedList<PrivateCourseListItemVm> Courses { get; set; } = null!;
    }
    public class PrivateCourseListItemVm
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public int? CategoryId { get; set; }
        // projected from DB
        public string? CategoryNameEn { get; set; }
        public string? CategoryNameIt { get; set; }
        public string? CategoryNameAr { get; set; }

        // used by views (localized)
        public string? CategoryName { get; set; }
        public decimal Price { get; set; }
        public string? PriceLabel { get; set; }
        public bool IsPublished { get; set; }
        public bool IsPublishRequested { get; set; }
        public string? CoverImageKey { get; set; }
        public string? PublicCoverUrl { get; set; }
        public bool IsForChildren { get; set; }
        public string? TeacherId { get; set; }
        public string? TeacherName { get; set; }
        public string? TeacherEmail { get; set; }
    }
    public class PrivateCourseDetailsVm
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        // Category projected fields
        public int CategoryId { get; set; }
        public string? CategoryNameEn { get; set; }
        public string? CategoryNameIt { get; set; }
        public string? CategoryNameAr { get; set; }

        // localized name for view
        public string? CategoryName { get; set; }
        public decimal? Price { get; set; }
        public string? PriceLabel { get; set; }

        public bool IsPublished { get; set; }
        public string? CoverImageKey { get; set; }
        public string? PublicCoverUrl { get; set; }
        public TeacherVm? Teacher { get; set; }
        public List<PrivateModuleVm> Modules { get; set; } = new();
        public List<PrivateLessonVm> StandaloneLessons { get; set; } = new();
    }

    public class TeacherVm
    {
        public string? Id { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
    }
    public class FileResourceVm
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        // storage key (preferred) or original DB file url
        public string? StorageKey { get; set; }
        public string? FileUrl { get; set; }
        public string? FileType { get; set; }

        // server-resolved public link (filled in controller)
        public string? PublicUrl { get; set; }

        // server-side anchor fallback -> FileResourcesController.Download
        public string? DownloadUrl { get; set; }
    }

    public class PrivateLessonVm
    {
        public int Id { get; set; }
        public int PrivateCourseId { get; set; }         // added for alignment with controller projection
        public int? PrivateModuleId { get; set; }        // nullable: lessons may be standalone
        public string? Title { get; set; }
        public int Order { get; set; }

        // video info
        public string? YouTubeVideoId { get; set; }
        public string? VideoUrl { get; set; }

        // files info
        public List<FileResourceVm>? Files { get; set; } = new List<FileResourceVm>();
        public int FileCount => Files?.Count ?? 0;
        public bool HasFiles => FileCount > 0;
        public bool HasVideo => !string.IsNullOrEmpty(YouTubeVideoId) || !string.IsNullOrEmpty(VideoUrl);

        // friendly label used in views (computed server-side)
        public string ContentLabel
            => HasVideo && HasFiles ? $"Video + Files ({FileCount})"
               : HasVideo ? "Video"
               : HasFiles ? $"Files ({FileCount})"
               : "No content";
    }

    public class PrivateModuleVm
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public int Order { get; set; }
        public List<PrivateLessonVm> Lessons { get; set; } = new();
    }
}

