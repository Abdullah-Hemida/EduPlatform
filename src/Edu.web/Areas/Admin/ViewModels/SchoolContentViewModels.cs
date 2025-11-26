
using Edu.Domain.Entities;
namespace Edu.Web.Areas.Admin.ViewModels
{
    public class CurriculumCreateViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int LevelId { get; set; }
        public IFormFile? CoverImage { get; set; }
        public string? CoverImageUrl { get; set; } // for preview if needed
        public string? CoverImageKey { get; set; }

        public int Order { get; set; } = 0;
    }
    public class CurriculumEditViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int LevelId { get; set; }
        public IFormFile? CoverImage { get; set; }
        public string? ExistingCoverUrl { get; set; }
        public string? ExistingCoverKey { get; set; }
        public int Order { get; set; }
    }

    // Curriculum list filtering VM
    public class CurriculumListViewModel
    {
        public int? LevelId { get; set; }
        public IEnumerable<Level>? Levels { get; set; }
        public IEnumerable<Curriculum>? Curricula { get; set; }
    }
    public class CurriculumListItemVm
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? LevelName { get; set; }
        public string? CoverImageUrl { get; set; }
    }
    // Details VM for curriculum view
    public class CurriculumDetailsViewModel
    {
        public Curriculum Curriculum { get; set; } = null!;
        public IEnumerable<SchoolModule> Modules { get; set; } = Enumerable.Empty<SchoolModule>();
        public ModuleCreateViewModel NewModule { get; set; } = new ModuleCreateViewModel();
        public LessonCreateViewModel NewLesson { get; set; } = new LessonCreateViewModel();
        public List<SchoolLesson> DirectLessons { get; set; } = new List<SchoolLesson>();
    }

    public class ModuleCreateViewModel
    {
        public int CurriculumId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Order { get; set; } = 1;
    }
    public class ModuleEditViewModel
    {
        public int Id { get; set; }
        public int CurriculumId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Order { get; set; } = 1;
    }

    public class LessonCreateViewModel
    {
        // optional module; if provided we will infer CurriculumId from module
        public int? ModuleId { get; set; }

        // required (or inferred). For Create GET you can pass curriculumId or moduleId.
        public int CurriculumId { get; set; }

        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? YouTubeUrl { get; set; }
        public bool IsFree { get; set; } = false;
        public int Order { get; set; } = 1;
        public IFormFileCollection? Files { get; set; }
    }

    public class LessonEditViewModel
    {
        public int Id { get; set; }

        // optional - lesson can be assigned/changed to a module or left null
        public int? ModuleId { get; set; }

        // required: lesson must belong to a curriculum; when assigning module we set curriculum from module
        public int CurriculumId { get; set; }

        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? YouTubeUrl { get; set; }
        public bool IsFree { get; set; } = false;
        public int Order { get; set; } = 1;
    }
}

