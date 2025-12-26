// Edu.Infrastructure.Services.MailKitEmailSender.cs
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Edu.Infrastructure.Services
{
    /// <summary>
    /// MailKit-based IEmailSender implementation. Fully async and supports CancellationToken.
    /// </summary>
    public class MailKitEmailSender : IEmailSender
    {
        private readonly SmtpOptions _opts;

        public MailKitEmailSender(IOptions<SmtpOptions> opts)
        {
            _opts = opts?.Value ?? throw new ArgumentNullException(nameof(opts));
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(toEmail)) throw new ArgumentException("toEmail is required", nameof(toEmail));

            var msg = new MimeMessage();
            msg.From.Add(MailboxAddress.Parse(_opts.From));
            msg.To.Add(MailboxAddress.Parse(toEmail));
            msg.Subject = subject ?? string.Empty;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = htmlMessage ?? string.Empty
            };
            msg.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();

            // Connect: use SecureSocketOptions.Auto so MailKit negotiates the best available.
            // Note: in production you should validate server certificate; override validation only for dev/test.
            await client.ConnectAsync(_opts.Host, _opts.Port, _opts.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, ct);

            try
            {
                if (!string.IsNullOrEmpty(_opts.Username))
                {
                    // Authenticate if credentials provided
                    await client.AuthenticateAsync(_opts.Username, _opts.Password ?? string.Empty, ct);
                }

                await client.SendAsync(msg, ct);
            }
            finally
            {
                // Always disconnect gracefully
                try { await client.DisconnectAsync(true, ct); }
                catch { /* swallow disconnect errors to avoid bubbling from a send */ }
            }
        }
    }
}

