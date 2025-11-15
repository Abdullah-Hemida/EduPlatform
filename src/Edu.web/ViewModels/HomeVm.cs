using Edu.Domain.Entities;

namespace Edu.Web.ViewModels
{
    // HeroVm (used by public pages)
    public class HeroVm
    {
        public int Id { get; set; }
        public HeroPlacement Placement { get; set; }
        public string? ImagePublicUrl { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
    }

    public class HomeVm
    {
        public HeroVm? Hero { get; set; }   // <- new: hero for home page
        public List<HomeSectionVm> Sections { get; set; } = new();
        public int TotalStudents { get; set; }
        public int TotalTeachers { get; set; }
        public int TotalCurricula { get; set; }
        public int TotalPrivateCourses { get; set; }
        public int TotalReactiveCourses { get; set; }
    }

    public class HomeSectionVm
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Subtitle { get; set; }
        public string? ImagePublicUrl { get; set; }
        public List<HomeSectionItemVm> Items { get; set; } = new();
    }
    public class HomeSectionItemVm
    {
        public int Id { get; set; }
        public string? Text { get; set; }
        public string? LinkUrl { get; set; }
        public string? ImagePublicUrl { get; set; }
    }
}

