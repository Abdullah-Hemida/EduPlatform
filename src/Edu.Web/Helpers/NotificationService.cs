// File: Edu.Infrastructure.Services
using Edu.Domain.Entities;
using Edu.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using System.Globalization;
using Edu.Web.Resources;
using Edu.Infrastructure.Localization;

namespace Edu.Web.Helpers
{
    public interface INotificationService
    {
        /// <summary>
        /// Send a localized email to a specific user (subject & body keys come from localization resources).
        /// </summary>
        Task SendLocalizedEmailAsync(ApplicationUser recipient, string subjectKey, string bodyKey, params object[] args);

        /// <summary>
        /// Notify all admins. For each admin we call argsFactory(admin) to compute per-admin arguments (e.g. include teacher name).
        /// </summary>
        Task NotifyAllAdminsAsync(string adminSubjectKey, string adminBodyKey, Func<ApplicationUser, Task<object[]>> argsFactoryAsync);
    }
    public class NotificationService : INotificationService
    {
        private readonly IEmailSender _emailSender;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IUserCultureProvider _userCultureProvider;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            IEmailSender emailSender,
            UserManager<ApplicationUser> userManager,
            IStringLocalizer<SharedResource> localizer,
            IUserCultureProvider userCultureProvider,
            ILogger<NotificationService> logger)
        {
            _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            _userCultureProvider = userCultureProvider ?? throw new ArgumentNullException(nameof(userCultureProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SendLocalizedEmailAsync(ApplicationUser recipient, string subjectKey, string bodyKey, params object[] args)
        {
            if (recipient == null)
            {
                _logger.LogDebug("SendLocalizedEmailAsync: recipient is null for keys {Subj}/{Body}", subjectKey, bodyKey);
                return;
            }

            if (string.IsNullOrEmpty(recipient.Email))
            {
                _logger.LogWarning("SendLocalizedEmailAsync: recipient {UserId} has no Email configured; skipping", recipient.Id);
                return;
            }

            try
            {
                // Resolve culture for the recipient
                var culture = _userCultureProvider.GetCulture(recipient) ?? CultureInfo.CurrentUICulture;

                // Localizer pinned to that culture
                var localized = _localizer.WithCulture(culture);

                // Subject: fetch template then format with args if present
                var subjLocalized = localized[subjectKey];
                var subjTemplate = subjLocalized.ResourceNotFound ? subjectKey : (subjLocalized.Value ?? subjectKey);
                var subject = SafeFormat(subjTemplate, args);

                // Body: prefer localized formatted overload; fall back to safe format if necessary
                string body;
                if (args != null && args.Length > 0)
                {
                    var bodyLocalized = localized[bodyKey, args];
                    if (!bodyLocalized.ResourceNotFound)
                    {
                        body = bodyLocalized.Value ?? string.Empty;
                    }
                    else
                    {
                        // fallback formatting
                        var bodyTemplate = localized[bodyKey].Value ?? bodyKey;
                        body = SafeFormat(bodyTemplate, args);
                    }
                }
                else
                {
                    var bodyLocalized = localized[bodyKey];
                    body = bodyLocalized.ResourceNotFound ? bodyKey : (bodyLocalized.Value ?? bodyKey);
                }

                _logger.LogInformation("NotificationService: Sending email to {Email} Culture={Culture} Subject='{Subject}'",
                    recipient.Email, culture.Name, subject);

                await _emailSender.SendEmailAsync(recipient.Email, subject, body);

                _logger.LogDebug("NotificationService: Email sent to {Email}", recipient.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NotificationService: error sending email to {UserId} ({Email}) using keys {SubjKey}/{BodyKey}",
                    recipient.Id, recipient.Email, subjectKey, bodyKey);
            }
        }

        public async Task NotifyAllAdminsAsync(string adminSubjectKey, string adminBodyKey, Func<ApplicationUser, Task<object[]>> argsFactoryAsync)
        {
            try
            {
                var admins = await _userManager.GetUsersInRoleAsync("Admin");
                if (admins == null || !admins.Any())
                {
                    _logger.LogWarning("NotifyAllAdminsAsync: no admins found in role 'Admin'");
                    return;
                }

                _logger.LogInformation("NotifyAllAdminsAsync: notifying {Count} admins using keys {Subj}/{Body}", admins.Count, adminSubjectKey, adminBodyKey);

                foreach (var admin in admins)
                {
                    if (admin == null)
                    {
                        _logger.LogWarning("NotifyAllAdminsAsync: null admin encountered; skipping");
                        continue;
                    }
                    if (string.IsNullOrEmpty(admin.Email))
                    {
                        _logger.LogWarning("NotifyAllAdminsAsync: admin {AdminId} has no Email; skipping", admin.Id);
                        continue;
                    }

                    try
                    {
                        var args = argsFactoryAsync != null ? await argsFactoryAsync(admin) : Array.Empty<object>();
                        await SendLocalizedEmailAsync(admin, adminSubjectKey, adminBodyKey, args);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "NotifyAllAdminsAsync: failed to notify admin {AdminId}", admin.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NotifyAllAdminsAsync: failed enumerating admins");
            }
        }

        // safe string.Format wrapper: returns template if formatting fails
        private static string SafeFormat(string template, object[] args)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;
            if (args == null || args.Length == 0) return template;
            try
            {
                return string.Format(template, args);
            }
            catch (FormatException)
            {
                return template; // fallback to unformatted template (avoid throwing)
            }
        }
    }
}

