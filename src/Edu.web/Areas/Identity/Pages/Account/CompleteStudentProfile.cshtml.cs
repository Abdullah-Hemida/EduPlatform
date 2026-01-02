// Edu.Web.Areas.Identity.Pages.Account.CompleteStudentProfileModel.cs
using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using System.Text.RegularExpressions;

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
            // derive region from culture (fallback "IT")
            string defaultRegion = "IT";
            try
            {
                // Replace this line:
                // ViewBag.DefaultRegion = defaultRegion;
                // With this line:
                // ViewData["DefaultRegion"] = defaultRegion;
                var regionInfo = new System.Globalization.RegionInfo(System.Globalization.CultureInfo.CurrentCulture.Name);
                defaultRegion = regionInfo.TwoLetterISORegionName ?? "IT";
            }
            catch { defaultRegion = "IT"; }

            ViewData["DefaultRegion"] = defaultRegion;
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

            // Determine default region from current culture (fallback to IT)
            string defaultRegion = "IT";
            try
            {
                var regionInfo = new System.Globalization.RegionInfo(System.Globalization.CultureInfo.CurrentCulture.Name);
                if (!string.IsNullOrEmpty(regionInfo.TwoLetterISORegionName))
                    defaultRegion = regionInfo.TwoLetterISORegionName;
            }
            catch { defaultRegion = "IT"; }

            /* ---------------------------
               PHOTO UPLOAD
            --------------------------- */
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

            /* ---------------------------
               PHONE VALIDATION & NORMALIZATION
               (Student phone and guardian phone)
            --------------------------- */

            // STUDENT PHONE
            string? normalizedStudentPhone = null;
            if (!string.IsNullOrWhiteSpace(Input.PhoneNumber))
            {
                var raw = Input.PhoneNumber.Trim();
                normalizedStudentPhone = PhoneHelpers.ToE164(raw, defaultRegion);

                // fallback: accept E.164-ish value (e.g. +391112223334 or 391112223334)
                if (normalizedStudentPhone == null)
                {
                    if (Regex.IsMatch(raw, @"^\+?\d{6,15}$"))
                    {
                        normalizedStudentPhone = raw.StartsWith("+") ? raw : "+" + raw;
                    }
                }

                _logger.LogDebug("Phone raw='{Raw}', normalized='{Norm}'", raw, normalizedStudentPhone);

                if (normalizedStudentPhone == null)
                {
                    ModelState.AddModelError(nameof(Input.PhoneNumber), _localizer["Validation.InvalidPhone"] ?? "Invalid phone number.");
                    await OnGetAsync();
                    return Page();
                }
            }

            // GUARDIAN PHONE (optional)
            string? normalizedGuardianPhone = null;
            if (!string.IsNullOrWhiteSpace(Input.GuardianPhoneNumber))
            {
                var rawG = Input.GuardianPhoneNumber.Trim();
                normalizedGuardianPhone = PhoneHelpers.ToE164(rawG, defaultRegion);

                if (normalizedGuardianPhone == null)
                {
                    if (Regex.IsMatch(rawG, @"^\+?\d{6,15}$"))
                    {
                        normalizedGuardianPhone = rawG.StartsWith("+") ? rawG : "+" + rawG;
                    }
                }

                _logger.LogDebug("Guardian raw='{RawG}', normalized='{NormG}'", rawG, normalizedGuardianPhone);

                if (normalizedGuardianPhone == null)
                {
                    ModelState.AddModelError(nameof(Input.GuardianPhoneNumber), _localizer["Validation.InvalidPhone"] ?? "Invalid phone number.");
                    await OnGetAsync();
                    return Page();
                }
            }

            /* ---------------------------
               UPDATE USER BASIC FIELDS
            --------------------------- */

            user.FullName = Input.FullName;
            user.DateOfBirth = Input.DateOfBirth;

            // SAVE preferred language (validate allowed values)
            var chosen = string.IsNullOrWhiteSpace(Input.PreferredLanguage) ? "it" : Input.PreferredLanguage;
            var allowed = new[] { "en", "it", "ar", "en-US", "it-IT", "ar-SA" };
            if (!allowed.Contains(chosen))
                chosen = chosen.Length >= 2 ? chosen.Substring(0, 2).ToLowerInvariant() : "it";
            user.PreferredLanguage = chosen;

            // Use SetPhoneNumberAsync so Identity keeps metadata in sync
            var phoneToSet = normalizedStudentPhone ?? string.Empty;
            var phoneRes = await _userManager.SetPhoneNumberAsync(user, phoneToSet);
            if (!phoneRes.Succeeded)
            {
                _logger.LogWarning("Failed SetPhoneNumberAsync for user {UserId} : {Errors}", user.Id, string.Join(";", phoneRes.Errors.Select(e => e.Description)));
                ModelState.AddModelError("", _localizer["Profile.UpdatePhoneFailed"] ?? "Failed to update phone number");
                await OnGetAsync();
                return Page();
            }

            // Update other user fields
            var updRes = await _userManager.UpdateAsync(user);
            if (!updRes.Succeeded)
            {
                _logger.LogError("Failed to update user {UserId}: {Errors}", user.Id,
                    string.Join(" | ", updRes.Errors.Select(e => e.Description)));
                ModelState.AddModelError("", _localizer["Profile.UpdateFailed"] ?? "Failed to update profile");
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

            /* ---------------------------
               STUDENT ENTITY & GUARDIAN PHONE
            --------------------------- */
            var student = await _db.Students.FindAsync(user.Id);
            if (student == null)
            {
                student = new Edu.Domain.Entities.Student { Id = user.Id };
                _db.Students.Add(student);
            }

            student.GuardianPhoneNumber = normalizedGuardianPhone; // store normalized E.164 or null

            /* ---------------------------
               SET CULTURE COOKIE
            --------------------------- */
            try
            {
                var requestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(user.PreferredLanguage);
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

            await _db.SaveChangesAsync();
            await _signInManager.RefreshSignInAsync(user);

            TempData["ProfileSaved"] = _localizer["ProfileSaved"] ?? "Profile saved";
            return RedirectToAction("Index", "Home", new { area = "" });
        }

    }
}



