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
        private readonly ILogger<LessonsController> _logger;

        public LessonsController(ApplicationDbContext db, IFileStorageService fs, ILogger<LessonsController> logger)
        {
            _db = db;
            _fs = fs;
            _logger = logger;
        }

        // GET: Admin/Lessons/Create?moduleId=5 or ?curriculumId=3
        public async Task<IActionResult> Create(int? moduleId, int? curriculumId, CancellationToken cancellationToken = default)
        {
            ViewData["ActivePage"] = "Curricula";

            var vm = new LessonCreateViewModel
            {
                ModuleId = moduleId,
                CurriculumId = curriculumId ?? 0,
                Order = 1
            };

            // If a moduleId is provided, infer curriculumId from module (fetch only the needed field)
            if (moduleId.HasValue)
            {
                var moduleCurriculum = await _db.SchoolModules
                    .AsNoTracking()
                    .Where(m => m.Id == moduleId.Value)
                    .Select(m => new { m.Id, m.CurriculumId })
                    .FirstOrDefaultAsync(cancellationToken);

                if (moduleCurriculum == null) return NotFound();
                vm.CurriculumId = moduleCurriculum.CurriculumId;
            }

            if (vm.CurriculumId == 0)
            {
                // no curriculum known — cannot create a lesson without curriculum
                return BadRequest("CurriculumId or ModuleId must be provided.");
            }

            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LessonCreateViewModel vm, CancellationToken cancellationToken = default)
        {
            ViewData["ActivePage"] = "Curricula";

            if (!ModelState.IsValid) return View(vm);

            // if lesson added without module → ModuleId must be null
            if (vm.ModuleId.HasValue && vm.ModuleId.Value == 0) vm.ModuleId = null;

            // Validate module if provided (ensure it exists and belongs to given curriculum)
            if (vm.ModuleId.HasValue)
            {
                var module = await _db.SchoolModules
                    .AsNoTracking()
                    .Where(m => m.Id == vm.ModuleId.Value)
                    .Select(m => new { m.Id, m.CurriculumId })
                    .FirstOrDefaultAsync(cancellationToken);

                if (module == null)
                {
                    ModelState.AddModelError(nameof(vm.ModuleId), "Module not found.");
                    return View(vm);
                }

                // optional: ensure module.CurriculumId == vm.CurriculumId
                vm.CurriculumId = module.CurriculumId;
            }

            var ytId = !string.IsNullOrWhiteSpace(vm.YouTubeUrl)
                ? YouTubeHelper.ExtractYouTubeId(vm.YouTubeUrl)
                : null;

            var lesson = new SchoolLesson
            {
                ModuleId = vm.ModuleId,
                CurriculumId = vm.CurriculumId,  // guaranteed to be set
                Title = vm.Title,
                Description = vm.Description,
                YouTubeVideoId = ytId,
                VideoUrl = vm.YouTubeUrl,
                Order = vm.Order
            };

            _db.SchoolLessons.Add(lesson);
            await _db.SaveChangesAsync(cancellationToken); // materialize lesson.Id

            // Upload files (if any) and insert FileResource rows in a batch
            if (vm.Files != null && vm.Files.Any())
            {
                var toAdd = new List<FileResource>(vm.Files.Count);

                foreach (var f in vm.Files)
                {
                    if (f == null || f.Length == 0) continue;
                    try
                    {
                        // SaveFileAsync may be IO bound and can take time; we run sequentially to avoid parallel upload storms
                        var urlOrKey = await _fs.SaveFileAsync(f, "lesson-files");
                        var fr = new FileResource
                        {
                            SchoolLessonId = lesson.Id,
                            FileUrl = urlOrKey,
                            Name = Path.GetFileName(f.FileName),
                            FileType = f.ContentType
                        };
                        toAdd.Add(fr);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to save uploaded file for lesson {LessonId}. File: {FileName}", lesson.Id, f?.FileName);
                        // continue with other files; do not fail the whole request
                    }
                }

                if (toAdd.Count > 0)
                {
                    _db.FileResources.AddRange(toAdd);
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }

            TempData["Success"] = "Lesson.Added";
            // redirect to curriculum details (use vm.CurriculumId which is guaranteed to be set)
            return RedirectToAction("Details", "Curricula", new { area = "Admin", id = vm.CurriculumId });
        }

        // GET: Admin/Lessons/Edit/5
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken = default)
        {
            ViewData["ActivePage"] = "Curricula";

            var l = await _db.SchoolLessons
                             .AsNoTracking()
                             .Include(x => x.Files)
                             .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (l == null) return NotFound();

            var vm = new LessonEditViewModel
            {
                Id = l.Id,
                ModuleId = l.ModuleId,
                CurriculumId = l.CurriculumId,
                Title = l.Title,
                Description = l.Description,
                YouTubeUrl = l.VideoUrl,
                Order = l.Order,
                ExistingFiles = l.Files?.ToList() ?? new List<FileResource>()
            };

            // used by Cancel link in your view
            ViewBag.CurriculumId = l.CurriculumId;

            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(LessonEditViewModel vm, CancellationToken cancellationToken = default)
        {
            ViewData["ActivePage"] = "Curricula";

            if (!ModelState.IsValid)
            {
                // repopulate ExistingFiles for redisplay if modelstate invalid
                var existing = await _db.FileResources
                                        .AsNoTracking()
                                        .Where(f => f.SchoolLessonId == vm.Id)
                                        .ToListAsync(cancellationToken);
                vm.ExistingFiles = existing;
                return View(vm);
            }

            var l = await _db.SchoolLessons.FindAsync(new object[] { vm.Id }, cancellationToken);
            if (l == null) return NotFound();

            // Module handling (same as your existing code)
            if (vm.ModuleId.HasValue)
            {
                var module = await _db.SchoolModules
                    .AsNoTracking()
                    .Where(m => m.Id == vm.ModuleId.Value)
                    .Select(m => new { m.Id, m.CurriculumId })
                    .FirstOrDefaultAsync(cancellationToken);

                if (module == null)
                {
                    ModelState.AddModelError(nameof(vm.ModuleId), "Module not found.");
                    // repopulate ExistingFiles before returning
                    vm.ExistingFiles = await _db.FileResources
                                               .AsNoTracking()
                                               .Where(f => f.SchoolLessonId == vm.Id)
                                               .ToListAsync(cancellationToken);
                    return View(vm);
                }

                l.ModuleId = vm.ModuleId;
                l.CurriculumId = module.CurriculumId;
            }
            else
            {
                if (vm.CurriculumId == 0)
                {
                    ModelState.AddModelError(nameof(vm.CurriculumId), "Curriculum is required when removing module.");
                    vm.ExistingFiles = await _db.FileResources
                                               .AsNoTracking()
                                               .Where(f => f.SchoolLessonId == vm.Id)
                                               .ToListAsync(cancellationToken);
                    return View(vm);
                }
                l.ModuleId = null;
                l.CurriculumId = vm.CurriculumId;
            }

            l.Title = vm.Title;
            l.Description = vm.Description;
            l.YouTubeVideoId = !string.IsNullOrWhiteSpace(vm.YouTubeUrl) ? YouTubeHelper.ExtractYouTubeId(vm.YouTubeUrl) : null;
            l.VideoUrl = vm.YouTubeUrl;
            l.Order = vm.Order;

            _db.SchoolLessons.Update(l);
            await _db.SaveChangesAsync(cancellationToken);

            // --- NEW: Upload any newly attached files and save FileResource rows ---
            if (vm.Files != null && vm.Files.Any())
            {
                var toAdd = new List<FileResource>(vm.Files.Length);

                foreach (var f in vm.Files)
                {
                    if (f == null || f.Length == 0) continue;

                    try
                    {
                        // SaveFileAsync returns a key or url depending on implementation
                        var urlOrKey = await _fs.SaveFileAsync(f, "lesson-files");
                        var fr = new FileResource
                        {
                            SchoolLessonId = l.Id,
                            FileUrl = urlOrKey,
                            Name = Path.GetFileName(f.FileName),
                            FileType = f.ContentType
                        };
                        toAdd.Add(fr);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to save uploaded file for lesson {LessonId}. File: {FileName}", l.Id, f?.FileName);
                        // continue with other files; do not fail the whole request
                    }
                }

                if (toAdd.Count > 0)
                {
                    _db.FileResources.AddRange(toAdd);
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }

            // redirect to curriculum details
            return RedirectToAction("Details", "Curricula", new { area = "Admin", id = l.CurriculumId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, int? curriculumId, CancellationToken cancellationToken = default)
        {
            var l = await _db.SchoolLessons.FindAsync(new object[] { id }, cancellationToken);
            if (l == null) return NotFound();

            // Load file resources associated with this lesson
            var files = await _db.FileResources
                                 .Where(fr => fr.SchoolLessonId == id)
                                 .ToListAsync(cancellationToken);

            // Delete files from storage and remove DB rows
            if (files.Count > 0)
            {
                foreach (var f in files)
                {
                    try
                    {
                        // prefer StorageKey if present
                        var keyOrUrl = !string.IsNullOrWhiteSpace(f.StorageKey) ? f.StorageKey : f.FileUrl;
                        if (!string.IsNullOrWhiteSpace(keyOrUrl))
                        {
                            await _fs.DeleteFileAsync(keyOrUrl);
                        }
                    }
                    catch (Exception ex)
                    {
                        // log and continue - we still remove DB record
                        _logger.LogWarning(ex, "Failed to delete file from storage for FileResource {FileResourceId}", f.Id);
                    }

                    _db.FileResources.Remove(f);
                }
            }

            _db.SchoolLessons.Remove(l);
            await _db.SaveChangesAsync(cancellationToken);

            TempData["Success"] = "Lesson.Deleted";

            // prefer passed curriculumId; otherwise infer from deleted lesson
            var redirectId = curriculumId ?? l.CurriculumId;
            return RedirectToAction("Details", "Curricula", new { area = "Admin", id = redirectId });
        }
    }
}


