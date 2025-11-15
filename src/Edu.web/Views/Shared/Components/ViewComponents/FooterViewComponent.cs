using Edu.Infrastructure.Data;
using Edu.Web.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Edu.Web.Views.Shared.Components.ViewComponents
{
    public class FooterViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _db;
        private readonly IStringLocalizer<SharedResource> _localizer;
        public FooterViewComponent(ApplicationDbContext db, IStringLocalizer<SharedResource> localizer)
        {
            _db = db;
            _localizer = localizer;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var culture = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName ?? "en";

            var contacts = await _db.FooterContacts
                .AsNoTracking()
                .Where(c => c.Culture == culture)
                .OrderBy(c => c.Order)
                .ToListAsync();

            // fallback: if no contacts for current culture, try "en"
            if (!contacts.Any() && culture != "en")
            {
                contacts = await _db.FooterContacts
                    .AsNoTracking()
                    .Where(c => c.Culture == "en")
                    .OrderBy(c => c.Order)
                    .ToListAsync();
            }

            var social = await _db.SocialLinks
                .AsNoTracking()
                .Where(s => s.IsVisible)
                .OrderBy(s => s.Order)
                .ToListAsync();

            var copyright =
                string.IsNullOrWhiteSpace(_localizer["Footer.Copyright"].Value)
                    ? $"© {DateTime.UtcNow.Year} - {_localizer["Site.Title"].Value}"
                    : _localizer["Footer.Copyright"].Value;

            var vm = new FooterVm
            {
                Contacts = contacts,
                SocialLinks = social,
                Copyright = copyright
            };


            return View(vm);
        }
    }

    // Footer view model used by the viewcomponent
    public class FooterVm
    {
        public List<Edu.Domain.Entities.FooterContact> Contacts { get; set; } = new();
        public List<Edu.Domain.Entities.SocialLink> SocialLinks { get; set; } = new();
        public string? Copyright { get; set; }
    }
}

