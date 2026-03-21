using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Infrastructure.Localization;
using Edu.Infrastructure.Services;
using Edu.Web.Areas.Admin.ViewModels;
using Edu.Web.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Edu.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ManageUsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<ManageUsersController> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IEmailSender _emailSender;
        private readonly IFileStorageService _fileService;
        private readonly IUserCultureProvider _userCultureProvider;

        public ManageUsersController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db,
            ILogger<ManageUsersController> logger,
            IWebHostEnvironment env,
            IStringLocalizer<SharedResource> localizer,
            IEmailSender emailSender,
            IFileStorageService fileStorage,
            IUserCultureProvider userCultureProvider)
        {
            _userManager = userManager;
            _db = db;
            _logger = logger;
            _env = env;
            _localizer = localizer;
            _emailSender = emailSender;
            _fileService = fileStorage;
            _userCultureProvider = userCultureProvider;
        }

        // GET: Admin/ManageUsers
        public async Task<IActionResult> Index(
            string role = "All",
            bool showDeleted = false,
            string? search = null,
            int pageIndex = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            if (pageIndex < 1) pageIndex = 1;

            // 1) global totals
            var total = await _db.Users.AsNoTracking().CountAsync(cancellationToken);

            // 2) roles + counts (single query)
            var rolesWithCounts = await (from r in _db.Roles.AsNoTracking()
                                         join ur in _db.Set<IdentityUserRole<string>>().AsNoTracking()
                                             on r.Id equals ur.RoleId into g
                                         select new
                                         {
                                             r.Id,
                                             r.Name,
                                             Count = g.Count()
                                         }).ToListAsync(cancellationToken);

            var roleMap = rolesWithCounts.ToDictionary(x => x.Name, x => x.Id, StringComparer.OrdinalIgnoreCase);
            int adminCount = rolesWithCounts.FirstOrDefault(rw => rw.Name.Equals("Admin", StringComparison.OrdinalIgnoreCase))?.Count ?? 0;
            int teacherCount = rolesWithCounts.FirstOrDefault(rw => rw.Name.Equals("Teacher", StringComparison.OrdinalIgnoreCase))?.Count ?? 0;
            int studentCount = rolesWithCounts.FirstOrDefault(rw => rw.Name.Equals("Student", StringComparison.OrdinalIgnoreCase))?.Count ?? 0;

            // 3) base users projection (min fields)
            IQueryable<ApplicationUser> baseQuery = _db.Users.AsNoTracking()
                .Select(u => new ApplicationUser
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    UserName = u.UserName,
                    Email = u.Email,
                    PhotoStorageKey = u.PhotoStorageKey,
                    PhoneNumber = u.PhoneNumber,
                    IsDeleted = u.IsDeleted
                });

            // 4) soft-delete filter
            baseQuery = showDeleted ? baseQuery.Where(u => u.IsDeleted) : baseQuery.Where(u => !u.IsDeleted);

            // 5) role filter if needed
            if (!string.IsNullOrEmpty(role) && !role.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                var roleEntity = rolesWithCounts.FirstOrDefault(rw => rw.Name.Equals(role, StringComparison.OrdinalIgnoreCase));
                if (roleEntity == null)
                {
                    var emptyVm = new UserIndexViewModel
                    {
                        TotalCount = total,
                        AdminCount = adminCount,
                        TeacherCount = teacherCount,
                        StudentCount = studentCount,
                        CurrentRoleFilter = role,
                        ShowDeleted = showDeleted,
                        Search = search,
                        PageIndex = pageIndex,
                        PageSize = pageSize,
                        Items = new PaginatedList<UserRowViewModel>(new List<UserRowViewModel>(), 0, pageIndex, pageSize)
                    };
                    ViewData["ActivePage"] = "Users";
                    ViewData["Title"] = _localizer["Admin.Users"].Value ?? "Users";
                    return View(emptyVm);
                }

                var roleId = roleEntity.Id;
                var userIdsInRoleQuery = _db.Set<IdentityUserRole<string>>()
                                             .AsNoTracking()
                                             .Where(ur => ur.RoleId == roleId)
                                             .Select(ur => ur.UserId);

                baseQuery = baseQuery.Where(u => userIdsInRoleQuery.Contains(u.Id));
            }

            // 6) search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                baseQuery = baseQuery.Where(u =>
                    (u.FullName ?? "").Contains(s) ||
                    (u.UserName ?? "").Contains(s) ||
                    (u.PhoneNumber ?? "").Contains(s));
            }

            // 7) total filtered
            var totalFiltered = await baseQuery.CountAsync(cancellationToken);

            // 8) page results
            var pageUsers = await baseQuery
                .OrderBy(u => u.FullName)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var ids = pageUsers.Select(u => u.Id).ToList();

            // 9) roles for page users
            var rolesMap = await (from ur in _db.Set<IdentityUserRole<string>>().AsNoTracking()
                                  join r in _db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                                  where ids.Contains(ur.UserId)
                                  select new { ur.UserId, r.Name })
                                  .ToListAsync(cancellationToken);

            var rolesByUser = rolesMap
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.Name)));

            // 10) student flags
            var studentFlags = await _db.Students
                .AsNoTracking()
                .Where(s => ids.Contains(s.Id))
                .Select(s => new { s.Id, s.IsAllowed })
                .ToListAsync(cancellationToken);

            var studentAllowedByUser = studentFlags.ToDictionary(s => s.Id, s => s.IsAllowed);

            // 11) teacher statuses
            var teachers = await _db.Teachers
                .AsNoTracking()
                .Where(t => ids.Contains(t.Id))
                .Select(t => new { t.Id, t.Status })
                .ToListAsync(cancellationToken);

            var teacherStatusByUser = teachers.ToDictionary(t => t.Id, t => t.Status.ToString());

            // 12) resolve photo URLs concurrently (external I/O)
            var urlTasks = pageUsers.Select(u => _fileService.GetPublicUrlAsync(u.PhotoStorageKey)).ToArray();
            string?[] urls;
            try
            {
                urls = await Task.WhenAll(urlTasks);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve one or more user photo URLs; continuing with nulls.");
                // map results for successful tasks, else null
                urls = pageUsers.Select(u => (string?)null).ToArray();
            }

            // 13) build rows
            var rowsList = pageUsers.Select((u, idx) => new UserRowViewModel
            {
                Id = u.Id,
                FullName = u.FullName ?? u.UserName ?? string.Empty,
                Email = u.Email ?? string.Empty,
                PhotoUrl = urls.ElementAtOrDefault(idx),
                PhoneNumber = u.PhoneNumber,
                Roles = rolesByUser.TryGetValue(u.Id, out var r) ? r : string.Empty,
                TeacherStatus = teacherStatusByUser.TryGetValue(u.Id, out var ts) ? ts : string.Empty,
                IsAllowed = studentAllowedByUser.TryGetValue(u.Id, out var allowed) ? allowed : (bool?)null
            }).ToList();

            var paged = new PaginatedList<UserRowViewModel>(rowsList, totalFiltered, pageIndex, pageSize);

            var vm = new UserIndexViewModel
            {
                TotalCount = total,
                AdminCount = adminCount,
                TeacherCount = teacherCount,
                StudentCount = studentCount,
                CurrentRoleFilter = role,
                ShowDeleted = showDeleted,
                Search = search,
                PageIndex = pageIndex,
                PageSize = pageSize,
                Items = paged
            };

            ViewData["ActivePage"] = "Users";
            ViewData["Title"] = _localizer["Admin.Users"].Value ?? "Users";
            return View(vm);
        }

        // GET: Admin/ManageUsers/Details/{id}
        public async Task<IActionResult> Details(string id, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            var teacher = await _db.Teachers.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
            var student = await _db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

            // compute default region from admin UI culture (fallback to IT)
            string defaultRegion = "IT";
            try
            {
                var regionInfo = new RegionInfo(CultureInfo.CurrentUICulture.Name);
                if (!string.IsNullOrEmpty(regionInfo.TwoLetterISORegionName))
                    defaultRegion = regionInfo.TwoLetterISORegionName;
            }
            catch { defaultRegion = "IT"; }

            var vm = new UserDetailsViewModel
            {
                Id = user.Id,
                FullName = user.FullName ?? user.UserName ?? "",
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                GuardianPhoneNumber = student?.GuardianPhoneNumber,
                DateOfBirth = user.DateOfBirth,
                PhotoStorageKey = user.PhotoStorageKey,
                Roles = roles,
                Teacher = teacher,
                Student = student,
                StudentIsAllowed = student?.IsAllowed
            };

            if (!string.IsNullOrEmpty(user.PhotoStorageKey))
            {
                try
                {
                    vm.PhotoUrl = await _fileService.GetPublicUrlAsync(user.PhotoStorageKey);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve photo URL for user {UserId}", id);
                    vm.PhotoUrl = null;
                }
            }
            else vm.PhotoUrl = null;

            vm.PhoneWhatsapp = PhoneHelpers.ToWhatsappDigits(vm.PhoneNumber, defaultRegion);
            vm.GuardianWhatsapp = PhoneHelpers.ToWhatsappDigits(vm.GuardianPhoneNumber, defaultRegion);

            ViewBag.WhatsAppLabel = _localizer["WhatsApp"].Value ?? "WhatsApp";

            // --- load curricula (lightweight projection) ---
            var curriculaEntities = await _db.Curricula
                .AsNoTracking()
                .Include(c => c.Level)
                .OrderBy(c => c.Level!.Order)
                .ThenBy(c => c.Order)
                .Select(c => new { c.Id, c.Title, Level = c.Level == null ? null : new { c.Level.Id, c.Level.NameEn, c.Level.NameIt, c.Level.NameAr } })
                .ToListAsync(cancellationToken);

            var displayCulture = CultureInfo.CurrentUICulture;

            static string GetLevelDisplayNameDynamic(dynamic? level, CultureInfo culture)
            {
                if (level == null) return string.Empty;
                var two = (culture?.TwoLetterISOLanguageName ?? "it").ToLowerInvariant();
                return two switch
                {
                    "ar" => string.IsNullOrWhiteSpace((string)level.NameAr) ? (string)level.NameEn ?? (string)level.NameIt ?? "" : (string)level.NameAr,
                    "it" => string.IsNullOrWhiteSpace((string)level.NameIt) ? (string)level.NameEn ?? (string)level.NameAr ?? "" : (string)level.NameIt,
                    _ => string.IsNullOrWhiteSpace((string)level.NameEn) ? (string)level.NameIt ?? (string)level.NameAr ?? "" : (string)level.NameEn
                };
            }

            vm.Curricula = curriculaEntities.Select(c => new CurriculumCheckboxVm
            {
                Id = c.Id,
                Title = c.Title,
                LevelName = GetLevelDisplayNameDynamic(c.Level, displayCulture)
            }).ToList();

            if (student != null)
            {
                var assignedIds = await _db.StudentCurricula
                    .AsNoTracking()
                    .Where(sc => sc.StudentId == student.Id)
                    .Select(sc => sc.CurriculumId)
                    .ToListAsync(cancellationToken);

                vm.SelectedCurriculumIds = assignedIds;
            }
            else vm.SelectedCurriculumIds = new List<int>();

            ViewData["Title"] = _localizer["Admin.UserDetails"].Value ?? "User details";
            ViewData["ActivePage"] = "Users";
            return View(vm);
        }

        // POST: Approve teacher
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveTeacher(string id, string? returnUrl = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["Error"] = "InvalidId";
                return RedirectToLocal(returnUrl);
            }

            var teacher = await _db.Teachers.FindAsync(new object[] { id }, cancellationToken);
            if (teacher == null)
            {
                TempData["Error"] = "TeacherNotFound";
                return RedirectToLocal(returnUrl);
            }

            teacher.Status = TeacherStatus.Approved;
            _db.Teachers.Update(teacher);
            await _db.SaveChangesAsync(cancellationToken);

            var user = await _userManager.FindByIdAsync(id);
            if (user != null && !string.IsNullOrEmpty(user.Email))
            {
                _ = SendLocalizedEmailToUserAsync(
                    user,
                    "Teacher.Email.Approved.Subject",
                    "Teacher.Email.Approved.Body",
                    user.FullName ?? user.UserName ?? "");
            }

            TempData["Success"] = "Teacher.Approved";
            return RedirectToLocal(returnUrl);
        }

        // POST: Unapprove teacher
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnapproveTeacher(string id, string? returnUrl = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["Error"] = "InvalidId";
                return RedirectToLocal(returnUrl);
            }

            var teacher = await _db.Teachers.FindAsync(new object[] { id }, cancellationToken);
            if (teacher == null)
            {
                TempData["Error"] = "TeacherNotFound";
                return RedirectToLocal(returnUrl);
            }

            teacher.Status = TeacherStatus.Rejected;
            _db.Teachers.Update(teacher);
            await _db.SaveChangesAsync(cancellationToken);

            var user = await _userManager.FindByIdAsync(id);
            if (user != null && !string.IsNullOrEmpty(user.Email))
            {
                _ = SendLocalizedEmailToUserAsync(
                    user,
                    "Teacher.Email.Rejected.Subject",
                    "Teacher.Email.Rejected.Body",
                    user.FullName ?? user.UserName ?? "");
            }

            TempData["Success"] = "Teacher.Rejected";
            return RedirectToLocal(returnUrl);
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Index));
        }

        private async Task SendLocalizedEmailToUserAsync(ApplicationUser recipient, string subjectKey, string bodyKey, params object[] args)
        {
            if (recipient == null || string.IsNullOrEmpty(recipient.Email)) return;

            try
            {
                var culture = _userCultureProvider.GetCulture(recipient) ?? CultureInfo.CurrentUICulture;
                var localized = _localizer.WithCulture(culture);

                var subjLocalized = localized[subjectKey];
                var subj = subjLocalized.ResourceNotFound ? subjectKey : subjLocalized.Value;

                string body;
                if (args != null && args.Length > 0)
                {
                    var bodyLocalized = localized[bodyKey, args];
                    body = bodyLocalized.ResourceNotFound ? string.Format(bodyKey, args) : bodyLocalized.Value;
                }
                else
                {
                    var bodyLocalized = localized[bodyKey];
                    body = bodyLocalized.ResourceNotFound ? bodyKey : bodyLocalized.Value;
                }

                await _emailSender.SendEmailAsync(recipient.Email, subj, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed sending localized email to user {UserId} ({Email}) using keys {SubjKey}/{BodyKey}", recipient.Id, recipient.Email, subjectKey, bodyKey);
            }
        }

        // POST: Soft delete (toggle)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SoftDelete(string id, string? returnUrl = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["Error"] = "InvalidId";
                return RedirectToLocal(returnUrl);
            }

            var currentUserId = _userManager.GetUserId(User);
            if (id == currentUserId)
            {
                TempData["Error"] = "CannotDeleteSelf";
                return RedirectToLocal(returnUrl);
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["Error"] = "UserNotFound";
                return RedirectToLocal(returnUrl);
            }

            user.IsDeleted = !user.IsDeleted;
            var res = await _userManager.UpdateAsync(user);
            if (!res.Succeeded)
            {
                var err = string.Join("; ", res.Errors.Select(e => e.Description));
                _logger.LogWarning("SoftDelete failed for {Id}: {Err}", id, err);
                TempData["Error"] = "UserUpdateFailed";
                return RedirectToLocal(returnUrl);
            }

            TempData["Success"] = user.IsDeleted ? "User.Suspended" : "User.Restored";
            return RedirectToLocal(returnUrl);
        }

        // POST: Hard delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HardDelete(string id, string? returnUrl = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["Error"] = "InvalidId";
                return RedirectToLocal(returnUrl);
            }

            var currentUserId = _userManager.GetUserId(User);
            if (id == currentUserId)
            {
                TempData["Error"] = "CannotDeleteSelf";
                return RedirectToLocal(returnUrl);
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["Error"] = "UserNotFound";
                return RedirectToLocal(returnUrl);
            }

            var hasBookings = await _db.Bookings.AsNoTracking().AnyAsync(b => b.TeacherId == id, cancellationToken);
            if (hasBookings)
            {
                TempData["Error"] = "CannotHardDeleteHasBookings";
                return RedirectToLocal(returnUrl);
            }

            var res = await _userManager.DeleteAsync(user);
            if (!res.Succeeded)
            {
                var err = string.Join("; ", res.Errors.Select(e => e.Description));
                _logger.LogWarning("HardDelete failed for {Id}: {Err}", id, err);
                TempData["Error"] = "UserDeleteFailed";
                return RedirectToLocal(returnUrl);
            }

            TempData["Success"] = "User.PermanentlyDeleted";
            return RedirectToLocal(returnUrl);
        }

        // GET: DownloadCV
        public async Task<IActionResult> DownloadCV(string id, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var teacher = await _db.Teachers
                .Include(t => t.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

            if (teacher == null || string.IsNullOrEmpty(teacher.CVUrl))
                return NotFound();

            var filePath = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, teacher.CVUrl.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
                return NotFound();

            var ext = Path.GetExtension(filePath);
            var safeName = Path.GetFileNameWithoutExtension(teacher.User?.FullName ?? id);
            var fileName = $"{safeName}_CV{ext}";
            var contentType = "application/pdf";
            return PhysicalFile(filePath, contentType, fileName);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ToggleStudentAllowedAjax(string id, bool setTo, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(id)) return BadRequest(new { success = false, message = _localizer["InvalidId"].Value ?? "Invalid id." });

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound(new { success = false, message = _localizer["UserNotFound"].Value ?? "User not found." });

            var student = await _db.Students.FindAsync(new object[] { id }, cancellationToken);
            if (student == null)
            {
                student = new Domain.Entities.Student { Id = id, IsAllowed = setTo };
                _db.Students.Add(student);
            }
            else
            {
                student.IsAllowed = setTo;
                _db.Students.Update(student);
            }

            await _db.SaveChangesAsync(cancellationToken);

            var text = setTo ? (_localizer["Admin.Allowed"].Value ?? "Allowed") : (_localizer["Admin.NotAllowed"].Value ?? "Not allowed");
            return Json(new { success = true, isAllowed = setTo, message = text });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignCurricula(string id, [FromForm] int[] selectedCurriculumIds, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(id)) return BadRequest();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
            if (student == null)
            {
                student = new Domain.Entities.Student { Id = id, IsAllowed = false };
                _db.Students.Add(student);
                await _db.SaveChangesAsync(cancellationToken);
            }

            var newSet = (selectedCurriculumIds ?? Array.Empty<int>()).Distinct().ToArray();

            var existing = await _db.StudentCurricula
                .Where(sc => sc.StudentId == id)
                .Select(sc => sc.CurriculumId)
                .ToListAsync(cancellationToken);

            var toAdd = newSet.Except(existing).ToList();
            var toRemove = existing.Except(newSet).ToList();

            if (toAdd.Any())
            {
                var ents = toAdd.Select(cid => new StudentCurriculum { StudentId = id, CurriculumId = cid });
                await _db.StudentCurricula.AddRangeAsync(ents, cancellationToken);
            }

            if (toRemove.Any())
            {
                var removeEntities = await _db.StudentCurricula
                    .Where(sc => sc.StudentId == id && toRemove.Contains(sc.CurriculumId))
                    .ToListAsync(cancellationToken);

                _db.StudentCurricula.RemoveRange(removeEntities);
            }

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                TempData["Success"] = "Admin.SaveSuccess";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed updating curricula assignments for student {StudentId}", id);
                TempData["Error"] = "Admin.OperationFailed";
            }

            return RedirectToAction(nameof(Details), new { id });
        }
    }
}

