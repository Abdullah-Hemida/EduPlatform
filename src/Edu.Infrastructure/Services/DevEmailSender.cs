using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Edu.Infrastructure.Services
{
    /// <summary>
    /// Development email sender that saves each email as an HTML file (and keeps an index.html).
    /// Safe to register in Development environment.
    /// Config keys (section "DevEmail"):
    ///   - DumpFolder (string, optional) : relative path under content root (default "dev-mails")
    ///   - MaxSaved (int, optional) : keep at most this many files (default 200)
    ///   - OpenInBrowser (bool, optional) : open each email after saving (default false)
    /// </summary>
    public class DevEmailSender : IEmailSender
    {
        private readonly ILogger<DevEmailSender> _logger;
        private readonly string _dumpFolder;
        private readonly int _maxSaved;
        private readonly bool _openInBrowser;
        private readonly object _indexLock = new();
        private readonly string _indexFileName = "index.html";

        public DevEmailSender(ILogger<DevEmailSender> logger, IConfiguration? config = null, IWebHostEnvironment? env = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var cfgFolder = config?.GetValue<string?>("DevEmail:DumpFolder") ?? "dev-mails";
            var contentRoot = env?.ContentRootPath ?? Directory.GetCurrentDirectory();
            _dumpFolder = Path.GetFullPath(Path.Combine(contentRoot, cfgFolder));

            _maxSaved = config?.GetValue<int?>("DevEmail:MaxSaved") ?? 200;
            _openInBrowser = config?.GetValue<bool?>("DevEmail:OpenInBrowser") ?? false;

            try
            {
                Directory.CreateDirectory(_dumpFolder);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DevEmailSender cannot create dump folder {DumpFolder}", _dumpFolder);
            }

            _logger.LogInformation("DevEmailSender initialized. DumpFolder={DumpFolder} MaxSaved={MaxSaved} OpenInBrowser={OpenInBrowser}",
                _dumpFolder, _maxSaved, _openInBrowser);
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage, CancellationToken ct = default)
        {
            try
            {
                var timestamp = DateTime.UtcNow;
                var safeTo = SanitizeFilename(toEmail);
                var fileName = $"{timestamp:yyyyMMdd_HHmmssfff}_{safeTo}.html";
                var filePath = Path.Combine(_dumpFolder, fileName);

                // Build HTML wrapper with metadata and body
                var sb = new StringBuilder();
                sb.AppendLine("<!doctype html>");
                sb.AppendLine("<html lang=\"en\"><head><meta charset=\"utf-8\"/>");
                sb.AppendLine($"<title>{HtmlEncode(subject)}</title>");
                sb.AppendLine("<style>body{font-family:Segoe UI, Roboto, Arial; margin:20px;} header{margin-bottom:12px;padding-bottom:8px;border-bottom:1px solid #eee} .meta{color:#555;font-size:0.9em} .body{margin-top:18px} pre{white-space:pre-wrap;word-break:break-word;}</style>");
                sb.AppendLine("</head><body>");
                sb.AppendLine("<header>");
                sb.AppendLine($"<h2>{HtmlEncode(subject)}</h2>");
                sb.AppendLine($"<div class=\"meta\"><strong>To:</strong> {HtmlEncode(toEmail)} &nbsp; • &nbsp; <strong>Date (UTC):</strong> {timestamp:u}</div>");
                sb.AppendLine("</header>");
                sb.AppendLine("<section class=\"body\">");

                // If htmlMessage looks like HTML (contains tags) embed raw, otherwise wrap in <pre>
                if (!string.IsNullOrEmpty(htmlMessage) && (htmlMessage.Contains("<") && htmlMessage.Contains(">")))
                {
                    sb.AppendLine(htmlMessage);
                }
                else
                {
                    sb.AppendLine($"<pre>{HtmlEncode(htmlMessage ?? string.Empty)}</pre>");
                }

                sb.AppendLine("</section>");
                sb.AppendLine("<hr/><footer><small>Dev email saved by DevEmailSender</small></footer>");
                sb.AppendLine("</body></html>");

                await File.WriteAllTextAsync(filePath, sb.ToString(), ct);

                _logger.LogInformation("Dev email written to {Path} (To={To}, Subject={Subject})", filePath, toEmail, subject);

                // update index
                try
                {
                    lock (_indexLock)
                    {
                        UpdateIndexHtml();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed updating dev email index");
                }

                if (_openInBrowser)
                {
                    try
                    {
                        // Try to open the file in default browser (UseShellExecute required)
                        var psi = new ProcessStartInfo
                        {
                            FileName = filePath,
                            UseShellExecute = true
                        };
                        Process.Start(psi);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to open dev email automatically");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DevEmailSender failed to write email to disk");
            }
        }

        private void UpdateIndexHtml()
        {
            // Build a simple index that lists the most recent files
            var files = Directory.Exists(_dumpFolder)
                ? Directory.GetFiles(_dumpFolder, "*.html").Select(f => new FileInfo(f)).Where(fi => fi.Name != _indexFileName).OrderByDescending(fi => fi.CreationTimeUtc).ToList()
                : new System.Collections.Generic.List<FileInfo>();

            // prune older files beyond MaxSaved
            if (files.Count > _maxSaved)
            {
                var toRemove = files.Skip(_maxSaved).ToList();
                foreach (var fi in toRemove)
                {
                    try { File.Delete(fi.FullName); }
                    catch { /* ignore */ }
                }
                files = files.Take(_maxSaved).ToList();
            }

            var indexSb = new StringBuilder();
            indexSb.AppendLine("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"/><title>Dev Emails</title>");
            indexSb.AppendLine("<style>body{font-family:Segoe UI, Roboto, Arial;margin:20px} table{width:100%;border-collapse:collapse} th,td{padding:8px;border-bottom:1px solid #eee;text-align:left} a{color:#0366d6}</style>");
            indexSb.AppendLine("</head><body>");
            indexSb.AppendLine($"<h1>Dev Emails ({files.Count})</h1>");
            indexSb.AppendLine("<table><thead><tr><th>Date (UTC)</th><th>File</th><th>Size</th></tr></thead><tbody>");

            foreach (var fi in files)
            {
                var rel = Uri.EscapeUriString(fi.Name);
                indexSb.AppendLine($"<tr><td>{fi.CreationTimeUtc:u}</td><td><a href=\"{rel}\">{HtmlEncode(fi.Name)}</a></td><td>{fi.Length} bytes</td></tr>");
            }

            indexSb.AppendLine("</tbody></table>");
            indexSb.AppendLine("</body></html>");

            var indexPath = Path.Combine(_dumpFolder, _indexFileName);
            File.WriteAllText(indexPath, indexSb.ToString());
        }

        private static string SanitizeFilename(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "unknown";
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '-');
            return s.Replace("@", "_at_").Replace(".", "_");
        }

        private static string HtmlEncode(string? s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return System.Net.WebUtility.HtmlEncode(s);
        }
    }
}


