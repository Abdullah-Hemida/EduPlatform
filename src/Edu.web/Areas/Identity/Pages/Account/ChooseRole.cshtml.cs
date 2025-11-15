// File: Areas/Identity/Pages/Account/ChooseRole.cshtml.cs
// ------------------------------------------------------------
using Edu.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduPlatform.Web.Areas.Identity.Pages.Account
{
    public class ChooseRoleModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<ChooseRoleModel> _logger;

        public ChooseRoleModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<ChooseRoleModel> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync(string role)
        {
            if (string.IsNullOrWhiteSpace(role)) return Page();

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("ChooseRole: user was null when posting role. Claims: {Claims}",
                    string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}")));
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            if (!await _userManager.IsInRoleAsync(user, role))
            {
                var addRes = await _userManager.AddToRoleAsync(user, role);
                if (!addRes.Succeeded)
                {
                    _logger.LogError("ChooseRole: AddToRoleAsync failed for user {UserId}: {Errors}",
                        user.Id, string.Join(";", addRes.Errors.Select(e => e.Description)));
                    ModelState.AddModelError("", "Failed to add role");
                    return Page();
                }
            }

            // Important: refresh cookie so the new role claim appears
            await _signInManager.RefreshSignInAsync(user);

            if (role == "Teacher")
                return RedirectToPage("/Account/CompleteTeacherProfile");

            return RedirectToPage("/Account/CompleteStudentProfile");
        }
    }
}
