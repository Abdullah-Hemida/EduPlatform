using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Web.Areas.Admin.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Edu.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class LessonsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _fs;

        public LessonsController(ApplicationDbContext db, IFileStorageService fs)
        {
            _db = db;
            _fs = fs;
        }

        // GET: Admin/Lessons/Create?moduleId=5 or ?curriculumId=3
        public async Task<IActionResult> Create(int? moduleId, int? curriculumId)
        {
            ViewData["ActivePage"] = "Curricula";

            var vm = new LessonCreateViewModel
            {
                ModuleId = moduleId,
                CurriculumId = curriculumId ?? 0,
                Order = 1
            };

            // If a moduleId is provided, infer curriculumId from module
            if (moduleId.HasValue)
            {
                var module = await _db.SchoolModules
                                      .AsNoTracking()
                                      .FirstOrDefaultAsync(m => m.Id == moduleId.Value);
                if (module == null) return NotFound();
                vm.CurriculumId = module.CurriculumId;
            }

            if (vm.CurriculumId == 0)
            {
                // no curriculum known — cannot create a lesson without curriculum
                return BadRequest("CurriculumId or ModuleId must be provided.");
            }

            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LessonCreateViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            // if lesson added without module → ModuleId must be null
            if (vm.ModuleId == 0)
                vm.ModuleId = null;

            var ytId = !string.IsNullOrWhiteSpace(vm.YouTubeUrl)
                ? YouTubeHelper.ExtractYouTubeId(vm.YouTubeUrl)
                : null;

            var lesson = new SchoolLesson
            {
                ModuleId = vm.ModuleId,
                CurriculumId = vm.CurriculumId,  // new
                Title = vm.Title,
                Description = vm.Description,
                YouTubeVideoId = ytId,
                VideoUrl = vm.YouTubeUrl,
                IsFree = vm.IsFree,
                Order = vm.Order
            };

            _db.SchoolLessons.Add(lesson);
            await _db.SaveChangesAsync();

            if (vm.Files != null && vm.Files.Any())
            {
                foreach (var f in vm.Files)
                {
                    if (f == null || f.Length == 0) continue;
                    var url = await _fs.SaveFileAsync(f, "lesson-files");
                    var fr = new FileResource
                    {
                        SchoolLessonId = lesson.Id,
                        FileUrl = url,
                        Name = Path.GetFileName(f.FileName),
                        FileType = f.ContentType
                    };
                    _db.FileResources.Add(fr);
                }
                await _db.SaveChangesAsync();
            }

            TempData["Success"] = "Lesson added.";

            // redirect to curriculum details (use vm.CurriculumId which is guaranteed to be set)
            return RedirectToAction("Details", "Curricula", new { area = "Admin", id = vm.CurriculumId });
        }

        // GET: Admin/Lessons/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            ViewData["ActivePage"] = "Curricula";
            var l = await _db.SchoolLessons
                             .Include(x => x.Files)
                             .AsNoTracking()
                             .FirstOrDefaultAsync(x => x.Id == id);
            if (l == null) return NotFound();

            var vm = new LessonEditViewModel
            {
                Id = l.Id,
                ModuleId = l.ModuleId,
                CurriculumId = l.CurriculumId,
                Title = l.Title,
                Description = l.Description,
                YouTubeUrl = l.VideoUrl,
                IsFree = l.IsFree,
                Order = l.Order
            };
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(LessonEditViewModel vm)
        {
            ViewData["ActivePage"] = "Curricula";

            if (!ModelState.IsValid) return View(vm);

            var l = await _db.SchoolLessons.FindAsync(vm.Id);
            if (l == null) return NotFound();

            // If ModuleId changed and is provided, ensure module belongs to same curriculum or update CurriculumId accordingly.
            if (vm.ModuleId.HasValue)
            {
                var module = await _db.SchoolModules.AsNoTracking().FirstOrDefaultAsync(m => m.Id == vm.ModuleId.Value);
                if (module == null)
                {
                    ModelState.AddModelError(nameof(vm.ModuleId), "Module not found.");
                    return View(vm);
                }
                l.ModuleId = vm.ModuleId;
                l.CurriculumId = module.CurriculumId; // ensure lesson's curriculum matches module
            }
            else
            {
                // If module removed, keep CurriculumId as provided by vm (must be valid)
                if (vm.CurriculumId == 0)
                {
                    ModelState.AddModelError(nameof(vm.CurriculumId), "Curriculum is required when removing module.");
                    return View(vm);
                }
                l.ModuleId = null;
                l.CurriculumId = vm.CurriculumId;
            }

            l.Title = vm.Title;
            l.Description = vm.Description;
            l.YouTubeVideoId = !string.IsNullOrWhiteSpace(vm.YouTubeUrl) ? YouTubeHelper.ExtractYouTubeId(vm.YouTubeUrl) : null;
            l.VideoUrl = vm.YouTubeUrl;
            l.IsFree = vm.IsFree;
            l.Order = vm.Order;

            _db.SchoolLessons.Update(l);
            await _db.SaveChangesAsync();

            // redirect to curriculum details
            return RedirectToAction("Details", "Curricula", new { area = "Admin", id = l.CurriculumId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, int? curriculumId)
        {
            var l = await _db.SchoolLessons.FindAsync(id);
            if (l == null) return NotFound();

            var files = await _db.FileResources.Where(fr => fr.SchoolLessonId == id).ToListAsync();
            foreach (var f in files)
            {
                await _fs.DeleteFileAsync(f.FileUrl);
                _db.FileResources.Remove(f);
            }

            _db.SchoolLessons.Remove(l);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Lesson deleted.";

            // prefer passed curriculumId; otherwise infer from deleted lesson
            var redirectId = curriculumId ?? l.CurriculumId;
            return RedirectToAction("Details", "Curricula", new { area = "Admin", id = redirectId });
        }
    }
}


