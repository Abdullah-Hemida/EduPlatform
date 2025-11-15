
namespace Edu.Infrastructure.Services
{
    // Adapter that lets the Identity UI consumer use your IEmailSender implementation.
    public class IdentityEmailSenderAdapter : Microsoft.AspNetCore.Identity.UI.Services.IEmailSender
    {
        private readonly IEmailSender _inner;

        public IdentityEmailSenderAdapter(IEmailSender inner)
        {
            _inner = inner;
        }

        // Microsoft interface doesn't accept CancellationToken, so forward without token.
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
            => _inner.SendEmailAsync(email, subject, htmlMessage, CancellationToken.None);
    }
}

