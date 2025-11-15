
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Edu.Domain.Entities;

// Edu.Domain.Entities.Student
public class Student
{
    public string Id { get; set; } = null!; // Same as ApplicationUser.Id
    public ApplicationUser? User { get; set; }

    // New flag - admin toggles this (default false)
    public bool IsAllowed { get; set; } = false;

    // Navigation
    public ICollection<Booking>? Bookings { get; set; }
    public ICollection<PurchaseRequest>? PurchaseRequests { get; set; }
    public ICollection<ReactiveEnrollment>? ReactiveEnrollments { get; set; }
}


public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.HasKey(s => s.Id);

        builder.HasOne(s => s.User)
               .WithOne(u => u.StudentProfile)
               .HasForeignKey<Student>(s => s.Id);
    }
}

public class Admin
{
    public string Id { get; set; } = null!;
    public ApplicationUser? User { get; set; }
}
public class AdminConfiguration : IEntityTypeConfiguration<Admin>
{
    public void Configure(EntityTypeBuilder<Admin> builder)
    {
        builder.HasKey(a => a.Id);

        builder.HasOne(a => a.User)
               .WithOne(u => u.AdminProfile)
               .HasForeignKey<Admin>(a => a.Id);
    }
}