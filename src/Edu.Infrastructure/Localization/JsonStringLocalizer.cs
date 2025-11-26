using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;

namespace Edu.Infrastructure.Localization;

public class JsonStringLocalizer : IStringLocalizer
{
    private readonly IMemoryCache _cache;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger _logger;

    public JsonStringLocalizer(IMemoryCache cache, IWebHostEnvironment env, ILogger logger)
    {
        _cache = cache;
        _env = env;
        _logger = logger;
    }

    // Build a merged dictionary for the CURRENT UI culture, using fallbacks.
    // Order: parent (two-letter) -> specific (en-US) -> always ensure "en" fallback.
    private IDictionary<string, string> GetAllStringsForCurrentCulture()
    {
        // Use two-letter code as canonical cache key (since your files are en.json/ar.json/it.json)
        var twoLetter = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName?.ToLowerInvariant() ?? "en";
        var cacheKey = $"__json_loc_{twoLetter}";

        if (!_cache.TryGetValue(cacheKey, out Dictionary<string, string> dict))
        {
            dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var basePath = Path.Combine(_env.ContentRootPath, "Resources", "i18n");

                // Try exact name first (e.g. "en-US.json"), then two-letter (e.g. "en.json")
                var tried = new List<string>
            {
                CultureInfo.CurrentUICulture.Name,                    // e.g. "en-US"
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName // e.g. "en"
            }.Distinct().ToList();

                string? file = null;
                foreach (var name in tried)
                {
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    var candidate = Path.Combine(basePath, $"{name}.json");
                    if (File.Exists(candidate))
                    {
                        file = candidate;
                        break;
                    }
                }

                // As a final fallback try "en.json"
                if (file == null)
                {
                    var candidate = Path.Combine(basePath, "en.json");
                    if (File.Exists(candidate))
                        file = candidate;
                }

                if (!string.IsNullOrEmpty(file))
                {
                    var text = File.ReadAllText(file);
                    var doc = JsonSerializer.Deserialize<Dictionary<string, string>>(text);
                    if (doc != null)
                        dict = new Dictionary<string, string>(doc, StringComparer.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading JSON localization file");
            }

            // cache using the two-letter code key
            _cache.Set(cacheKey, dict, TimeSpan.FromMinutes(30));
        }

        return dict;
    }


    public LocalizedString this[string name]
    {
        get
        {
            var dict = GetAllStringsForCurrentCulture();
            var found = dict.TryGetValue(name, out var value);
            return new LocalizedString(name, found ? value : name, !found);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var format = this[name].Value;
            var value = string.Format(format, arguments);
            return new LocalizedString(name, value, false);
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        var dict = GetAllStringsForCurrentCulture();
        foreach (var kv in dict)
            yield return new LocalizedString(kv.Key, kv.Value, false);
    }

    public IStringLocalizer WithCulture(CultureInfo culture) => this;
}
