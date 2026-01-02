// Areas/Identity/Pages/Account/CompleteTeacherProfile.cshtml.cs
using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Edu.Web.Areas.Identity.Pages.Account
{
    public class CompleteTeacherProfileModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _files;
        private readonly IStringLocalizer<CompleteTeacherProfileModel> _localizer;
        private readonly ILogger<CompleteTeacherProfileModel> _logger;

        public CompleteTeacherProfileModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext db,
            IFileStorageService files,
            IStringLocalizer<CompleteTeacherProfileModel> localizer,
            ILogger<CompleteTeacherProfileModel> logger)
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

        [BindProperty]
        public IFormFile? CVFile { get; set; }

        public class InputModel
        {
            public string FullName { get; set; } = string.Empty;
            public string? PhoneNumber { get; set; }
            public DateTime? DateOfBirth { get; set; }
            public string PreferredLanguage { get; set; } = "it";
            // teacher-specific
            public string? JobTitle { get; set; }
            public string? ShortBio { get; set; }
            public string? IntroVideoUrl { get; set; }

            // display-only
            public string? PhotoDisplayUrl { get; set; }
            public string? CVDisplayUrl { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            // Load teacher if exists
            var teacher = await _db.Teachers.FindAsync(user.Id);

            Input.FullName = user.FullName;
            Input.PhoneNumber = await _userManager.GetPhoneNumberAsync(user);
            Input.DateOfBirth = user.DateOfBirth;
            Input.PreferredLanguage = string.IsNullOrWhiteSpace(user.PreferredLanguage) ? "it" : user.PreferredLanguage;

            if (teacher != null)
            {
                Input.JobTitle = teacher.JobTitle;
                Input.ShortBio = teacher.ShortBio;
                Input.IntroVideoUrl = teacher.IntroVideoUrl;
            }

            // Resolve photo/cv public URLs (best-effort)
            try
            {
                Input.PhotoDisplayUrl = await _files.GetPublicUrlAsync(user.PhotoStorageKey ?? user.PhotoUrl);
            }
            catch
            {
                Input.PhotoDisplayUrl = user.PhotoUrl;
            }

            try
            {
                if (teacher != null)
                {
                    Input.CVDisplayUrl = await _files.GetPublicUrlAsync(teacher.CVStorageKey ?? teacher.CVUrl);
                }
            }
            catch
            {
                // ignore - CVDisplayUrl remains null
            }

            // Set default region for intl-tel-input; derive from current culture if possible
            string defaultRegion = "IT";
            try
            {
                var regionInfo = new RegionInfo(CultureInfo.CurrentCulture.Name);
                if (!string.IsNullOrEmpty(regionInfo.TwoLetterISORegionName))
                    defaultRegion = regionInfo.TwoLetterISORegionName;
            }
            catch
            {
                defaultRegion = "IT";
            }

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

            // Determine default region for phone parsing (fallback to IT)
            string defaultRegion = "IT";
            try
            {
                var regionInfo = new RegionInfo(CultureInfo.CurrentCulture.Name);
                if (!string.IsNullOrEmpty(regionInfo.TwoLetterISORegionName))
                    defaultRegion = regionInfo.TwoLetterISORegionName;
            }
            catch
            {
                defaultRegion = "IT";
            }

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

                // delete old file safely
                if (!string.IsNullOrWhiteSpace(user.PhotoStorageKey))
                    await _files.DeleteFileAsync(user.PhotoStorageKey);
                else if (!string.IsNullOrWhiteSpace(user.PhotoUrl))
                    await _files.DeleteFileAsync(user.PhotoUrl);

                // save new file
                var newPhotoKey = await _files.SaveFileAsync(Photo, $"users/{user.Id}");
                if (string.IsNullOrWhiteSpace(newPhotoKey))
                {
                    _logger.LogError("SaveFileAsync returned empty key for user {UserId}", user.Id);
                    ModelState.AddModelError("Photo", _localizer["FileSaveFailed"] ?? "Failed to save file");
                    await OnGetAsync();
                    return Page();
                }

                user.PhotoStorageKey = newPhotoKey;
                user.PhotoUrl = null;
            }

            /* ---------------------------
               CV UPLOAD
            --------------------------- */
            var teacher = await _db.Teachers.FindAsync(user.Id);

            if (CVFile != null)
            {
                if (!FileValidators.IsValidDocument(CVFile))
                {
                    ModelState.AddModelError("CVFile", _localizer["InvalidDocument"]);
                    await OnGetAsync();
                    return Page();
                }

                // delete old CV if any
                if (teacher != null)
                {
                    if (!string.IsNullOrWhiteSpace(teacher.CVStorageKey))
                        await _files.DeleteFileAsync(teacher.CVStorageKey);
                    else if (!string.IsNullOrWhiteSpace(teacher.CVUrl))
                        await _files.DeleteFileAsync(teacher.CVUrl);
                }

                // save new CV
                var newCvKey = await _files.SaveFileAsync(CVFile, $"users/{user.Id}/cv");
                if (teacher != null)
                {
                    teacher.CVStorageKey = newCvKey;
                    teacher.CVUrl = null;
                }
            }

            /* ---------------------------
               PHONE VALIDATION & NORMALIZATION
            --------------------------- */
            string? normalizedPhone = null;
            if (!string.IsNullOrWhiteSpace(Input.PhoneNumber))
            {
                var raw = Input.PhoneNumber.Trim();
                normalizedPhone = PhoneHelpers.ToE164(raw, defaultRegion);

                // fallback: accept raw E.164-ish digits string (e.g. +39123456789 or 39123456789)
                if (normalizedPhone == null)
                {
                    if (Regex.IsMatch(raw, @"^\+?\d{6,15}$"))
                    {
                        normalizedPhone = raw.StartsWith("+") ? raw : "+" + raw;
                    }
                }

                _logger.LogDebug("Teacher phone raw='{Raw}', normalized='{Norm}'", raw, normalizedPhone);

                if (normalizedPhone == null)
                {
                    ModelState.AddModelError(nameof(Input.PhoneNumber), _localizer["Validation.InvalidPhone"] ?? "Invalid phone number.");
                    await OnGetAsync();
                    return Page();
                }
            }

            /* ---------------------------
               UPDATE BASIC USER FIELDS
            --------------------------- */
            user.FullName = Input.FullName;
            user.DateOfBirth = Input.DateOfBirth;

            // validate/sanitize preferred language
            var chosen = string.IsNullOrWhiteSpace(Input.PreferredLanguage) ? "it" : Input.PreferredLanguage;
            var allowed = new[] { "en", "it", "ar", "en-US", "it-IT", "ar-SA" };
            if (!allowed.Contains(chosen))
                chosen = chosen.Length >= 2 ? chosen.Substring(0, 2).ToLowerInvariant() : "it";
            user.PreferredLanguage = chosen;

            // Set phone via Identity (use empty string to clear)
            var phoneToSet = normalizedPhone ?? string.Empty;
            var phoneRes = await _userManager.SetPhoneNumberAsync(user, phoneToSet);
            if (!phoneRes.Succeeded)
            {
                _logger.LogWarning("Failed SetPhoneNumberAsync for user {UserId} : {Errors}", user.Id, string.Join(";", phoneRes.Errors.Select(e => e.Description)));
                ModelState.AddModelError("", _localizer["Profile.UpdatePhoneFailed"] ?? "Failed to update phone number");
                await OnGetAsync();
                return Page();
            }

            // persist other user updates
            var updateRes = await _userManager.UpdateAsync(user);
            if (!updateRes.Succeeded)
            {
                _logger.LogError("Failed to update user {UserId}: {Errors}", user.Id, string.Join(" | ", updateRes.Errors.Select(e => e.Description)));
                ModelState.AddModelError("", _localizer["Profile.UpdateFailed"] ?? "Failed to update profile");
                await OnGetAsync();
                return Page();
            }

            // Ensure teacher entity exists and update teacher-specific fields
            if (teacher == null)
            {
                teacher = new Edu.Domain.Entities.Teacher
                {
                    Id = user.Id,
                    Status = TeacherStatus.Pending
                };
                _db.Teachers.Add(teacher);
            }

            teacher.JobTitle = Input.JobTitle ?? string.Empty;
            teacher.ShortBio = Input.ShortBio;
            teacher.IntroVideoUrl = YouTubeHelper.ExtractYouTubeId(Input.IntroVideoUrl ?? "");

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

            await _db.SaveChangesAsync();
            await _signInManager.RefreshSignInAsync(user);

            TempData["ProfileSaved"] = _localizer["ProfileSaved"] ?? "Profile saved";
            return RedirectToAction("Index", "Home", new { area = "" });
        }
    }
}




