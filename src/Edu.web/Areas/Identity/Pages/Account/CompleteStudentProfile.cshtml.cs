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

            // ⬅ This will hold the display URL (computed, not stored)
            public string? PhotoDisplayUrl { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToPage("/Account/Login", new
                {
                    area = "Identity",
                    returnUrl = Url.Page("/Account/CompleteStudentProfile")
                });
            }

            Input.FullName = user.FullName;
            Input.PhoneNumber = await _userManager.GetPhoneNumberAsync(user);
            Input.DateOfBirth = user.DateOfBirth;

            // NEW: generate display URL
            Input.PhotoDisplayUrl = await _files.GetPublicUrlAsync(
                user.PhotoStorageKey ?? user.PhotoUrl
            );

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            if (!ModelState.IsValid)
            {
                await OnGetAsync();
                return Page();
            }

            // PHOTO UPLOAD
            if (Photo != null)
            {
                if (!FileValidators.IsValidImage(Photo))
                {
                    ModelState.AddModelError("Photo", _localizer["InvalidImage"]);
                    await OnGetAsync();
                    return Page();
                }

                // delete old file (if any)
                if (!string.IsNullOrWhiteSpace(user.PhotoStorageKey))
                    await _files.DeleteFileAsync(user.PhotoStorageKey);
                else if (!string.IsNullOrWhiteSpace(user.PhotoUrl))
                    await _files.DeleteFileAsync(user.PhotoUrl);

                // save new file via storage service
                var newKey = await _files.SaveFileAsync(Photo, $"users/{user.Id}");

                // defensive: make sure SaveFileAsync returned something meaningful
                if (string.IsNullOrWhiteSpace(newKey))
                {
                    _logger.LogError("SaveFileAsync returned empty key for user {UserId}", user.Id);
                    ModelState.AddModelError("Photo", _localizer["FileSaveFailed"] ?? "Failed to save file");
                    await OnGetAsync();
                    return Page();
                }

                // assign key — this must be the storage KEY (not the public URL).
                user.PhotoStorageKey = newKey;
                user.PhotoUrl = null;
            }

            // OTHER FIELDS
            user.FullName = Input.FullName;
            user.DateOfBirth = Input.DateOfBirth;

            // Prefer SetPhoneNumberAsync (if you want the built-in phone handling).
            var phoneRes = await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber ?? "");
            if (!phoneRes.Succeeded)
            {
                ModelState.AddModelError("", "Failed to update phone number");
                await OnGetAsync();
                return Page();
            }

            // Persist user changes
            var updRes = await _userManager.UpdateAsync(user);
            if (!updRes.Succeeded)
            {
                _logger.LogError("Failed to update user {UserId}: {Errors}", user.Id,
                    string.Join(" | ", updRes.Errors.Select(e => e.Description)));
                ModelState.AddModelError("", "Failed to update profile");
                await OnGetAsync();
                return Page();
            }

            // IMPORTANT: reload user in the DbContext we injected to avoid stale-tracking overwrites
            try
            {
                // this will ensure the injected _db context has the latest values for this user
                await _db.Entry(user).ReloadAsync();
            }
            catch (Exception ex)
            {
                // not fatal, but helpful for debugging if reload fails
                _logger.LogWarning(ex, "Failed to reload user entity into injected DbContext after update for {UserId}", user.Id);
            }

            // ensure student exists
            var student = await _db.Students.FindAsync(user.Id);
            if (student == null)
            {
                student = new Edu.Domain.Entities.Student { Id = user.Id };
                _db.Students.Add(student);
                await _db.SaveChangesAsync();
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["ProfileSaved"] = _localizer["ProfileSaved"] ?? "Profile saved";
            return RedirectToAction("Index", "Home", new { area = "" });
        }
    }
}


