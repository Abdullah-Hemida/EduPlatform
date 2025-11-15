using System;

namespace Edu.Domain.Entities
{
    // HeroSection.cs
    public enum HeroPlacement
    {
        Home = 0,
        School = 1
    }
    public class HeroSection
    {
        public int Id { get; set; }

        // where this hero is used
        public HeroPlacement Placement { get; set; } = HeroPlacement.Home;

        // Image storage key (use IFileStorageService to store files)
        public string? ImageStorageKey { get; set; }

        // Localized fields (simple approach)
        public string? TitleEn { get; set; }
        public string? TitleIt { get; set; }
        public string? TitleAr { get; set; }

        public string? DescriptionEn { get; set; }
        public string? DescriptionIt { get; set; }
        public string? DescriptionAr { get; set; }

        public bool IsActive { get; set; } = true;

        public int Order { get; set; } = 0;

        // Timestamps (optional)
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }
    }
}
