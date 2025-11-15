
using Edu.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace Edu.Web.Areas.Admin.ViewModels
{
    public class CurriculumCreateViewModel
    {
        [Required, StringLength(250)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public int LevelId { get; set; }

        public IFormFile? CoverImage { get; set; }

        public int Order { get; set; } = 0;
    }

    public class CurriculumEditViewModel
    {
        public int Id { get; set; }
        [Required, StringLength(250)] public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        [Required] public int LevelId { get; set; }
        public IFormFile? CoverImage { get; set; }
        public string? ExistingCoverUrl { get; set; }
        public int Order { get; set; }
    }

    // Curriculum list filtering VM
    public class CurriculumListViewModel
    {
        public int? LevelId { get; set; }
        public IEnumerable<Edu.Domain.Entities.Level>? Levels { get; set; }
        public IEnumerable<Edu.Domain.Entities.Curriculum>? Curricula { get; set; }
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

        // Optional nested VMs used by the inline create forms
        public ModuleCreateViewModel NewModule { get; set; } = new ModuleCreateViewModel();
        public LessonCreateViewModel NewLesson { get; set; } = new LessonCreateViewModel();
    }

    public class ModuleCreateViewModel
    {
        [Required] public int CurriculumId { get; set; }
        [Required, StringLength(250)] public string Title { get; set; } = string.Empty;
        public int Order { get; set; } = 1;
    }
    public class ModuleEditViewModel
    {
        [Required]
        public int Id { get; set; }

        [Required]
        public int CurriculumId { get; set; }

        [Required, StringLength(250)]
        public string Title { get; set; } = string.Empty;

        public int Order { get; set; } = 1;
    }

    public class LessonCreateViewModel
    {
        [Required]
        public int ModuleId { get; set; }

        [Required, StringLength(300)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Url]
        public string? YouTubeUrl { get; set; }

        public bool IsFree { get; set; } = false;

        public int Order { get; set; } = 1;

        public IFormFileCollection? Files { get; set; }
    }
    public class LessonEditViewModel
    {
        [Required]
        public int Id { get; set; }

        [Required]
        public int ModuleId { get; set; }

        [Required, StringLength(300)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Url]
        public string? YouTubeUrl { get; set; }

        public bool IsFree { get; set; } = false;

        public int Order { get; set; } = 1;
    }


}

