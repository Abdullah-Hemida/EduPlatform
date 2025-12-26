using Edu.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using System.Globalization;

namespace Edu.Infrastructure.Services
{
    public interface IUserCultureProvider
    {
        /// <summary>
        /// Resolve CultureInfo for an already-loaded ApplicationUser instance (fast, sync).
        /// </summary>
        CultureInfo GetCulture(ApplicationUser? user);

        /// <summary>
        /// Resolve CultureInfo for a user id (may fetch from store).
        /// </summary>
        Task<CultureInfo> GetCultureAsync(string? userId);
    }
    public class UserCultureProvider : IUserCultureProvider
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public UserCultureProvider(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        }

        public CultureInfo GetCulture(ApplicationUser? user)
        {
            if (user == null) return CultureInfo.CurrentUICulture;

            var pref = user.PreferredLanguage;
            if (string.IsNullOrWhiteSpace(pref)) return CultureInfo.CurrentUICulture;

            // try full culture
            if (TryCreateCulture(pref, out var ci)) return ci;

            // try two-letter fallback
            var two = pref.Length >= 2 ? pref.Substring(0, 2) : pref;
            if (TryCreateCulture(two, out ci)) return ci;

            return CultureInfo.CurrentUICulture;
        }

        public async Task<CultureInfo> GetCultureAsync(string? userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return CultureInfo.CurrentUICulture;
            var user = await _userManager.FindByIdAsync(userId);
            return GetCulture(user);
        }

        private static bool TryCreateCulture(string cultureString, out CultureInfo ci)
        {
            try
            {
                ci = new CultureInfo(cultureString);
                return true;
            }
            catch
            {
                ci = CultureInfo.InvariantCulture;
                return false;
            }
        }
    }
}
