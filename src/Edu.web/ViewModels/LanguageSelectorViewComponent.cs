using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace Edu.Web.ViewComponents
{
    public class LanguageSelectorViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(string? returnUrl = null)
        {
            var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName ?? "en";
            var model = new LanguageSelectorModel
            {
                CurrentCulture = culture,
                ReturnUrl = returnUrl ?? (HttpContext.Request.Path + HttpContext.Request.QueryString)
            };
            return View(model);
        }
    }

    public class LanguageSelectorModel
    {
        public string CurrentCulture { get; set; } = "en";
        public string ReturnUrl { get; set; } = "/";
    }
}

