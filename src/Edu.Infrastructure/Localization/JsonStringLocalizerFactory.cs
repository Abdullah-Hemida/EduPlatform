using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Edu.Infrastructure.Localization;
public class JsonStringLocalizerFactory : IStringLocalizerFactory
{
    private readonly IMemoryCache _cache;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<JsonStringLocalizerFactory> _logger;

    public JsonStringLocalizerFactory(IMemoryCache cache, IWebHostEnvironment env, ILogger<JsonStringLocalizerFactory> logger)
    {
        _cache = cache;
        _env = env;
        _logger = logger;
    }

    public IStringLocalizer Create(Type resourceSource) => Create(null, null);

    public IStringLocalizer Create(string baseName, string location)
    {
        return new JsonStringLocalizer(_cache, _env, _logger);
    }
}
