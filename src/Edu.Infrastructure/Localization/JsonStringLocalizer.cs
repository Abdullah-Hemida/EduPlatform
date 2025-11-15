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

    private IDictionary<string, string> GetAllStringsForCurrentCulture()
    {
        var culture = CultureInfo.CurrentUICulture.Name; // "en", "ar", "it"
        var cacheKey = $"__json_loc_{culture}";
        if (!_cache.TryGetValue(cacheKey, out Dictionary<string, string> dict))
        {
            dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var basePath = Path.Combine(_env.ContentRootPath, "Resources/i18n");
                var file = Path.Combine(basePath, $"{culture}.json");
                if (!File.Exists(file))
                {
                    // fallback: try neutral culture (first 2 letters)
                    file = Path.Combine(basePath, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName + ".json");
                }

                if (File.Exists(file))
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

