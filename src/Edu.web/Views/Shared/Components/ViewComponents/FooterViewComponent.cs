using Edu.Infrastructure.Data;
using Edu.Web.Resources;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Edu.Web.Views.Shared.Components.ViewComponents
{
    public class FooterViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _db;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly ILogger<FooterViewComponent> _logger;

        public FooterViewComponent(
            ApplicationDbContext db,
            IStringLocalizer<SharedResource> localizer,
            ILogger<FooterViewComponent> logger)
        {
            _db = db;
            _localizer = localizer;
            _logger = logger;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            // Prefer request-localization UI culture if available
            var requestCultureFeature = HttpContext.Features.Get<IRequestCultureFeature>();
            var uiCulture = requestCultureFeature?.RequestCulture?.UICulture ?? CultureInfo.CurrentUICulture;
            var culture = (uiCulture?.TwoLetterISOLanguageName ?? "ar").ToLowerInvariant();

            _logger.LogInformation("FooterViewComponent invoked. Request UI culture: {Culture}", uiCulture?.Name ?? "unknown");

            // 1) try requested culture
            var contacts = await _db.FooterContacts
                .AsNoTracking()
                .Where(c => c.Culture != null && c.Culture.ToLower() == culture)
                .OrderBy(c => c.Order)
                .ToListAsync();

            // 2) try english explicitly (useful if your app expects en fallback)
            if (!contacts.Any() && !string.Equals(culture, "ar", StringComparison.OrdinalIgnoreCase))
            {
                contacts = await _db.FooterContacts
                    .AsNoTracking()
                    .Where(c => c.Culture != null && c.Culture.ToLower() == "ar")
                    .OrderBy(c => c.Order)
                    .ToListAsync();
            }

            // 3) FINAL fallback: if still none, return *any* available contacts (DB contains only one language)
            if (!contacts.Any())
            {
                contacts = await _db.FooterContacts
                    .AsNoTracking()
                    .OrderBy(c => c.Order)
                    .ToListAsync();

                if (contacts.Any())
                {
                    var dbCultures = string.Join(", ", contacts.Select(c => c.Culture).Distinct());
                    _logger.LogWarning("No footer contacts for {Requested}. Falling back to available DB cultures: {DbCultures}", culture, dbCultures);
                }
                else
                {
                    _logger.LogWarning("No footer contacts found at all in DB.");
                }
            }
            else
            {
                _logger.LogInformation("Loaded {N} footer contact(s) for culture '{Culture}'", contacts.Count, culture);
            }

            // social links are global in your current model; keep as-is
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


