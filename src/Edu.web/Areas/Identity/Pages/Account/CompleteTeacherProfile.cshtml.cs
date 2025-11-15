// File: Areas/Identity/Pages/Account/CompleteTeacherProfile.cshtml.cs
// ------------------------------------------------------------
using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
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
            public string? PhotoUrl { get; set; }

            // Teacher-specific
            public string? JobTitle { get; set; }
            public string? ShortBio { get; set; }
            public string? IntroVideoUrl { get; set; }
            public string? CVUrl { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("CompleteTeacherProfile.OnGet: user is null. Claims: {Claims}",
                    string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}")));
                return RedirectToPage("/Account/Login", new { area = "Identity", returnUrl = Url.Page("/Account/CompleteTeacherProfile") });
            }

            Input.FullName = user.FullName;
            Input.PhoneNumber = await _userManager.GetPhoneNumberAsync(user);
            Input.DateOfBirth = user.DateOfBirth;
            Input.PhotoUrl = user.PhotoUrl;

            if (await _userManager.IsInRoleAsync(user, "Teacher"))
            {
                var teacher = await _db.Teachers.FindAsync(user.Id);
                if (teacher != null)
                {
                    Input.JobTitle = teacher.JobTitle;
                    Input.ShortBio = teacher.ShortBio;
                    // store video id or url for display
                    Input.IntroVideoUrl = teacher.IntroVideoUrl;
                    Input.CVUrl = teacher.CVUrl;
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("CompleteTeacherProfile.OnPost: user is null. Claims: {Claims}",
                    string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}")));
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            if (!ModelState.IsValid)
            {
                await OnGetAsync();
                return Page();
            }

            // phone validation (optional stronger validation)
            if (!string.IsNullOrWhiteSpace(Input.PhoneNumber))
            {
                // Optional: additional validation logic here
            }

            // Photo
            if (Photo != null)
            {
                if (!FileValidators.IsValidImage(Photo))
                {
                    ModelState.AddModelError("Photo", _localizer["InvalidImage"]);
                    await OnGetAsync();
                    return Page();
                }
                try
                {
                    Input.PhotoUrl = await _files.SaveFileAsync(Photo, $"users/{user.Id}");
                    user.PhotoUrl = Input.PhotoUrl;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving teacher photo for user {UserId}", user.Id);
                    ModelState.AddModelError("", _localizer["FileSaveError"] ?? "Failed to save photo");
                    await OnGetAsync();
                    return Page();
                }
            }

            // CV upload
            if (CVFile != null)
            {
                if (!FileValidators.IsValidDocument(CVFile))
                {
                    ModelState.AddModelError("CVFile", _localizer["InvalidDocument"]);
                    await OnGetAsync();
                    return Page();
                }
                try
                {
                    Input.CVUrl = await _files.SaveFileAsync(CVFile, $"users/{user.Id}/cv");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving CV file for user {UserId}", user.Id);
                    ModelState.AddModelError("", _localizer["FileSaveError"] ?? "Failed to save document");
                    await OnGetAsync();
                    return Page();
                }
            }

            // update core user fields
            user.FullName = Input.FullName;
            user.DateOfBirth = Input.DateOfBirth;

            var phoneRes = await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber ?? string.Empty);
            if (!phoneRes.Succeeded)
            {
                _logger.LogWarning("Failed to set phone number for user {UserId}: {Errors}", user.Id, string.Join(";", phoneRes.Errors.Select(e => e.Description)));
            }

            var updUserRes = await _userManager.UpdateAsync(user);
            if (!updUserRes.Succeeded)
            {
                _logger.LogError("Failed to update user {UserId}: {Errors}", user.Id, string.Join(";", updUserRes.Errors.Select(e => e.Description)));
                ModelState.AddModelError("", _localizer["ProfileUpdateFailed"] ?? "Failed to update profile");
                await OnGetAsync();
                return Page();
            }

            // teacher record
            if (await _userManager.IsInRoleAsync(user, "Teacher"))
            {
                var teacher = await _db.Teachers.FindAsync(user.Id);
                var introVideoId = YouTubeHelper.ExtractYouTubeId(Input.IntroVideoUrl ?? string.Empty);

                if (teacher == null)
                {
                    teacher = new Edu.Domain.Entities.Teacher
                    {
                        Id = user.Id,
                        JobTitle = Input.JobTitle ?? string.Empty,
                        ShortBio = Input.ShortBio,
                        IntroVideoUrl = introVideoId,
                        CVUrl = Input.CVUrl,
                        Status = TeacherStatus.Pending
                    };
                    _db.Teachers.Add(teacher);
                }
                else
                {
                    teacher.JobTitle = Input.JobTitle ?? string.Empty;
                    teacher.ShortBio = Input.ShortBio;
                    teacher.IntroVideoUrl = introVideoId;
                    if (!string.IsNullOrEmpty(Input.CVUrl)) teacher.CVUrl = Input.CVUrl;
                    _db.Teachers.Update(teacher);
                }
            }

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving teacher record for user {UserId}", user.Id);
                ModelState.AddModelError("", _localizer["ProfileSaveFailed"] ?? "Failed to save profile details");
                await OnGetAsync();
                return Page();
            }

            // Refresh sign-in so updated profile info & claims appear in cookie
            try
            {
                await _signInManager.RefreshSignInAsync(user);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh sign-in for user {UserId}", user.Id);
            }

            TempData["ProfileSaved"] = _localizer["ProfileSaved"] ?? "Profile saved";

            return RedirectToAction("Index", "Home", new { area = "" });
        }
    }
}



