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

        [TempData] public string? StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            //[Display(Name = "Full name")]
            //[Required, StringLength(200)]
            public string FullName { get; set; } = string.Empty;

            [Phone]
            [Display(Name = "Phone number")]
            public string? PhoneNumber { get; set; }

            [Display(Name = "Date of birth")]
            [DataType(DataType.Date)]
            public DateTime? DateOfBirth { get; set; }

            // Photo/CV handled via files in OnPostAsync

            // Teacher-specific
            [Display(Name = "Job Title")]
            [StringLength(150)]
            public string? JobTitle { get; set; }

            [Display(Name = "Short Bio")]
            [StringLength(2000)]
            public string? ShortBio { get; set; }

            [Display(Name = "Intro Video (YouTube URL or ID)")]
            public string? IntroVideoUrl { get; set; }

            // For display
            public string? PhotoUrl { get; set; }
            public string? CVUrl { get; set; }
        }

        // GET
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
                PhotoUrl = user.PhotoUrl
            };

            // load teacher data if teacher role
            if (await _userManager.IsInRoleAsync(user, "Teacher"))
            {
                var teacher = await _db.Teachers.AsNoTracking().FirstOrDefaultAsync(t => t.Id == user.Id);
                if (teacher != null)
                {
                    Input.JobTitle = teacher.JobTitle;
                    Input.ShortBio = teacher.ShortBio;
                    Input.IntroVideoUrl = teacher.IntroVideoUrl;
                    Input.CVUrl = teacher.CVUrl;

                    // Set the embed URL for display
                    ViewData["IntroVideoEmbedUrl"] = GetYouTubeEmbedUrl(teacher.IntroVideoUrl);
                }
            }

            return Page();
        }

        // POST: Update profile only
        public async Task<IActionResult> OnPostProfileAsync(IFormFile? photo)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            if (!ModelState.IsValid)
            {
                await OnGetAsync();
                return Page();
            }

            // Update basic profile info
            if (Input.FullName != user.FullName)
            {
                user.FullName = Input.FullName;
            }

            // Phone update
            var phone = await _userManager.GetPhoneNumberAsync(user);
            if (Input.PhoneNumber != phone)
            {
                var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
                if (!setPhoneResult.Succeeded)
                {
                    ModelState.AddModelError(string.Empty, _localizer["UnexpectedErrorPhone"]);
                    await OnGetAsync();
                    return Page();
                }
            }

            // DOB
            user.DateOfBirth = Input.DateOfBirth;

            // Photo upload
            if (photo != null)
            {
                if (!FileValidators.IsValidImage(photo))
                {
                    ModelState.AddModelError("photo", _localizer["InvalidImage"]);
                    await OnGetAsync();
                    return Page();
                }
                var photoUrl = await _files.SaveFileAsync(photo, $"users/{user.Id}");
                user.PhotoUrl = photoUrl;
                Input.PhotoUrl = photoUrl;
            }

            await _userManager.UpdateAsync(user);
            await _signInManager.RefreshSignInAsync(user);

            StatusMessage = _localizer["ProfileUpdated"];
            return RedirectToAction("Index", "Home", new { area = "" });
        }

        // POST: Update teacher profile only
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
                // Create if missing
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
                var cvUrl = await _files.SaveFileAsync(cvFile, $"users/{user.Id}/cv");
                teacher.CVUrl = cvUrl;
                Input.CVUrl = cvUrl;
            }

            await _db.SaveChangesAsync();
            StatusMessage = _localizer["TeacherProfileUpdated"];
            return RedirectToAction("Index", "Home", new { area = "" });
        }
        // Add this method to the IndexModel class
        private string? GetYouTubeEmbedUrl(string? url)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            // If it's already an embed URL, return as is
            if (url.Contains("youtube.com/embed/"))
                return url;

            // Handle standard YouTube URLs
            if (url.Contains("youtube.com/watch") && url.Contains("v="))
            {
                var uri = new Uri(url);
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                var videoId = query["v"];
                if (!string.IsNullOrEmpty(videoId))
                    return $"https://www.youtube.com/embed/{videoId}";
            }

            // Handle youtu.be shortened URLs
            if (url.Contains("youtu.be/"))
            {
                var videoId = url.Split('/').Last();
                if (videoId.Contains("?"))
                    videoId = videoId.Substring(0, videoId.IndexOf("?"));
                return $"https://www.youtube.com/embed/{videoId}";
            }

            // If it doesn't match standard patterns, assume it's a video ID
            return $"https://www.youtube.com/embed/{url}";
        }
    }
}

