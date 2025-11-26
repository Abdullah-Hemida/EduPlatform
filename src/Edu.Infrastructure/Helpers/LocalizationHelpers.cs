
using Edu.Domain.Entities;
using System.Globalization;

namespace Edu.Infrastructure.Helpers
{
    public static class LocalizationHelpers
    {
        // Returns the best available name based on current UI culture.
        public static string GetLocalizedLevelName(Level level)
        {
            if (level == null) return string.Empty;
            var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName?.ToLowerInvariant();

            // prefer explicit match, then fallbacks
            if (lang == "ar" && !string.IsNullOrWhiteSpace(level.NameAr)) return level.NameAr;
            if (lang == "it" && !string.IsNullOrWhiteSpace(level.NameIt)) return level.NameIt;
            if ((lang == "en" || string.IsNullOrWhiteSpace(lang)) && !string.IsNullOrWhiteSpace(level.NameEn)) return level.NameEn;

            // fallback chain: en -> it -> ar -> any non-empty
            if (!string.IsNullOrWhiteSpace(level.NameEn)) return level.NameEn;
            if (!string.IsNullOrWhiteSpace(level.NameIt)) return level.NameIt;
            if (!string.IsNullOrWhiteSpace(level.NameAr)) return level.NameAr;

            return string.Empty;
        }
    }
}
