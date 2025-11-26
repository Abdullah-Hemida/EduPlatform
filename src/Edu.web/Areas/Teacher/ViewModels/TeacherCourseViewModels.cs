
using Edu.Domain.Entities;
using Edu.Infrastructure.Helpers;
using Edu.Web.Areas.Admin.ViewModels;
using System.ComponentModel.DataAnnotations;

namespace Edu.Web.Areas.Teacher.ViewModels
{
    // Index VM
    // TeacherCourseIndexVm unchanged except Course item type
    public class TeacherCourseIndexVm
    {
        public string? Query { get; set; }
        public int? CategoryId { get; set; }
        public bool? ShowPublished { get; set; }
        public bool? ForChildren { get; set; }
        public PaginatedList<TeacherCourseListItemVm> Courses { get; set; } = default!;
    }

    public class TeacherCourseListItemVm
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public int CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public decimal Price { get; set; }
        public string? PriceLabel { get; set; }
        public bool IsPublished { get; set; }

        // <-- storage key (what you persist in DB)
        public string? CoverImageKey { get; set; }

        // <-- resolved public URL (set by controller, used in <img src="...">)
        public string? PublicCoverUrl { get; set; }

        public int ModuleCount { get; set; }
        public int LessonCount { get; set; }
    }

    // Create VM
    public class TeacherCourseCreateVm
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public int? CategoryId { get; set; }
        public decimal Price { get; set; }
        public string? PriceLabel { get; set; }
        public IFormFile? CoverImage { get; set; }
        public bool IsPublishRequested { get; set; }
        public bool IsForChildren { get; set; }
    }

    // Edit VM
    public class TeacherCourseEditVm : TeacherCourseCreateVm
    {
        public int Id { get; set; }

        // storage key of existing cover (from DB)
        public string? ExistingCoverKey { get; set; }

        // optional resolved url for preview in the edit page
        public string? ExistingCoverPublicUrl { get; set; }
    }

    // Details VM
    public class TeacherCourseDetailsVm
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }

        // storage key and public url
        public string? CoverImageKey { get; set; }
        public string? PublicCoverUrl { get; set; }

        public decimal Price { get; set; }
        public string? PriceLabel { get; set; }
        public bool IsPublished { get; set; }
        public bool IsPublishRequested { get; set; }
        public int CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? TeacherId { get; set; }
        public bool IsForChildren { get; set; }
        public bool IsOwner { get; set; } = false;
        public List<ModuleSummaryVm> Modules { get; set; } = new();
        public List<PrivateLessonVm> Lessons { get; set; } = new();
        public Dictionary<int, List<PrivateLessonVm>> LessonsByModule { get; set; } = new();
        public List<CourseModerationLogVm>? ModerationLogs { get; set; }
    }


    public class ModuleSummaryVm
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Order { get; set; }
        public int LessonCount { get; set; }
    }

    public class PrivateLessonVm
    {
        public int Id { get; set; }
        public int PrivateCourseId { get; set; }
        public int? PrivateModuleId { get; set; } // null => top-level
        public string Title { get; set; } = string.Empty;
        public string? YouTubeVideoId { get; set; }
        public string? VideoUrl { get; set; }
        public int Order { get; set; }
        public List<FileResourceVm> Files { get; set; } = new();
    }
    // Module VMs
    public class ModuleCreateVm
    {
        public int PrivateCourseId { get; set; }
        public string Title { get; set; } = string.Empty;

        public int Order { get; set; } = 0;
    }

    public class ModuleEditVm
    {
        public int Id { get; set; }
        public int PrivateCourseId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Order { get; set; } = 0;
    }

    // Lesson VMs
    public class LessonCreateVm
    {
        public int PrivateCourseId { get; set; }

        public int? PrivateModuleId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? YouTubeUrl { get; set; }

        public int Order { get; set; } = 0;

        // Input files from form
        public IFormFile[]? Files { get; set; }
    }

    public class LessonEditVm
    {
        public int Id { get; set; }

        public int PrivateCourseId { get; set; }

        public int? PrivateModuleId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? YouTubeUrl { get; set; }

        public int Order { get; set; } = 0;

        // New files to upload
        public IFormFile[]? Files { get; set; }
    }
    public class FileResourceVm
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? FileType { get; set; }
        public string? FileUrl { get; set; }     // legacy
        public string? StorageKey { get; set; }  // preferred
        // server-resolved public link (filled in controller)
        public string? PublicUrl { get; set; }
    }

    public class CourseModerationLogVm
    {
        public int Id { get; set; }
        public string? AdminId { get; set; }
        public string? Note { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string? Action { get; set; } // e.g., "Rejected","Published"
    }
}

