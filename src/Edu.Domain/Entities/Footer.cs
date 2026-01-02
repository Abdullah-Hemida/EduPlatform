
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Edu.Domain.Entities
{
    // FooterContact.cs
    public class FooterContact
    {
        public int Id { get; set; }
        public string Culture { get; set; } = "en";
        public string PersonName { get; set; } = "";
        public string Position { get; set; } = "";
        public string Contact { get; set; } = "";
        public int Order { get; set; }
    }

    public class FooterContactConfiguration : IEntityTypeConfiguration<FooterContact>
    {
        public void Configure(EntityTypeBuilder<FooterContact> builder)
        {
            builder.Property(x => x.PersonName).HasMaxLength(200);
            builder.Property(x => x.Position).HasMaxLength(200);
            builder.Property(x => x.Contact).HasMaxLength(500);
        }
    }
    // SocialLink.cs
    public class SocialLink
    {
        public int Id { get; set; }
        public string Provider { get; set; } = ""; // e.g. facebook, whatsapp, telegram
        public string Url { get; set; } = "";
        public bool IsVisible { get; set; } = true;
        public int Order { get; set; }
    }
    public class SocialLinkConfiguration : IEntityTypeConfiguration<SocialLink>
    {
        public void Configure(EntityTypeBuilder<SocialLink> builder)
        {
            builder.Property(x => x.Provider).HasMaxLength(100);
            builder.Property(x => x.Url).HasMaxLength(1000);
        }
    }
}
