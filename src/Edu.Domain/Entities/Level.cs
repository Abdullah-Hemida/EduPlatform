// src/Edu.Domain/Entities/Level.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Edu.Domain.Entities;

public class Level
{
    public int Id { get; set; }
    // Multilingual names (store the canonical strings)
    public string NameEn { get; set; } = string.Empty;
    public string NameIt { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public int Order { get; set; }
    public ICollection<Curriculum>? Curricula { get; set; }
    public ICollection<OnlineCourse>? OnlineCourses { get; set; }
}

public class LevelConfiguration : IEntityTypeConfiguration<Level>
{
    public void Configure(EntityTypeBuilder<Level> builder)
    {
        builder.HasKey(l => l.Id);

        // multilingual columns limits
        builder.Property(l => l.NameEn).IsRequired().HasMaxLength(200);
        builder.Property(l => l.NameIt).IsRequired().HasMaxLength(200);
        builder.Property(l => l.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(l => l.Order).IsRequired();
    }
}


