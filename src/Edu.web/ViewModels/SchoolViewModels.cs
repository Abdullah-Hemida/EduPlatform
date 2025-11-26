
namespace Edu.Web.ViewModels
{
    // PaginationVm.cs (partial model used to render the pagination partial)
    public class PaginationVm
    {
        public int Page { get; set; }
        public int TotalPages { get; set; }
        public int PageSize { get; set; }
        public int? LevelId { get; set; }
    }

    // SchoolIndexVm.cs
    public class SchoolIndexVm
    {
        public List<SchoolLevelVm> AllLevels { get; set; } = new();
        public List<CurriculumSummaryVm> Curricula { get; set; } = new();
        public int? SelectedLevelId { get; set; }
        public HeroVm? SchoolHero { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 9;
        public int TotalCount { get; set; } = 0;
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 1;
    }

    // SchoolLevelVm.cs (existing in your project — ensure it has CurriculaCountText)
    public class SchoolLevelVm
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Order { get; set; }
        public string CurriculaCountText { get; set; } = "";
        public List<CurriculumSummaryVm> Curricula { get; set; } = new();
    }
    public class CurriculumSummaryVm
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string? CoverImageUrl { get; set; }
        public string? CoverImageKey { get; set; }
        public int ModuleCount { get; set; }
        public bool IsAccessible { get; set; } = false; // computed per-user
        public int Order { get; set; } = 0; // optional if you want stable ordering
    }
    public class CurriculumDetailsVm
    {
        public int Id { get; set; }
        public int LevelId { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string? CoverImageUrl { get; set; }
        public HeroVm? SchoolHero { get; set; }
        public List<ModuleVm> Modules { get; set; } = new();
    }

    public class ModuleVm
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public int Order { get; set; }
        public List<LessonVm> Lessons { get; set; } = new();
    }

    public class LessonVm
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string? YouTubeVideoId { get; set; }
        public string? VideoUrl { get; set; }
        public bool IsFree { get; set; }
        public int Order { get; set; }
        public List<FileResourceVm> Files { get; set; } = new();
    }

    public class FileResourceVm
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? FileType { get; set; }
        public string? PublicUrl { get; set; }
    }

    public class LessonDetailsVm : LessonVm
    {
        public string? ModuleTitle { get; set; }
        public string? CurriculumTitle { get; set; }
    }
}

