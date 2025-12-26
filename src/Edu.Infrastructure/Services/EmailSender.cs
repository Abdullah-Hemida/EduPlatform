using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Edu.Infrastructure.Services;
public class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string From { get; set; } = "";
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public interface IEmailSender
{
    Task SendEmailAsync(string toEmail, string subject, string htmlMessage, CancellationToken ct = default);
}

public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _opts;

    public SmtpEmailSender(IOptions<SmtpOptions> opts)
    {
        _opts = opts.Value;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage, CancellationToken ct = default)
    {
        using var client = new SmtpClient(_opts.Host, _opts.Port)
        {
            EnableSsl = _opts.UseSsl
        };

        if (!string.IsNullOrEmpty(_opts.Username))
        {
            client.Credentials = new NetworkCredential(_opts.Username, _opts.Password);
        }

        var mail = new MailMessage
        {
            From = new MailAddress(_opts.From),
            Subject = subject,
            Body = htmlMessage,
            IsBodyHtml = true
        };
        mail.To.Add(toEmail);
        // SmtpClient.SendMailAsync is available
        await client.SendMailAsync(mail, ct);
    }
}

