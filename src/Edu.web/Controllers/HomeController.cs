using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Web.Views.Shared.Components.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorageService _files;
    private readonly IHeroService _heroService;

    public HomeController(ApplicationDbContext db, IFileStorageService files, IHeroService heroService)
    {
        _db = db;
        _files = files;
        _heroService = heroService;
    }

    public async Task<IActionResult> Index()
    {
        var culture = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName ?? "en";
        var hero = await _heroService.GetHeroAsync(HeroPlacement.Home);
        // counts
        var vm = new Edu.Web.ViewModels.HomeVm
        {
            TotalStudents = await _db.Students.CountAsync(),
            TotalTeachers = await _db.Teachers.CountAsync(),
            TotalPrivateCourses = await _db.PrivateCourses.CountAsync(c => c.IsPublished),
            TotalReactiveCourses = await _db.ReactiveCourses.CountAsync(),
            TotalCurricula = await _db.Curricula.CountAsync(),
            Hero = hero
        };

        // Load sections and translate per culture
        var sections = await _db.HomeSections
            .Include(s => s.Translations)
            .Include(s => s.Items).ThenInclude(i => i.Translations)
            .OrderBy(s => s.Order)
            .AsNoTracking()
            .ToListAsync();

        vm.Sections = new List<Edu.Web.ViewModels.HomeSectionVm>();

        foreach (var s in sections)
        {
            var ts = s.Translations.FirstOrDefault(t => t.Culture == culture) ?? s.Translations.FirstOrDefault()!;
            var sectionVm = new Edu.Web.ViewModels.HomeSectionVm
            {
                Id = s.Id,
                Title = ts?.Title,
                Subtitle = ts?.Subtitle,
                ImagePublicUrl = string.IsNullOrEmpty(s.ImageStorageKey) ? null : await _files.GetPublicUrlAsync(s.ImageStorageKey),
                Items = new List<Edu.Web.ViewModels.HomeSectionItemVm>()
            };

            foreach (var it in s.Items.OrderBy(i => i.Order))
            {
                var itTr = it.Translations.FirstOrDefault(tt => tt.Culture == culture) ?? it.Translations.FirstOrDefault();
                var itemVm = new Edu.Web.ViewModels.HomeSectionItemVm
                {
                    Id = it.Id,
                    Text = itTr?.Text ?? it.Text,
                    LinkUrl = it.LinkUrl,
                    ImagePublicUrl = string.IsNullOrEmpty(it.ImageStorageKey) ? null : await _files.GetPublicUrlAsync(it.ImageStorageKey)
                };
                sectionVm.Items.Add(itemVm);
            }

            vm.Sections.Add(sectionVm);
        }

        return View(vm);
    }
}






