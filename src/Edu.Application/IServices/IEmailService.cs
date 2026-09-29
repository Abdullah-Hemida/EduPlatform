
namespace Edu.Application.IServices
{
    public sealed record EmailMessage(string To, string Subject, string HtmlBody);

    public interface IEmailService
    {
        Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
        Task SendToAdminAsync(string subject, string htmlBody, CancellationToken cancellationToken = default);
    }
}
