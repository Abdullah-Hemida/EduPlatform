// File: Areas/Identity/Pages/Account/CompleteTeacherProfile.cshtml.cs
// ------------------------------------------------------------
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
using Microsoft.Extensions.Logging;

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

    // Display-only URLs
    public string? PhotoDisplayUrl { get; set; }
    public string? CVDisplayUrl { get; set; }

    // Teacher specific
    public string? JobTitle { get; set; }
    public string? ShortBio { get; set; }
    public string? IntroVideoUrl { get; set; }
}


        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToPage("/Account/Login", new
                {
                    area = "Identity",
                    returnUrl = Url.Page("/Account/CompleteTeacherProfile")
                });
            }

            Input.FullName = user.FullName;
            Input.PhoneNumber = await _userManager.GetPhoneNumberAsync(user);
            Input.DateOfBirth = user.DateOfBirth;
            Input.PreferredLanguage = string.IsNullOrWhiteSpace(user.PreferredLanguage) ? "it" : user.PreferredLanguage;
            // Photo (new key or old url fallback)
            var photoKey = user.PhotoStorageKey ?? user.PhotoUrl;
            Input.PhotoDisplayUrl = await _files.GetPublicUrlAsync(photoKey);

            if (await _userManager.IsInRoleAsync(user, "Teacher"))
            {
                var teacher = await _db.Teachers.FindAsync(user.Id);
                if (teacher != null)
                {
                    Input.JobTitle = teacher.JobTitle;
                    Input.ShortBio = teacher.ShortBio;
                    Input.IntroVideoUrl = teacher.IntroVideoUrl;

                    // CV (new key or old url fallback)
                    var cvKey = teacher.CVStorageKey ?? teacher.CVUrl;
                    Input.CVDisplayUrl = await _files.GetPublicUrlAsync(cvKey);
                }
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

                // Delete old file safely
                if (!string.IsNullOrWhiteSpace(user.PhotoStorageKey))
                    await _files.DeleteFileAsync(user.PhotoStorageKey);
                else if (!string.IsNullOrWhiteSpace(user.PhotoUrl))
                    await _files.DeleteFileAsync(user.PhotoUrl);

                // Save new file
                var newPhotoKey = await _files.SaveFileAsync(Photo, $"users/{user.Id}");
                user.PhotoStorageKey = newPhotoKey;
                user.PhotoUrl = null; // migrate away from old system
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

                // Delete old files
                if (teacher != null)
                {
                    if (!string.IsNullOrWhiteSpace(teacher.CVStorageKey))
                        await _files.DeleteFileAsync(teacher.CVStorageKey);
                    else if (!string.IsNullOrWhiteSpace(teacher.CVUrl))
                        await _files.DeleteFileAsync(teacher.CVUrl);
                }

                // Save new CV
                var newCvKey = await _files.SaveFileAsync(CVFile, $"users/{user.Id}/cv");

                if (teacher != null)
                {
                    teacher.CVStorageKey = newCvKey;
                    teacher.CVUrl = null;
                }
            }

            /* ---------------------------
               UPDATE BASIC USER FIELDS
            --------------------------- */
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
            await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber ?? "");
            await _userManager.UpdateAsync(user);

            /* ---------------------------
               TEACHER PROFILE
            --------------------------- */
            if (await _userManager.IsInRoleAsync(user, "Teacher"))
            {
                if (teacher == null)
                {
                    teacher = new Edu.Domain.Entities.Teacher
                    {
                        Id = user.Id,
                        Status = TeacherStatus.Pending
                    };
                    _db.Teachers.Add(teacher);
                }

                teacher.JobTitle = Input.JobTitle ?? "";
                teacher.ShortBio = Input.ShortBio;

                teacher.IntroVideoUrl = YouTubeHelper.ExtractYouTubeId(Input.IntroVideoUrl ?? "");
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
            await _db.SaveChangesAsync();
            await _signInManager.RefreshSignInAsync(user);

            TempData["ProfileSaved"] = _localizer["ProfileSaved"] ?? "Profile saved";
            return RedirectToAction("Index", "Home", new { area = "" });
        }
    }
}



