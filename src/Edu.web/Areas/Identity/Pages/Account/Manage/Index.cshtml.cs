using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.ComponentModel.DataAnnotations;

namespace Edu.Web.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _files;
        private readonly IStringLocalizer<IndexModel> _localizer;

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext db,
            IFileStorageService files,
            IStringLocalizer<IndexModel> localizer)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _db = db;
            _files = files;
            _localizer = localizer;
        }

        public string Username { get; set; } = null!;

        [TempData]
        public string? StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            public string FullName { get; set; } = string.Empty;

            [Phone]
            public string? PhoneNumber { get; set; }

            [DataType(DataType.Date)]
            public DateTime? DateOfBirth { get; set; }

            // Storage keys
            public string? PhotoStorageKey { get; set; }
            public string? CVStorageKey { get; set; }

            // Teacher-specific
            public string? JobTitle { get; set; }
            public string? ShortBio { get; set; }
            public string? IntroVideoUrl { get; set; }

            // Computed URLs for display
            public string? PhotoUrl { get; set; }
            public string? CVUrl { get; set; }
        }

        // GET: Load profile
        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            Username = user.UserName ?? user.Email ?? string.Empty;

            var phone = await _userManager.GetPhoneNumberAsync(user);

            Input = new InputModel
            {
                FullName = user.FullName,
                PhoneNumber = phone,
                DateOfBirth = user.DateOfBirth,
                PhotoStorageKey = user.PhotoStorageKey,
                PhotoUrl = await _files.GetPublicUrlAsync(user.PhotoStorageKey)
            };

            // Load teacher-specific data
            if (await _userManager.IsInRoleAsync(user, "Teacher"))
            {
                var teacher = await _db.Teachers.AsNoTracking().FirstOrDefaultAsync(t => t.Id == user.Id);
                if (teacher != null)
                {
                    Input.JobTitle = teacher.JobTitle;
                    Input.ShortBio = teacher.ShortBio;
                    Input.IntroVideoUrl = teacher.IntroVideoUrl;
                    Input.CVStorageKey = teacher.CVStorageKey;
                    Input.CVUrl = await _files.GetPublicUrlAsync(teacher.CVStorageKey);

                    ViewData["IntroVideoEmbedUrl"] = GetYouTubeEmbedUrl(teacher.IntroVideoUrl);
                }
            }

            return Page();
        }

        // POST: Update basic profile (photo included)
        public async Task<IActionResult> OnPostProfileAsync(IFormFile? photo)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            if (!ModelState.IsValid)
            {
                await OnGetAsync();
                return Page();
            }

            // Update basic fields
            user.FullName = Input.FullName;
            user.DateOfBirth = Input.DateOfBirth;

            var phone = await _userManager.GetPhoneNumberAsync(user);
            if (Input.PhoneNumber != phone)
            {
                var result = await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
                if (!result.Succeeded)
                {
                    ModelState.AddModelError(string.Empty, _localizer["UnexpectedErrorPhone"]);
                    await OnGetAsync();
                    return Page();
                }
            }

            // Photo upload
            if (photo != null)
            {
                if (!FileValidators.IsValidImage(photo))
                {
                    ModelState.AddModelError("photo", _localizer["InvalidImage"]);
                    await OnGetAsync();
                    return Page();
                }

                var key = await _files.SaveFileAsync(photo, $"users/{user.Id}");
                user.PhotoStorageKey = key;
                Input.PhotoStorageKey = key;
                Input.PhotoUrl = await _files.GetPublicUrlAsync(key);
            }

            await _userManager.UpdateAsync(user);
            await _signInManager.RefreshSignInAsync(user);

            StatusMessage = _localizer["ProfileUpdated"];
            return RedirectToAction("Index", "Home", new { area = "" });
        }

        // POST: Update teacher profile (CV included)
        public async Task<IActionResult> OnPostTeacherAsync(IFormFile? cvFile)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            if (!await _userManager.IsInRoleAsync(user, "Teacher"))
            {
                StatusMessage = _localizer["NotTeacher"];
                return RedirectToPage();
            }

            if (!ModelState.IsValid)
            {
                await OnGetAsync();
                return Page();
            }

            var teacher = await _db.Teachers.FirstOrDefaultAsync(t => t.Id == user.Id);
            if (teacher == null)
            {
                teacher = new Edu.Domain.Entities.Teacher
                {
                    Id = user.Id,
                    JobTitle = Input.JobTitle ?? string.Empty,
                    ShortBio = Input.ShortBio,
                    IntroVideoUrl = Input.IntroVideoUrl,
                    Status = TeacherStatus.Pending
                };
                _db.Teachers.Add(teacher);
            }
            else
            {
                teacher.JobTitle = Input.JobTitle ?? string.Empty;
                teacher.ShortBio = Input.ShortBio;
                teacher.IntroVideoUrl = Input.IntroVideoUrl;
            }

            // CV upload
            if (cvFile != null)
            {
                if (!FileValidators.IsValidDocument(cvFile))
                {
                    ModelState.AddModelError("cvFile", _localizer["InvalidDocument"]);
                    await OnGetAsync();
                    return Page();
                }

                var key = await _files.SaveFileAsync(cvFile, $"users/{user.Id}/cv");
                teacher.CVStorageKey = key;
                Input.CVStorageKey = key;
                Input.CVUrl = await _files.GetPublicUrlAsync(key);
            }

            await _db.SaveChangesAsync();
            StatusMessage = _localizer["TeacherProfileUpdated"];
            return RedirectToAction("Index", "Home", new { area = "" });
        }

        // YouTube helper
        private string? GetYouTubeEmbedUrl(string? url)
        {
            if (string.IsNullOrEmpty(url)) return null;

            if (url.Contains("youtube.com/embed/")) return url;

            if (url.Contains("youtube.com/watch") && url.Contains("v="))
            {
                var uri = new Uri(url);
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                var videoId = query["v"];
                if (!string.IsNullOrEmpty(videoId))
                    return $"https://www.youtube.com/embed/{videoId}";
            }

            if (url.Contains("youtu.be/"))
            {
                var videoId = url.Split('/').Last();
                if (videoId.Contains("?"))
                    videoId = videoId.Substring(0, videoId.IndexOf("?"));
                return $"https://www.youtube.com/embed/{videoId}";
            }

            return $"https://www.youtube.com/embed/{url}";
        }
    }
}


