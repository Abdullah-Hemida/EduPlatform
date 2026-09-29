using Edu.Domain.Entities;
using Edu.Infrastructure.Data; // Ensure you import the namespace where ApplicationDbContext lives
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore; // Needed for .MigrateAsync()
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Edu.Infrastructure.Persistence;

public static class DbInitializer
{
    private static readonly string[] DefaultRoles = { "Admin", "Teacher", "Student" };

    public static async Task InitializeAsync(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        using var scope = serviceProvider.CreateScope();

        // 1. Get the Database Context
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // 2. Automatically apply migrations and create tables if they do not exist
        await context.Database.MigrateAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Ensure roles exist
        foreach (var role in DefaultRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var adminEmail = configuration["AdminUser:Email"];
        var adminPassword = configuration["AdminUser:Password"];
        var adminFullName = configuration["AdminUser:FullName"] ?? "Ahmed Khedr";

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
            return;

        // Check if any Admin user already exists
        var adminUsers = await userManager.GetUsersInRoleAsync("Admin");
        var adminExists = adminUsers.Any();

        if (!adminExists)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = adminFullName
            };

            var createResult = await userManager.CreateAsync(admin, adminPassword);

            if (!createResult.Succeeded)
            {
                var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create seed admin user: {errors}");
            }

            var addToRoleResult = await userManager.AddToRoleAsync(admin, "Admin");
            if (!addToRoleResult.Succeeded)
            {
                var errors = string.Join("; ", addToRoleResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to assign Admin role: {errors}");
            }
        }
    }
}


