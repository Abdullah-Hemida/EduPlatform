
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Edu.Infrastructure.Data;
using Edu.Domain.Entities;
using Edu.Web.Areas.Teacher.ViewModels; // ModuleCreateVm / ModuleEditVm

namespace Edu.Web.Areas.Teacher.Controllers
{
    [Area("Teacher")]
    [Authorize(Roles = "Teacher")]
    public class ModulesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public ModulesController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // GET: Teacher/Modules/Create?privateCourseId=#
        public async Task<IActionResult> Create(int privateCourseId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var course = await _db.PrivateCourses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == privateCourseId);
            if (course == null) return NotFound();
            if (course.TeacherId != user.Id) return Forbid();

            var vm = new ModuleCreateVm { PrivateCourseId = privateCourseId, Order = 1 };
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ModuleCreateVm vm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var course = await _db.PrivateCourses.FirstOrDefaultAsync(c => c.Id == vm.PrivateCourseId);
            if (course == null) return NotFound();
            if (course.TeacherId != user.Id) return Forbid();

            if (!ModelState.IsValid) return View(vm);

            var module = new PrivateModule
            {
                PrivateCourseId = vm.PrivateCourseId,
                Title = vm.Title,
                Order = vm.Order
            };
            _db.PrivateModules.Add(module);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Module added.";
            return RedirectToAction("Details", "PrivateCourses", new { area = "Teacher", id = vm.PrivateCourseId });
        }

        // GET: Teacher/Modules/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var module = await _db.PrivateModules
                                  .AsNoTracking()
                                  .Include(m => m.PrivateCourse)
                                  .FirstOrDefaultAsync(m => m.Id == id);

            if (module == null) return NotFound();
            if (module.PrivateCourse?.TeacherId != user.Id) return Forbid();

            var vm = new ModuleEditVm { Id = module.Id, PrivateCourseId = module.PrivateCourseId, Title = module.Title, Order = module.Order };
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ModuleEditVm vm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var module = await _db.PrivateModules.Include(m => m.PrivateCourse).FirstOrDefaultAsync(m => m.Id == vm.Id);
            if (module == null) return NotFound();
            if (module.PrivateCourse?.TeacherId != user.Id) return Forbid();

            if (!ModelState.IsValid) return View(vm);

            module.Title = vm.Title;
            module.Order = vm.Order;
            await _db.SaveChangesAsync();

            TempData["Success"] = "Module updated.";
            return RedirectToAction("Details", "Courses", new { area = "Teacher", id = vm.PrivateCourseId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, int courseId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var module = await _db.PrivateModules.Include(m => m.PrivateCourse).FirstOrDefaultAsync(m => m.Id == id);
            if (module == null) return NotFound();
            if (module.PrivateCourse?.TeacherId != user.Id) return Forbid();

            _db.PrivateModules.Remove(module);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Module deleted.";
            return RedirectToAction("Details", "PrivateCourses", new { area = "Teacher", id = courseId });
        }
    }
}


