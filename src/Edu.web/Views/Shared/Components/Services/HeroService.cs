using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Web.Resources;
using Edu.Web.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using System.Globalization;

namespace Edu.Web.Views.Shared.Components.Services
{
    public interface IHeroService
    {
        Task<HeroVm?> GetHeroAsync(HeroPlacement placement);
        Task InvalidateCacheAsync(HeroPlacement placement);
    }

    public class HeroService : IHeroService
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorageService _files;
        private readonly IMemoryCache _cache;

        private const string CACHE_PREFIX = "Hero_"; // final key: Hero_{placement}_{culture}
        // Known cultures to invalidate; extend if you add more locales
        private static readonly string[] KnownCultures = new[] { "en", "it", "ar" };

        public HeroService(ApplicationDbContext db, IFileStorageService files, IMemoryCache cache)
        {
            _db = db;
            _files = files;
            _cache = cache;
        }

        public async Task<HeroVm?> GetHeroAsync(HeroPlacement placement)
        {
            var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName ?? "en";
            var cacheKey = $"{CACHE_PREFIX}{placement}_{culture}";

            if (_cache.TryGetValue<HeroVm?>(cacheKey, out var cached))
                return cached;

            // Load the active hero for placement (first by order). No translations here; select row then localize below.
            var ent = await _db.HeroSections
                .AsNoTracking()
                .Where(h => h.Placement == placement && h.IsActive)
                .OrderBy(h => h.Order)
                .FirstOrDefaultAsync();

            if (ent == null)
            {
                // Cache a null for a short time to avoid repeated DB hits if none exist.
                _cache.Set<HeroVm?>(cacheKey, null, GetShortCacheOptions());
                return null;
            }

            // Choose localized title/description with fallbacks (ar -> it -> en etc.)
            string pick(string en, string it, string ar)
            {
                if (culture == "ar")
                    return !string.IsNullOrWhiteSpace(ar) ? ar : (!string.IsNullOrWhiteSpace(en) ? en : it);
                if (culture == "it")
                    return !string.IsNullOrWhiteSpace(it) ? it : (!string.IsNullOrWhiteSpace(en) ? en : ar);
                // default english
                return !string.IsNullOrWhiteSpace(en) ? en : (!string.IsNullOrWhiteSpace(it) ? it : ar);
            }

            var title = pick(ent.TitleEn, ent.TitleIt, ent.TitleAr);
            var desc = pick(ent.DescriptionEn, ent.DescriptionIt, ent.DescriptionAr);

            string? publicUrl = null;
            if (!string.IsNullOrEmpty(ent.ImageStorageKey))
            {
                // This may call storage (blob/local) — caching the whole hero avoids repeating this frequently.
                publicUrl = await _files.GetPublicUrlAsync(ent.ImageStorageKey);
            }

            var vm = new HeroVm
            {
                Id = ent.Id,
                Placement = ent.Placement,
                ImagePublicUrl = publicUrl,
                Title = title,
                Description = desc
            };

            // Store in cache with *short* TTL (Option B)
            _cache.Set(cacheKey, vm, GetShortCacheOptions());
            return vm;
        }

        public Task InvalidateCacheAsync(HeroPlacement placement)
        {
            // Remove all known culture keys for this placement.
            foreach (var c in KnownCultures)
            {
                var key = $"{CACHE_PREFIX}{placement}_{c}";
                _cache.Remove(key);
            }
            // also remove possible empty/other keys defensively
            _cache.Remove($"{CACHE_PREFIX}{placement}_");
            return Task.CompletedTask;
        }

        private MemoryCacheEntryOptions GetShortCacheOptions()
        {
            // Short absolute expiration plus short sliding expiration.
            // Adjust numbers if you want faster or slower refresh.
            return new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(2))   // absolute TTL: 2 minutes
                .SetSlidingExpiration(TimeSpan.FromMinutes(1));   // sliding: 1 minute
        }
    }
}