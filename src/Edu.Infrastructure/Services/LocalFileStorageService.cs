// File: Edu.Infrastructure.Services/LocalFileStorageService.cs
using Edu.Application.IServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Edu.Infrastructure.Services
{
    public class LocalFileStorageService : IFileStorageService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<LocalFileStorageService>? _logger;
        private readonly string _rootFolder;      // physical root folder (under webroot)
        private readonly string _publicBasePath;  // public base path (e.g. "/uploads")

        public LocalFileStorageService(IWebHostEnvironment env, IConfiguration config, ILogger<LocalFileStorageService>? logger = null)
        {
            _env = env ?? throw new ArgumentNullException(nameof(env));
            _logger = logger;
            // allow overriding in config: "LocalFileStorage:RootFolder" (relative to webroot) and "LocalFileStorage:PublicBasePath"
            var cfgRoot = config["LocalFileStorage:RootFolder"] ?? "uploads";
            _rootFolder = cfgRoot.Trim('/').Replace('\\', '/');

            _publicBasePath = config["LocalFileStorage:PublicBasePath"] ?? $"/{_rootFolder}";
        }

        private string GetWebRoot()
        {
            return _env.WebRootPath ?? _env.ContentRootPath ?? Directory.GetCurrentDirectory();
        }

        // Normalize keys like "folder/file.ext" (no leading slash)
        private static string NormalizeKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return "";
            // remove leading slashes and normalize separators
            var k = key.Trim().Replace('\\', '/').TrimStart('/');
            // if caller passed a full url, try to extract last segment(s) — but prefer callers to pass keys
            if (k.Contains("://"))
            {
                if (Uri.TryCreate(k, UriKind.Absolute, out var u))
                    k = Uri.UnescapeDataString(u.AbsolutePath).TrimStart('/');
            }
            return k;
        }

        // Ensure full path is under webroot to prevent path traversal
        private bool IsUnderRoot(string fullPath)
        {
            var root = Path.GetFullPath(GetWebRoot());
            var target = Path.GetFullPath(fullPath);
            return target.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }

        public async Task<string> SaveFileAsync(IFormFile file, string folder)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));

            var safeFolder = string.IsNullOrWhiteSpace(folder) ? _rootFolder : $"{_rootFolder}/{folder.Trim('/').Replace('\\', '/')}";
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var relativePath = $"{safeFolder.TrimStart('/')}/{fileName}".TrimStart('/');

            var webroot = GetWebRoot();
            var fullPath = Path.Combine(webroot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var dir = Path.GetDirectoryName(fullPath) ?? webroot;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            if (!IsUnderRoot(fullPath)) throw new InvalidOperationException("Invalid storage key / attempted path traversal.");

            using var fs = new FileStream(fullPath, FileMode.CreateNew);
            await file.CopyToAsync(fs);

            // <-- IMPORTANT: return a **storage key** WITHOUT leading slash or public prefix
            return NormalizeKey(relativePath);
        }

        public async Task DeleteFileAsync(string fileUrlOrKey)
        {
            if (string.IsNullOrWhiteSpace(fileUrlOrKey)) return;
            var key = NormalizeKey(fileUrlOrKey);

            // if provided a full url (starts with '/'), treat as relative path
            var rel = key;
            if (key.StartsWith("/")) rel = key.TrimStart('/');

            var fullPath = Path.Combine(GetWebRoot(), rel.Replace('/', Path.DirectorySeparatorChar));
            try
            {
                if (!IsUnderRoot(fullPath))
                {
                    _logger?.LogWarning("Attempt to delete file outside root: {FullPath}", fullPath);
                    return;
                }
                if (File.Exists(fullPath)) File.Delete(fullPath);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "DeleteFileAsync failed for {Path}", fullPath);
            }
            await Task.CompletedTask;
        }

        public Task<Stream?> OpenReadAsync(string fileUrlOrKey)
        {
            if (string.IsNullOrWhiteSpace(fileUrlOrKey)) return Task.FromResult<Stream?>(null);
            var key = NormalizeKey(fileUrlOrKey);
            var rel = key.StartsWith("/") ? key.TrimStart('/') : key;
            var fullPath = Path.Combine(GetWebRoot(), rel.Replace('/', Path.DirectorySeparatorChar));
            if (!IsUnderRoot(fullPath)) return Task.FromResult<Stream?>(null);
            if (!File.Exists(fullPath)) return Task.FromResult<Stream?>(null);
            Stream s = File.OpenRead(fullPath);
            return Task.FromResult<Stream?>(s);
        }

        public Task<bool> ExistsAsync(string fileUrlOrKey)
        {
            if (string.IsNullOrWhiteSpace(fileUrlOrKey)) return Task.FromResult(false);
            var key = NormalizeKey(fileUrlOrKey);
            var rel = key.StartsWith("/") ? key.TrimStart('/') : key;
            var fullPath = Path.Combine(GetWebRoot(), rel.Replace('/', Path.DirectorySeparatorChar));
            if (!IsUnderRoot(fullPath)) return Task.FromResult(false);
            return Task.FromResult(File.Exists(fullPath));
        }

        public string? GetContentType(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            var provider = new FileExtensionContentTypeProvider();
            return provider.TryGetContentType(fileName, out var ct) ? ct : null;
        }

        // Returns a public URL for the given storage key or absolute url
        // expiry parameter ignored for local files
        public Task<string?> GetPublicUrlAsync(string storageKeyOrUrl, TimeSpan? expiry = null)
        {
            if (string.IsNullOrWhiteSpace(storageKeyOrUrl)) return Task.FromResult<string?>(null);

            // If the caller passed an absolute URL, return it as-is
            if (storageKeyOrUrl.Contains("://")) return Task.FromResult<string?>(storageKeyOrUrl);

            var k = NormalizeKey(storageKeyOrUrl);

            // If caller accidentally passed a key that already begins with root folder (e.g. "uploads/xyz"),
            // we still combine to ensure single '/uploads/' prefix only.
            var publicRoot = _publicBasePath.TrimEnd('/'); // e.g. "/uploads"
            var combined = $"{publicRoot}/{k}".Replace("//", "/");

            // Ensure leading slash
            if (!combined.StartsWith("/")) combined = "/" + combined;

            return Task.FromResult<string?>(combined);
        }

        // Save a text file (json snapshot) to the local storage. Returns storage key.
        public async Task<string> SaveTextFileAsync(string key, string content)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));
            var normalized = NormalizeKey(key);
            var fullPath = Path.Combine(GetWebRoot(), normalized.Replace('/', Path.DirectorySeparatorChar));
            var dir = Path.GetDirectoryName(fullPath) ?? GetWebRoot();
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            if (!IsUnderRoot(fullPath)) throw new InvalidOperationException("Invalid storage key");

            await File.WriteAllTextAsync(fullPath, content ?? string.Empty, Encoding.UTF8);
            return NormalizeKey(normalized);
        }
    }
}




