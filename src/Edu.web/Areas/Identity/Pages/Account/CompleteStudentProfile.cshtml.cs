// Edu.Web.Areas.Identity.Pages.Account.CompleteStudentProfileModel.cs
using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Localization;

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

            // CORRECTED: PreferredLanguage as a property (default to Italian as your sample)
            public string PreferredLanguage { get; set; } = "it";

            public string GuardianPhoneNumber { get; set; } = string.Empty;
            public DateTime? DateOfBirth { get; set; }

            // display-only (computed)
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
            Input.GuardianPhoneNumber = (await _db.Students.FindAsync(user.Id))?.GuardianPhoneNumber ?? "";
            // safe fallback to default
            Input.PreferredLanguage = string.IsNullOrWhiteSpace(user.PreferredLanguage) ? "it" : user.PreferredLanguage;

            // NEW: generate display URL
            try
            {
                Input.PhotoDisplayUrl = await _files.GetPublicUrlAsync(user.PhotoStorageKey ?? user.PhotoUrl);
            }
            catch
            {
                Input.PhotoDisplayUrl = user.PhotoUrl; // best-effort fallback
            }

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

                if (string.IsNullOrWhiteSpace(newKey))
                {
                    _logger.LogError("SaveFileAsync returned empty key for user {UserId}", user.Id);
                    ModelState.AddModelError("Photo", _localizer["FileSaveFailed"] ?? "Failed to save file");
                    await OnGetAsync();
                    return Page();
                }

                user.PhotoStorageKey = newKey;
                user.PhotoUrl = null;
            }

            // OTHER FIELDS
            user.FullName = Input.FullName;
            user.DateOfBirth = Input.DateOfBirth;

            // SAVE preferred language (validate allowed values)
            var chosen = string.IsNullOrWhiteSpace(Input.PreferredLanguage) ? "it" : Input.PreferredLanguage;
            var allowed = new[] { "en", "it", "ar", "en-US", "it-IT", "ar-SA" };
            if (!allowed.Contains(chosen))
            {
                // normalize by taking first two letters
                chosen = chosen.Length >= 2 ? chosen.Substring(0, 2).ToLowerInvariant() : "it";
            }
            user.PreferredLanguage = chosen;

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

            // reload entity into injected DbContext (best-effort)
            try
            {
                await _db.Entry(user).ReloadAsync();
            }
            catch (System.Exception ex)
            {
                _logger.LogWarning(ex, "Failed to reload user entity into injected DbContext after update for {UserId}", user.Id);
            }

            // ensure student exists
            var student = await _db.Students.FindAsync(user.Id);
            if (student == null)
            {
                student = new Edu.Domain.Entities.Student { Id = user.Id };
                student.GuardianPhoneNumber = Input.GuardianPhoneNumber;
                _db.Students.Add(student);
                await _db.SaveChangesAsync();
            }

            // Set the request-culture cookie so the user's language selection applies immediately
            try
            {
                var requestCulture = new RequestCulture(user.PreferredLanguage);
                var cookieValue = CookieRequestCultureProvider.MakeCookieValue(requestCulture);
                Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName, cookieValue, new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    HttpOnly = false,
                    IsEssential = true,
                    Secure = Request.IsHttps
                });
            }
            catch (System.Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set localization cookie for user {UserId} with culture {Culture}", user.Id, user.PreferredLanguage);
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["ProfileSaved"] = _localizer["ProfileSaved"] ?? "Profile saved";
            return RedirectToAction("Index", "Home", new { area = "" });
        }
    }
}



