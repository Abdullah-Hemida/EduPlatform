// File: Edu.Infrastructure.Localization/StringLocalizerExtensions.cs
using System.Globalization;
using Microsoft.Extensions.Localization;

namespace Edu.Infrastructure.Localization
{
    public static class StringLocalizerExtensions
    {
        /// <summary>
        /// Small helper that scopes CurrentUICulture and returns the same localizer instance for chaining.
        /// This is intentionally simple: it sets the CurrentUICulture then returns the localizer so callers
        /// can perform localized[key] calls.
        /// </summary>
        public static IStringLocalizer WithCulture(this IStringLocalizer loc, CultureInfo culture)
        {
            if (culture != null)
            {
                CultureInfo.CurrentUICulture = culture;
                CultureInfo.CurrentCulture = culture; // optional: align both
            }
            return loc;
        }
    }
}

