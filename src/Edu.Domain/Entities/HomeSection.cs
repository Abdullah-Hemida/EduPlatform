
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Edu.Domain.Entities
{


// HomeSection.cs
public class HomeSection
{
    public int Id { get; set; }
    public int Order { get; set; } = 0;                 // ordering of sections
    public string? ImageStorageKey { get; set; }        // optional storage key for a section-level image
    public ICollection<HomeSectionTranslation> Translations { get; set; } = new List<HomeSectionTranslation>();
    public ICollection<HomeSectionItem> Items { get; set; } = new List<HomeSectionItem>();
}
public class HomeSectionConfiguration : IEntityTypeConfiguration<HomeSection>
{
    public void Configure(EntityTypeBuilder<HomeSection> builder)
    {
        builder.HasMany(x => x.Translations).WithOne(x => x.HomeSection).HasForeignKey(x => x.HomeSectionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Items).WithOne(x => x.HomeSection).HasForeignKey(x => x.HomeSectionId).OnDelete(DeleteBehavior.Cascade);
    }
}
    // HomeSectionTranslation.cs
    public class HomeSectionTranslation
{
    public int Id { get; set; }
    public int HomeSectionId { get; set; }
    public string Culture { get; set; } = "en"; // "en", "it", "ar"
    public string Title { get; set; } = "";
    public string? Subtitle { get; set; }
    public HomeSection HomeSection { get; set; } = null!;

}
    public class HomeSectionTranslationConfiguration : IEntityTypeConfiguration<HomeSectionTranslation>
    {
        public void Configure(EntityTypeBuilder<HomeSectionTranslation> builder)
        {
            builder.HasIndex(x => new { x.HomeSectionId, x.Culture }).IsUnique();
            builder.Property(x => x.Title).HasMaxLength(500);
            builder.Property(x => x.Subtitle).HasMaxLength(2000);
        }
    }

    // HomeSectionItem.cs
    public class HomeSectionItem
    {
        public int Id { get; set; }
        public int HomeSectionId { get; set; }
        public int Order { get; set; } = 0;

        // keep Text if you want default; to support translations, either leave Text for fallback or remove and use translations only
        public string? Text { get; set; }     // optional fallback
        public string? ImageStorageKey { get; set; }
        public string? LinkUrl { get; set; }

        public HomeSection HomeSection { get; set; } = null!;
        public ICollection<HomeSectionItemTranslation> Translations { get; set; } = new List<HomeSectionItemTranslation>();
    }
    public class HomeSectionItemConfiguration : IEntityTypeConfiguration<HomeSectionItem>
    {
        public void Configure(EntityTypeBuilder<HomeSectionItem> builder)
        {
            builder.Property(x => x.Text).HasMaxLength(2000);
        }
    }

    public class HomeSectionItemTranslation
    {
        public int Id { get; set; }
        public int HomeSectionItemId { get; set; }
        public string Culture { get; set; } = "en"; // en/it/ar
        public string Text { get; set; } = "";

        public HomeSectionItem HomeSectionItem { get; set; } = null!;
    }
    public class HomeSectionItemTranslationConfiguration : IEntityTypeConfiguration<HomeSectionItemTranslation>
    {
        public void Configure(EntityTypeBuilder<HomeSectionItemTranslation> builder)
        {
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.HomeSectionItemId, x.Culture }).IsUnique();
            builder.Property(x => x.Text).HasMaxLength(2000);
            builder.HasOne(x => x.HomeSectionItem).WithMany(i => i.Translations).HasForeignKey(x => x.HomeSectionItemId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
