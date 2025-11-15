using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Edu.Web.Areas.Identity.Pages.Account
{
    public class CompleteStudentProfileModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _files;
        private readonly IStringLocalizer<CompleteStudentProfileModel> _localizer;
        private readonly ILogger<CompleteStudentProfileModel> _logger;

        public CompleteStudentProfileModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext db,
            IFileStorageService files,
            IStringLocalizer<CompleteStudentProfileModel> localizer,
            ILogger<CompleteStudentProfileModel> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _db = db;
            _files = files;
            _localizer = localizer;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        [BindProperty]
        public IFormFile? Photo { get; set; }

        public class InputModel
        {
            public string FullName { get; set; } = string.Empty;
            public string? PhoneNumber { get; set; }
            public DateTime? DateOfBirth { get; set; }
            public string? PhotoUrl { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("CompleteStudentProfile.OnGet: user is null. Claims: {Claims}",
                    string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}")));
                // Redirect to Login instead of Challenge
                return RedirectToPage("/Account/Login", new { area = "Identity", returnUrl = Url.Page("/Account/CompleteStudentProfile") });
            }

            Input.FullName = user.FullName;
            Input.PhoneNumber = await _userManager.GetPhoneNumberAsync(user); // small fix: make sure method name matches
            Input.DateOfBirth = user.DateOfBirth;
            Input.PhotoUrl = user.PhotoUrl;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("CompleteStudentProfile.OnPost: user is null.");
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            if (!ModelState.IsValid)
            {
                await OnGetAsync();
                return Page();
            }

            if (Photo != null)
            {
                if (!FileValidators.IsValidImage(Photo))
                {
                    ModelState.AddModelError("Photo", _localizer["InvalidImage"]);
                    await OnGetAsync();
                    return Page();
                }

                Input.PhotoUrl = await _files.SaveFileAsync(Photo, $"users/{user.Id}");
                user.PhotoUrl = Input.PhotoUrl;
            }

            user.FullName = Input.FullName;
            user.DateOfBirth = Input.DateOfBirth;
            var phoneRes = await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber ?? string.Empty);
            if (!phoneRes.Succeeded)
            {
                _logger.LogWarning("Failed to set phone number for user {UserId}: {Errors}", user.Id, string.Join(";", phoneRes.Errors.Select(e => e.Description)));
            }

            var updRes = await _userManager.UpdateAsync(user);
            if (!updRes.Succeeded)
            {
                _logger.LogError("Failed to update user {UserId}: {Errors}", user.Id, string.Join(";", updRes.Errors.Select(e => e.Description)));
                ModelState.AddModelError("", "Failed to update profile");
                await OnGetAsync();
                return Page();
            }

            // ensure student row exists
            var student = await _db.Students.FindAsync(user.Id);
            if (student == null)
            {
                student = new Edu.Domain.Entities.Student { Id = user.Id };
                _db.Students.Add(student);
            }

            await _db.SaveChangesAsync();

            // Refresh cookie so layout and subsequent pages read latest claims/values
            await _signInManager.RefreshSignInAsync(user);

            TempData["ProfileSaved"] = _localizer["ProfileSaved"] ?? "Profile saved";
            return RedirectToAction("Index", "Home", new { area = "" });
        }
    }
}


