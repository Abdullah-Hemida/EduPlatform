using Microsoft.AspNetCore.Http;

namespace Edu.Infrastructure.Storage
{
    public static class FileValidators
    {
        private const long MaxImageBytes = 5 * 1024 * 1024; // 5 MB
        private const long MaxDocBytes = 10 * 1024 * 1024; // 10 MB

        private static readonly string[] AllowedImageExtensions = new[] { ".jpg", ".jpeg", ".png" };
        private static readonly string[] AllowedDocExtensions = new[] { ".pdf", ".doc", ".docx" };

        public static bool IsValidImage(IFormFile file)
        {
            if (file == null) return false;
            if (file.Length == 0 || file.Length > MaxImageBytes) return false;
            var ext = System.IO.Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedImageExtensions.Contains(ext)) return false;
            return true;
        }

        public static bool IsValidDocument(IFormFile file)
        {
            if (file == null) return false;
            if (file.Length == 0 || file.Length > MaxDocBytes) return false;
            var ext = System.IO.Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedDocExtensions.Contains(ext)) return false;
            return true;
        }
    }
}
