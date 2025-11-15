using Edu.Domain.Entities;
using Edu.Web.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Edu.Web.Views.Shared.Components.ViewComponents
{
    public class UserMenuViewComponent : ViewComponent
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UserMenuViewComponent(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            // If not logged in → guest menu
            if (!(User.Identity?.IsAuthenticated ?? false))
                return View("Guest");

            // Cast User to ClaimsPrincipal to use FindFirstValue
            var claimsPrincipal = User as ClaimsPrincipal;
            var userId = claimsPrincipal?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return View("Guest");

            // Fetch basic info only (no navigation loads)
            var user = await _userManager.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new UserMenuViewModel
                {
                    FullName = u.FullName,
                    Email = u.Email,
                    PhotoUrl = u.PhotoUrl
                })
                .FirstOrDefaultAsync();

            if (user == null)
                return View("Guest");

            // Read role from claims (no DB call)
            var roleClaim = claimsPrincipal?.FindFirstValue(ClaimTypes.Role);
            user.Role = roleClaim ?? "User";

            return View(user);
        }
    }
}
