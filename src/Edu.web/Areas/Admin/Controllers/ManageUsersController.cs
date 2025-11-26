using Azure.Core;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Web.Areas.Admin.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Edu.Web.Resources;
using Edu.Infrastructure.Services;

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

        public ManageUsersController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db,
            ILogger<ManageUsersController> logger,
            IWebHostEnvironment env, IStringLocalizer<SharedResource> localizer, IEmailSender emailSender)
        {
            _userManager = userManager;
            _db = db;
            _logger = logger;
            _env = env;
            _localizer = localizer;
            _emailSender = emailSender;
        }

        // GET: Admin/ManageUsers
        // Server-rendered listing with filters, search and pagination
        public async Task<IActionResult> Index(
            string role = "All",
            bool showDeleted = false,
            string? search = null,
            int pageIndex = 1,
            int pageSize = 20)
        {
            // compute counts
            var total = await _userManager.Users.CountAsync();

            var roles = await _db.Roles.AsNoTracking().ToListAsync();
            var userRoles = _db.Set<IdentityUserRole<string>>().AsNoTracking();

            int adminCount = 0, teacherCount = 0, studentCount = 0;
            var roleMap = roles.ToDictionary(r => r.Name, r => r.Id, StringComparer.OrdinalIgnoreCase);

            if (roleMap.TryGetValue("Admin", out var adminRoleId))
                adminCount = await userRoles.CountAsync(ur => ur.RoleId == adminRoleId);

            if (roleMap.TryGetValue("Teacher", out var teacherRoleId))
                teacherCount = await userRoles.CountAsync(ur => ur.RoleId == teacherRoleId);

            if (roleMap.TryGetValue("Student", out var studentRoleId))
                studentCount = await userRoles.CountAsync(ur => ur.RoleId == studentRoleId);

            // base query
            IQueryable<ApplicationUser> query = _userManager.Users.AsNoTracking();

            // filter by deletion state
            if (showDeleted)
                query = query.Where(u => u.IsDeleted);
            else
                query = query.Where(u => !u.IsDeleted);

            // role filter (if provided and not All)
            if (!string.IsNullOrEmpty(role) && !role.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                var roleEntity = await _db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Name == role);
                if (roleEntity != null)
                {
                    var userIdsInRole = _db.Set<IdentityUserRole<string>>()
                                           .Where(ur => ur.RoleId == roleEntity.Id)
                                           .Select(ur => ur.UserId);
                    query = query.Where(u => userIdsInRole.Contains(u.Id));
                }
                else
                {
                    // role not found -> return empty page
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
                    ViewData["Title"] = "Users";
                    return View(emptyVm);
                }
            }

            // search fields — FullName, UserName, PhoneNumber
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(u =>
                    (u.FullName ?? "").Contains(search) ||
                    (u.UserName ?? "").Contains(search) ||
                    ((u.PhoneNumber ?? "").Contains(search))
                );
            }

            var ordered = query.OrderBy(u => u.FullName);
            var totalFiltered = await ordered.CountAsync();

            // apply paging
            if (pageIndex < 1) pageIndex = 1;
            var page = await ordered.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();
            var ids = page.Select(u => u.Id).ToList();

            // roles for page users
            var rolesMap = await (from ur in _db.Set<IdentityUserRole<string>>()
                                  join r in _db.Roles on ur.RoleId equals r.Id
                                  where ids.Contains(ur.UserId)
                                  select new { ur.UserId, r.Name })
                                  .ToListAsync();

            var rolesByUser = rolesMap.GroupBy(x => x.UserId).ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.Name)));
            // after you fetched ids for page
            var studentFlags = await _db.Students
                .Where(s => ids.Contains(s.Id))
                .Select(s => new { s.Id, s.IsAllowed })
                .ToListAsync();

            var studentAllowedByUser = studentFlags.ToDictionary(s => s.Id, s => s.IsAllowed);
            // teacher status if exists
            var teachers = await _db.Teachers
                                    .Where(t => ids.Contains(t.Id))
                                    .Select(t => new { t.Id, t.Status })
                                    .ToListAsync();
            var teacherStatusByUser = teachers.ToDictionary(t => t.Id, t => t.Status.ToString());

            var rows = page.Select(u => new UserRowViewModel
            {
                Id = u.Id,
                FullName = u.FullName ?? u.UserName ?? "",
                Email = u.Email ?? "",
                PhotoUrl = u.PhotoUrl,
                PhoneNumber = u.PhoneNumber,
                Roles = rolesByUser.TryGetValue(u.Id, out var r) ? r : string.Empty,
                TeacherStatus = teacherStatusByUser.TryGetValue(u.Id, out var ts) ? ts : string.Empty,
                IsAllowed = studentAllowedByUser.TryGetValue(u.Id, out var allowed) ? allowed : (bool?)null
            }).ToList();


            var paged = new PaginatedList<UserRowViewModel>(rows, totalFiltered, pageIndex, pageSize);

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

            // pass any TempData messages to view through ViewData (view reads TempData directly is fine too)
            ViewData["ActivePage"] = "Users";
            ViewData["Title"] = "Users";
            return View(vm);
        }

        

        // GET: Admin/ManageUsers/Details/{id}
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _userManager.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(await _userManager.FindByIdAsync(id));
            var teacher = await _db.Teachers.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
            var student = await _db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);

            var vm = new UserDetailsViewModel
            {
                Id = user.Id,
                FullName = user.FullName ?? user.UserName ?? "",
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                DateOfBirth = user.DateOfBirth,
                PhotoUrl = user.PhotoUrl,
                Roles = roles,
                Teacher = teacher,
                Student = student,
                StudentIsAllowed = student?.IsAllowed
            };

            ViewData["Title"] = "User details";
            ViewData["ActivePage"] = "Users";
            return View(vm);
        }

        // POST (redirecting): Approve teacher (server flow)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveTeacher(string id, string? returnUrl = null)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["Error"] = _localizer["InvalidId"].Value;
                return RedirectToLocal(returnUrl);
            }

            var teacher = await _db.Teachers.FindAsync(id);
            if (teacher == null)
            {
                TempData["Error"] = _localizer["TeacherNotFound"].Value;
                return RedirectToLocal(returnUrl);
            }

            teacher.Status = TeacherStatus.Approved;
            _db.Teachers.Update(teacher);
            await _db.SaveChangesAsync();

            // Get the user
            var user = await _userManager.FindByIdAsync(id);

            // Send Approval Email
            if (user != null)
            {
                await _emailSender.SendEmailAsync(
                    user.Email!,
                    "Your Teacher Account Is Approved",
                    $"Dear {user.FullName},<br/><br/>" +
                    $"Your teacher profile has been approved. You can now access your dashboard.<br/><br/>" +
                    $"Best regards,<br/>Edu Platform Team"
                );
            }

            TempData["Success"] = _localizer["TeacherApproved"].Value;
            return RedirectToLocal(returnUrl);
        }


        // POST (redirecting): Unapprove teacher (server flow)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnapproveTeacher(string id, string? returnUrl = null)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["Error"] = _localizer["InvalidId"].Value;
                return RedirectToLocal(returnUrl);
            }

            var teacher = await _db.Teachers.FindAsync(id);
            if (teacher == null)
            {
                TempData["Error"] = _localizer["TeacherNotFound"].Value;
                return RedirectToLocal(returnUrl);
            }

            teacher.Status = TeacherStatus.Rejected;
            _db.Teachers.Update(teacher);
            await _db.SaveChangesAsync();

            var user = await _userManager.FindByIdAsync(id);

            if (user != null)
            {
                await _emailSender.SendEmailAsync(
                    user.Email!,
                    "Your Teacher Application Was Rejected",
                    $"Dear {user.FullName},<br/><br/>" +
                    $"Your application was not approved. Please review your information and try again.<br/><br/>" +
                    $"Best regards,<br/>Edu Platform Team"
                );
            }

            TempData["Success"] = _localizer["TeacherRejected"].Value;
            return RedirectToLocal(returnUrl);
        }

        // POST (redirecting): Soft delete (toggle) — server flow friendly
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SoftDelete(string id, string? returnUrl = null)
        {
            if (string.IsNullOrEmpty(id)) { TempData["Error"] = "Invalid id."; return RedirectToLocal(returnUrl); }

            // Prevent admin from deleting themselves
            var currentUserId = _userManager.GetUserId(User);
            if (id == currentUserId)
            {
                TempData["Error"] = "You cannot delete your own account.";
                return RedirectToLocal(returnUrl);
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) { TempData["Error"] = "User not found."; return RedirectToLocal(returnUrl); }

            user.IsDeleted = !user.IsDeleted;
            var res = await _userManager.UpdateAsync(user);
            if (!res.Succeeded)
            {
                var err = string.Join("; ", res.Errors.Select(e => e.Description));
                _logger.LogWarning("SoftDelete failed for {Id}: {Err}", id, err);
                TempData["Error"] = "Failed to update user: " + err;
                return RedirectToLocal(returnUrl);
            }

            TempData["Success"] = user.IsDeleted ? "User suspended." : "User restored.";
            return RedirectToLocal(returnUrl);
        }

        // POST (redirecting): Hard delete (server flow friendly)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HardDelete(string id, string? returnUrl = null)
        {
            if (string.IsNullOrEmpty(id)) { TempData["Error"] = "Invalid id."; return RedirectToLocal(returnUrl); }

            var currentUserId = _userManager.GetUserId(User);
            if (id == currentUserId)
            {
                TempData["Error"] = "You cannot delete your own account.";
                return RedirectToLocal(returnUrl);
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) { TempData["Error"] = "User not found."; return RedirectToLocal(returnUrl); }

            // Example constraint check (you had bookings check before)
            var hasBookings = await _db.Bookings.AnyAsync(b => b.TeacherId == id);
            if (hasBookings)
            {
                TempData["Error"] = "Cannot hard-delete: user has related bookings.";
                return RedirectToLocal(returnUrl);
            }

            var res = await _userManager.DeleteAsync(user);
            if (!res.Succeeded)
            {
                var err = string.Join("; ", res.Errors.Select(e => e.Description));
                _logger.LogWarning("HardDelete failed for {Id}: {Err}", id, err);
                TempData["Error"] = "Delete failed: " + err;
                return RedirectToLocal(returnUrl);
            }

            TempData["Success"] = "User permanently deleted.";
            return RedirectToLocal(returnUrl);
        }

        // GET: Admin/ManageUsers/DownloadCV/{id}
        public async Task<IActionResult> DownloadCV(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var teacher = await _db.Teachers
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (teacher == null || string.IsNullOrEmpty(teacher.CVUrl))
                return NotFound();

            var filePath = Path.Combine(_env.WebRootPath, teacher.CVUrl.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
                return NotFound();

            var contentType = "application/pdf"; // or detect by extension
            var fileName = $"{teacher.User!.FullName}_CV{Path.GetExtension(filePath)}";

            return PhysicalFile(filePath, contentType, fileName);
        }

        // Helper to redirect to provided returnUrl or Index
        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Index));
        }
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ToggleStudentAllowedAjax(string id, bool setTo)
        {
            if (string.IsNullOrEmpty(id))
                return BadRequest(new { success = false, message = "Invalid id." });

            // Find user (optional check)
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound(new { success = false, message = "User not found." });

            // Find or create student record
            var student = await _db.Students.FindAsync(id);
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

            await _db.SaveChangesAsync();

            var text = setTo ? _localizer["Admin.Allowed"].Value ?? "Allowed" : _localizer["Admin.NotAllowed"].Value ?? "Not allowed";

            return Json(new
            {
                success = true,
                isAllowed = setTo,
                message = text
            });
        }

    }
}
