using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Web.Areas.Admin.ViewModels;
using Edu.Infrastructure.Helpers;
using Edu.Application.IServices;
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

        // GET: Admin/Lessons/Create?moduleId=5
        public IActionResult Create(int moduleId)
        {
            ViewData["ActivePage"] = "Curricula";
            var vm = new LessonCreateViewModel { ModuleId = moduleId, Order = 1 };
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LessonCreateViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var ytId = !string.IsNullOrWhiteSpace(vm.YouTubeUrl) ? YouTubeHelper.ExtractYouTubeId(vm.YouTubeUrl) : null;
            var lesson = new SchoolLesson
            {
                ModuleId = vm.ModuleId,
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
            // find curriculum id to redirect to details
            var module = await _db.SchoolModules.FindAsync(vm.ModuleId);
            return RedirectToAction("Details", "Curricula", new { area = "Admin", id = module?.CurriculumId ?? 0 });
        }

        public async Task<IActionResult> Edit(int id)
        {
            ViewData["ActivePage"] = "Curricula";
            var l = await _db.SchoolLessons.Include(x => x.Files).FirstOrDefaultAsync(x => x.Id == id);
            if (l == null) return NotFound();

            var vm = new LessonEditViewModel
            {
                Id = l.Id,
                ModuleId = l.ModuleId,
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
            if (!ModelState.IsValid) return View(vm);

            var l = await _db.SchoolLessons.FindAsync(vm.Id);
            if (l == null) return NotFound();

            l.Title = vm.Title;
            l.Description = vm.Description;
            l.YouTubeVideoId = !string.IsNullOrWhiteSpace(vm.YouTubeUrl) ? YouTubeHelper.ExtractYouTubeId(vm.YouTubeUrl) : null;
            l.VideoUrl = vm.YouTubeUrl;
            l.IsFree = vm.IsFree;
            l.Order = vm.Order;
            _db.SchoolLessons.Update(l);
            await _db.SaveChangesAsync();

            // redirect to curriculum details
            var module = await _db.SchoolModules.FindAsync(vm.ModuleId);
            return RedirectToAction("Details", "Curricula", new { area = "Admin", id = module?.CurriculumId ?? 0 });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, int curriculumId)
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
            return RedirectToAction("Details", "Curricula", new { area = "Admin", id = curriculumId });
        }
    }
}

