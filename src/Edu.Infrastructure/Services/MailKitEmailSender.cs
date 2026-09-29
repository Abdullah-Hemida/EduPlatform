// Edu.Infrastructure.Services.MailKitEmailSender.cs
using Edu.Application.IServices;
using Edu.Infrastructure.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Edu.Infrastructure.Services;

public sealed class MailKitEmailService : IEmailService
{
    private readonly SmtpOptions _opts;
    private readonly ILogger<MailKitEmailService> _logger;

    public MailKitEmailService(IOptions<SmtpOptions> opts, ILogger<MailKitEmailService> logger)
    {
        _opts = opts.Value;
        _logger = logger;
    }

    public Task SendToAdminAsync(string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_opts.AdminReceiveEmail))
            return Task.CompletedTask;

        return SendAsync(new EmailMessage(_opts.AdminReceiveEmail, subject, htmlBody), cancellationToken);
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message.To))
            throw new ArgumentException("Recipient is required.", nameof(message));

        var email = new MimeMessage();
        email.From.Add(new MailboxAddress(_opts.From, _opts.From));
        email.To.Add(MailboxAddress.Parse(message.To));
        email.Subject = message.Subject ?? string.Empty;
        email.Body = new BodyBuilder { HtmlBody = message.HtmlBody ?? string.Empty }.ToMessageBody();

        using var client = new SmtpClient();

        try
        {
            var secure = _opts.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
            await client.ConnectAsync(_opts.Host, _opts.Port, secure, cancellationToken);

            if (!string.IsNullOrWhiteSpace(_opts.Username))
            {
                await client.AuthenticateAsync(_opts.Username, _opts.Password ?? string.Empty, cancellationToken);
            }

            await client.SendAsync(email, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", message.To);
            throw;
        }
        finally
        {
            try
            {
                if (client.IsConnected)
                    await client.DisconnectAsync(true, cancellationToken);
            }
            catch
            {
            }
        }
    }
}

