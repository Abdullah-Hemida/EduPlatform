// File: Edu.Infrastructure.Services/AzureBlobStorageService.cs
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Edu.Application.IServices;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.StaticFiles;
using System.Text;

namespace Edu.Infrastructure.Services
{
    public sealed class AzureBlobOptions
    {
        public string ConnectionString { get; set; } = null!;
        public string Container { get; set; } = "edu-files";
        public bool UseCdnUrl { get; set; } = false;
        public string? CdnBaseUrl { get; set; }
        public int SasExpiryMinutes { get; set; } = 15;
    }

    public class AzureBlobStorageService : IFileStorageService
    {
        private readonly BlobContainerClient _container;
        private readonly AzureBlobOptions _opts;
        private readonly ILogger<AzureBlobStorageService>? _logger;

        public AzureBlobStorageService(IConfiguration configuration, ILogger<AzureBlobStorageService>? logger = null)
        {
            _opts = new AzureBlobOptions();
            configuration.GetSection("Storage:Azure").Bind(_opts);
            _logger = logger;

            var conn = _opts.ConnectionString;
            if (string.IsNullOrWhiteSpace(conn))
            {
                conn = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
            }
            if (string.IsNullOrWhiteSpace(conn))
            {
                throw new InvalidOperationException("Azure storage connection string is not configured.");
            }

            _container = new BlobContainerClient(conn, _opts.Container);
            _container.CreateIfNotExists(PublicAccessType.None);
        }

        static string NormalizeFolder(string? folder) => string.IsNullOrWhiteSpace(folder) ? "" : folder.Replace('\\', '/').Trim('/');

        public async Task<string> SaveFileAsync(IFormFile file, string folder)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            var ext = Path.GetExtension(file.FileName);
            var blobFileName = $"{Guid.NewGuid()}{ext}";
            var folderNormalized = NormalizeFolder(folder);
            var blobName = string.IsNullOrEmpty(folderNormalized) ? blobFileName : $"{folderNormalized}/{blobFileName}";

            var blobClient = _container.GetBlobClient(blobName);
            var headers = new BlobHttpHeaders { ContentType = file.ContentType ?? "application/octet-stream" };

            using (var s = file.OpenReadStream())
            {
                await blobClient.UploadAsync(s, new BlobUploadOptions { HttpHeaders = headers });
            }

            // return storage key (blobName)
            return blobName;
        }

        public async Task DeleteFileAsync(string fileUrlOrKey)
        {
            if (string.IsNullOrWhiteSpace(fileUrlOrKey)) return;
            var blobName = ExtractBlobNameFromUrlOrKey(fileUrlOrKey);
            if (string.IsNullOrEmpty(blobName)) return;
            var blob = _container.GetBlobClient(blobName);
            try
            {
                await blob.DeleteIfExistsAsync();
            }
            catch (RequestFailedException ex)
            {
                _logger?.LogWarning(ex, "DeleteFileAsync failed for blob {Blob}", blobName);
            }
        }

        public async Task<Stream?> OpenReadAsync(string fileUrlOrKey)
        {
            var blobName = ExtractBlobNameFromUrlOrKey(fileUrlOrKey);
            if (string.IsNullOrEmpty(blobName)) return null;
            var blob = _container.GetBlobClient(blobName);
            var exists = (await blob.ExistsAsync()).Value;
            if (!exists) return null;
            var resp = await blob.DownloadStreamingAsync();
            return resp.Value.Content;
        }

        public async Task<bool> ExistsAsync(string fileUrlOrKey)
        {
            var blobName = ExtractBlobNameFromUrlOrKey(fileUrlOrKey);
            if (string.IsNullOrEmpty(blobName)) return false;
            var blob = _container.GetBlobClient(blobName);
            return (await blob.ExistsAsync()).Value;
        }

        public string? GetContentType(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            var provider = new FileExtensionContentTypeProvider();
            return provider.TryGetContentType(fileName, out var ct) ? ct : null;
        }

        // Try to return a public URL. If container is private, attempt to generate a SAS URL.
        // If CDN configured and UseCdnUrl==true, return CDN URL (no SAS).
        public Task<string?> GetPublicUrlAsync(string storageKeyOrUrl, TimeSpan? expiry = null)
        {
            if (string.IsNullOrWhiteSpace(storageKeyOrUrl)) return Task.FromResult<string?>(null);

            // If already a full url (absolute), return it
            if (storageKeyOrUrl.Contains("://")) return Task.FromResult<string?>(storageKeyOrUrl);

            var blobName = ExtractBlobNameFromUrlOrKey(storageKeyOrUrl) ?? string.Empty;
            var blobClient = _container.GetBlobClient(blobName);

            if (_opts.UseCdnUrl && !string.IsNullOrEmpty(_opts.CdnBaseUrl))
            {
                // trust that CDN serves the blobName path
                var url = $"{_opts.CdnBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(blobName)}";
                return Task.FromResult<string?>(url);
            }

            // If container allows public access, return blob URI
            try
            {
                // Try to create SAS (will work if client has account key)
                var expiryMinutes = expiry?.TotalMinutes ?? _opts.SasExpiryMinutes;
                var sasUri = blobClient.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddMinutes(expiryMinutes));
                return Task.FromResult<string?>(sasUri.ToString());
            }
            catch
            {
                // fallback to direct URI (may be private and not accessible externally)
                return Task.FromResult<string?>(blobClient.Uri.ToString());
            }
        }

        private string? ExtractBlobNameFromUrlOrKey(string fileUrlOrKey)
        {
            if (string.IsNullOrWhiteSpace(fileUrlOrKey)) return null;
            // If it looks like a full URL -> extract path part
            if (fileUrlOrKey.Contains("://"))
            {
                if (Uri.TryCreate(fileUrlOrKey, UriKind.Absolute, out var uri))
                {
                    var path = Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/');
                    return path;
                }
                return null;
            }
            // otherwise treat as key (normalize)
            return fileUrlOrKey.Trim().TrimStart('/').Replace('\\', '/');
        }

        public async Task<string> SaveTextFileAsync(string key, string content)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));
            var normalized = ExtractBlobNameFromUrlOrKey(key) ?? throw new ArgumentException("Invalid key");
            var blob = _container.GetBlobClient(normalized);
            var bytes = Encoding.UTF8.GetBytes(content ?? string.Empty);
            using var ms = new MemoryStream(bytes);
            await blob.UploadAsync(ms, overwrite: true);
            return normalized;
        }
    }
}

