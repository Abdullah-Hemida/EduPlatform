
namespace Edu.Infrastructure.Options
{

    public sealed class DigitalOceanSpacesOptions
    {
        public string? AccessKey { get; set; }
        public string? SecretKey { get; set; }
        public string Container { get; set; } = "futuragenerazion-storage-file"; // This behaves as your bucket name
        public string ServiceUrl { get; set; } = "https://digitaloceanspaces.com"; // Choose your DO region URL
        public bool UseCdnUrl { get; set; } = false;
        public string? CdnBaseUrl { get; set; }
        public int SasExpiryMinutes { get; set; } = 15;
    }


    public sealed class SmtpOptions
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public bool UseSsl { get; set; } = true;
        public string From { get; set; } = string.Empty;
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string AdminReceiveEmail { get; set; } = string.Empty;
    }
    public sealed class AdminSeedOptions
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
