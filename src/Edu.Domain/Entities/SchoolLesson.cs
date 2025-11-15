// src/Edu.Domain/Entities/Lesson.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Edu.Domain.Entities;
public class SchoolLesson
{
    public int Id { get; set; }
    public int ModuleId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? YouTubeVideoId { get; set; }
    public string? VideoUrl { get; set; }
    public bool IsFree { get; set; }
    public int Order { get; set; }
    public SchoolModule? SchoolModule { get; set; }
    public ICollection<FileResource>? Files { get; set; }
}

public class SchoolLessonConfiguration : IEntityTypeConfiguration<SchoolLesson>
{
    public void Configure(EntityTypeBuilder<SchoolLesson> builder)
    {
        builder.HasKey(l => l.Id);
        builder.HasOne(l => l.SchoolModule)
               .WithMany(m => m.SchoolLessons)
               .HasForeignKey(l => l.ModuleId);
    }
}

