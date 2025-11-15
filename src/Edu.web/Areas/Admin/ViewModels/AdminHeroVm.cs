using Edu.Domain.Entities;

namespace Edu.Web.Areas.Admin.ViewModels
{
    // AdminHeroVm.cs
    public class AdminHeroVm
    {
        public int Id { get; set; }
        public HeroPlacement Placement { get; set; }
        public IFormFile? ImageFile { get; set; }
        public string? ImageStorageKey { get; set; }

        public string? TitleEn { get; set; }
        public string? TitleIt { get; set; }
        public string? TitleAr { get; set; }

        public string? DescriptionEn { get; set; }
        public string? DescriptionIt { get; set; }
        public string? DescriptionAr { get; set; }

        public bool IsActive { get; set; } = true;
        public int Order { get; set; } = 0;
    }
}
