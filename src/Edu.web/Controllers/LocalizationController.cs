using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace Edu.Web.Controllers
{
    public class LocalizationController : Controller
    {
        // GET version - simple and reliable (no antiforgery)
        [HttpGet]
        public IActionResult SetLanguage(string culture, string? returnUrl = "/")
        {
            if (string.IsNullOrEmpty(culture)) culture = "en";
            var cookieValue = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture));
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                cookieValue,
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    HttpOnly = false,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Path = "/"
                });
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }

        // POST version - keep for compatibility if you want to submit forms (with antiforgery)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetLanguagePost(string culture, string? returnUrl)
        {
            if (string.IsNullOrEmpty(culture)) culture = "en";

            var cookieValue = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture));
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                cookieValue,
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    HttpOnly = false,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Path = "/"
                });

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }
    }
}

