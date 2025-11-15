using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Edu.Infrastructure.Services
{
    /// <summary>
    /// Development-only email sender: logs emails and optionally writes them to a local folder for inspection.
    /// Safe to register in Development environment.
    /// </summary>
    public class NoOpEmailSender : IEmailSender
    {
        private readonly ILogger<NoOpEmailSender> _logger;
        private readonly string? _dumpFolder;

        public NoOpEmailSender(ILogger<NoOpEmailSender> logger, IConfiguration? config = null)
        {
            _logger = logger;
            // optional folder path configured at "DevEmail:DumpFolder" in appsettings.Development.json or env var
            _dumpFolder = config?.GetValue<string?>("DevEmail:DumpFolder") ?? "dev-mails";
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage, CancellationToken ct = default)
        {
            _logger.LogInformation("NoOpEmailSender -> To:{To} Subject:{Subject} BodyPreview:{Preview}",
                toEmail, subject, (htmlMessage?.Length > 240 ? htmlMessage.Substring(0, 240) + "…" : htmlMessage));

            try
            {
                if (!string.IsNullOrWhiteSpace(_dumpFolder))
                {
                    Directory.CreateDirectory(_dumpFolder);
                    var safeFile = Path.Combine(_dumpFolder, $"{System.DateTime.UtcNow:yyyyMMdd_HHmmssfff}_{SanitizeFilename(toEmail)}.html");
                    await File.WriteAllTextAsync(safeFile, $"To: {toEmail}\nSubject: {subject}\n\n{htmlMessage}", ct);
                    _logger.LogInformation("Saved dev email to {Path}", safeFile);
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write dev email to disk.");
            }
        }

        private static string SanitizeFilename(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "unknown";
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '-');
            return s.Replace("@", "_at_").Replace(".", "_");
        }
    }
}

