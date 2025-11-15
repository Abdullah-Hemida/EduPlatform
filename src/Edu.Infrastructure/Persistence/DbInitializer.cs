using Edu.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Edu.Infrastructure.Persistence;


public static class DbInitializer
{
    private static readonly string[] DefaultRoles = new[] { "Admin", "Teacher", "Student" };

    public static async Task InitializeAsync(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // 1) Ensure roles - ✅ CORRECT
        foreach (var role in DefaultRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // 2) Ensure admin user - ✅ CORRECT
        var adminEmail = configuration["AdminUser:Email"] ?? "admin@localhost";
        var adminPassword = configuration["AdminUser:Password"] ?? "Admin123!";

        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin == null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "Abdullah Hemida",
                EmailConfirmed = true,
                PhotoUrl = null
            };

            var createRes = await userManager.CreateAsync(admin, adminPassword);
            if (createRes.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Admin");
            }
            else
            {
                throw new Exception("Failed to create seed admin user: " + string.Join("; ", createRes.Errors));
            }
        }
        else
        {
            if (!await userManager.IsInRoleAsync(admin, "Admin"))
            {
                await userManager.AddToRoleAsync(admin, "Admin");
            }
        }
    }
}

