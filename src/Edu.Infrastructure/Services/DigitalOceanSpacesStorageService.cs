using Amazon.S3;
using Amazon.S3.Model;
using Edu.Application.IServices;
using Edu.Infrastructure.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace Edu.Infrastructure.Services;

public sealed class DigitalOceanSpacesStorageService : IFileStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly DigitalOceanSpacesOptions _opts;
    private readonly ILogger<DigitalOceanSpacesStorageService> _logger;

    public DigitalOceanSpacesStorageService(
        IAmazonS3 s3Client,
        IOptions<DigitalOceanSpacesOptions> opts,
        ILogger<DigitalOceanSpacesStorageService> logger)
    {
        _s3Client = s3Client;
        _opts = opts.Value;
        _logger = logger;
    }

    private static string NormalizeFolder(string? folder)
        => string.IsNullOrWhiteSpace(folder) ? string.Empty : folder.Replace('\\', '/').Trim('/');

    public async Task<string> SaveFileAsync(IFormFile file, string folder)
    {
        if (file == null)
            throw new ArgumentNullException(nameof(file));

        if (file.Length == 0)
            throw new ArgumentException("File is empty.", nameof(file));

        var ext = Path.GetExtension(file.FileName);
        var blobFileName = $"{Guid.NewGuid():N}{ext}";

        var folderNormalized = NormalizeFolder(folder);

        var fileKey = string.IsNullOrEmpty(folderNormalized)
            ? blobFileName
            : $"{folderNormalized}/{blobFileName}";

        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType;

        await using var stream = file.OpenReadStream();

        var request = new PutObjectRequest
        {
            BucketName = _opts.Container,
            Key = fileKey,
            InputStream = stream,
            ContentType = contentType,
            CannedACL = S3CannedACL.Private
        };

        try
        {
            _logger.LogInformation(
                "Uploading file to DigitalOcean Spaces. Bucket={Bucket}, Key={Key}, Endpoint={Endpoint}, Size={Size}, ContentType={ContentType}",
                _opts.Container,
                fileKey,
                _opts.ServiceUrl,
                file.Length,
                contentType);

            // Diagnostic: verify that the configured credentials
            // can see the configured Space.
            var metadata = await _s3Client.GetBucketLocationAsync(
                new Amazon.S3.Model.GetBucketLocationRequest
                {
                    BucketName = _opts.Container
                });

            _logger.LogInformation(
                "Bucket check succeeded. Bucket={Bucket}, Location={Location}",
                _opts.Container,
                metadata.Location?.Value);

            // Actual upload
            await _s3Client.PutObjectAsync(request);

            _logger.LogInformation(
                "Successfully uploaded file to DigitalOcean Spaces. Bucket={Bucket}, Key={Key}",
                _opts.Container,
                fileKey);

            return fileKey;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(
                ex,
                """
            DigitalOcean Spaces operation failed.
            Bucket: {Bucket}
            Key: {Key}
            Endpoint: {Endpoint}
            HTTP Status: {StatusCode}
            AWS Error Code: {ErrorCode}
            Request ID: {RequestId}
            Amazon ID 2: {AmazonId2}
            Message: {Message}
            """,
                _opts.Container,
                fileKey,
                _opts.ServiceUrl,
                ex.StatusCode,
                ex.ErrorCode,
                ex.RequestId,
                ex.AmazonId2,
                ex.Message);

            throw;
        }
    }

    public async Task DeleteFileAsync(string fileUrlOrKey)
    {
        if (string.IsNullOrWhiteSpace(fileUrlOrKey)) return;

        var fileKey = ExtractBlobNameFromUrlOrKey(fileUrlOrKey);
        if (string.IsNullOrWhiteSpace(fileKey)) return;

        try
        {
            var request = new DeleteObjectRequest
            {
                BucketName = _opts.Container,
                Key = fileKey
            };
            await _s3Client.DeleteObjectAsync(request);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogWarning(ex, "DeleteFileAsync failed for key {Key}", fileKey);
        }
    }

    public async Task<Stream?> OpenReadAsync(string fileUrlOrKey)
    {
        var fileKey = ExtractBlobNameFromUrlOrKey(fileUrlOrKey);
        if (string.IsNullOrWhiteSpace(fileKey)) return null;

        try
        {
            var request = new GetObjectRequest
            {
                BucketName = _opts.Container,
                Key = fileKey
            };
            var response = await _s3Client.GetObjectAsync(request);
            return response.ResponseStream;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> ExistsAsync(string fileUrlOrKey)
    {
        var fileKey = ExtractBlobNameFromUrlOrKey(fileUrlOrKey);
        if (string.IsNullOrWhiteSpace(fileKey)) return false;

        try
        {
            await _s3Client.GetObjectMetadataAsync(_opts.Container, fileKey);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string? GetContentType(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;

        var provider = new FileExtensionContentTypeProvider();
        return provider.TryGetContentType(fileName, out var ct) ? ct : null;
    }

    public Task<string?> GetPublicUrlAsync(string storageKeyOrUrl, TimeSpan? expiry = null)
    {
        if (string.IsNullOrWhiteSpace(storageKeyOrUrl))
            return Task.FromResult<string?>(null);

        if (storageKeyOrUrl.Contains("://", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<string?>(storageKeyOrUrl);

        var fileKey = ExtractBlobNameFromUrlOrKey(storageKeyOrUrl);
        if (string.IsNullOrWhiteSpace(fileKey))
            return Task.FromResult<string?>(null);

        if (_opts.UseCdnUrl && !string.IsNullOrWhiteSpace(_opts.CdnBaseUrl))
        {
            var url = $"{_opts.CdnBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(fileKey)}";
            return Task.FromResult<string?>(url);
        }

        try
        {
            var minutes = expiry?.TotalMinutes ?? _opts.SasExpiryMinutes;

            // Map Azure SAS URI logic directly to S3 Pre-Signed URLs
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _opts.Container,
                Key = fileKey,
                Expires = DateTime.UtcNow.AddMinutes(minutes)
            };

            string url = _s3Client.GetPreSignedURL(request);
            return Task.FromResult<string?>(url);
        }
        catch
        {
            // Fallback default static link
            var cleanUrl = _opts.ServiceUrl.Replace("https://", $"https://{_opts.Container}.");
            return Task.FromResult<string?>($"{cleanUrl.TrimEnd('/')}/{fileKey}");
        }
    }

    public async Task<string> SaveTextFileAsync(string key, string content)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key));

        var fileKey = ExtractBlobNameFromUrlOrKey(key) ?? throw new ArgumentException("Invalid key", nameof(key));
        var bytes = Encoding.UTF8.GetBytes(content ?? string.Empty);

        using var ms = new MemoryStream(bytes);
        var request = new PutObjectRequest
        {
            BucketName = _opts.Container,
            Key = fileKey,
            InputStream = ms,
            ContentType = "text/plain"
        };

        await _s3Client.PutObjectAsync(request);
        return fileKey;
    }

    private static string? ExtractBlobNameFromUrlOrKey(string fileUrlOrKey)
    {
        if (string.IsNullOrWhiteSpace(fileUrlOrKey)) return null;

        if (fileUrlOrKey.Contains("://", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(fileUrlOrKey, UriKind.Absolute, out var uri))
            {
                // Drops host paths safely returning just folders and items 
                var segments = uri.AbsolutePath.TrimStart('/').Split('/');
                return Uri.UnescapeDataString(string.Join("/", segments));
            }
            return null;
        }

        return fileUrlOrKey.Trim().TrimStart('/').Replace('\\', '/');
    }
}



//// File: Edu.Infrastructure.Services/AzureBlobStorageService.cs
//using Azure;
//using Azure.Storage.Blobs;
//using Azure.Storage.Blobs.Models;
//using Azure.Storage.Sas;
//using Edu.Application.IServices;
//using Edu.Infrastructure.Options;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.StaticFiles;
//using Microsoft.Extensions.Logging;
//using Microsoft.Extensions.Options;
//using System.Text;

//namespace Edu.Infrastructure.Services;

//public sealed class AzureBlobStorageService : IFileStorageService
//{
//    private readonly BlobServiceClient _blobServiceClient;
//    private readonly AzureBlobOptions _opts;
//    private readonly ILogger<AzureBlobStorageService> _logger;

//    public AzureBlobStorageService(
//        BlobServiceClient blobServiceClient,
//        IOptions<AzureBlobOptions> opts,
//        ILogger<AzureBlobStorageService> logger)
//    {
//        _blobServiceClient = blobServiceClient;
//        _opts = opts.Value;
//        _logger = logger;
//    }

//    private BlobContainerClient GetContainerClient()
//        => _blobServiceClient.GetBlobContainerClient(_opts.Container);

//    private async Task EnsureContainerExistsAsync()
//    {
//        var container = GetContainerClient();
//        await container.CreateIfNotExistsAsync(PublicAccessType.None);
//    }

//    private static string NormalizeFolder(string? folder)
//        => string.IsNullOrWhiteSpace(folder) ? string.Empty : folder.Replace('\\', '/').Trim('/');

//    public async Task<string> SaveFileAsync(IFormFile file, string folder)
//    {
//        if (file == null) throw new ArgumentNullException(nameof(file));
//        if (file.Length == 0) throw new ArgumentException("File is empty.", nameof(file));

//        await EnsureContainerExistsAsync();

//        var ext = Path.GetExtension(file.FileName);
//        var blobFileName = $"{Guid.NewGuid():N}{ext}";
//        var folderNormalized = NormalizeFolder(folder);
//        var blobName = string.IsNullOrEmpty(folderNormalized) ? blobFileName : $"{folderNormalized}/{blobFileName}";

//        var blobClient = GetContainerClient().GetBlobClient(blobName);

//        var headers = new BlobHttpHeaders
//        {
//            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType
//        };

//        await using var stream = file.OpenReadStream();
//        await blobClient.UploadAsync(stream, new BlobUploadOptions { HttpHeaders = headers });

//        return blobName;
//    }

//    public async Task DeleteFileAsync(string fileUrlOrKey)
//    {
//        if (string.IsNullOrWhiteSpace(fileUrlOrKey)) return;

//        var blobName = ExtractBlobNameFromUrlOrKey(fileUrlOrKey);
//        if (string.IsNullOrWhiteSpace(blobName)) return;

//        try
//        {
//            await EnsureContainerExistsAsync();
//            var blob = GetContainerClient().GetBlobClient(blobName);
//            await blob.DeleteIfExistsAsync();
//        }
//        catch (RequestFailedException ex)
//        {
//            _logger.LogWarning(ex, "DeleteFileAsync failed for blob {Blob}", blobName);
//        }
//    }

//    public async Task<Stream?> OpenReadAsync(string fileUrlOrKey)
//    {
//        var blobName = ExtractBlobNameFromUrlOrKey(fileUrlOrKey);
//        if (string.IsNullOrWhiteSpace(blobName)) return null;

//        await EnsureContainerExistsAsync();
//        var blob = GetContainerClient().GetBlobClient(blobName);

//        if (!await blob.ExistsAsync()) return null;

//        var resp = await blob.DownloadStreamingAsync();
//        return resp.Value.Content;
//    }

//    public async Task<bool> ExistsAsync(string fileUrlOrKey)
//    {
//        var blobName = ExtractBlobNameFromUrlOrKey(fileUrlOrKey);
//        if (string.IsNullOrWhiteSpace(blobName)) return false;

//        await EnsureContainerExistsAsync();
//        var blob = GetContainerClient().GetBlobClient(blobName);
//        return (await blob.ExistsAsync()).Value;
//    }

//    public string? GetContentType(string fileName)
//    {
//        if (string.IsNullOrWhiteSpace(fileName)) return null;

//        var provider = new FileExtensionContentTypeProvider();
//        return provider.TryGetContentType(fileName, out var ct) ? ct : null;
//    }

//    public Task<string?> GetPublicUrlAsync(string storageKeyOrUrl, TimeSpan? expiry = null)
//    {
//        if (string.IsNullOrWhiteSpace(storageKeyOrUrl))
//            return Task.FromResult<string?>(null);

//        if (storageKeyOrUrl.Contains("://", StringComparison.OrdinalIgnoreCase))
//            return Task.FromResult<string?>(storageKeyOrUrl);

//        var blobName = ExtractBlobNameFromUrlOrKey(storageKeyOrUrl);
//        if (string.IsNullOrWhiteSpace(blobName))
//            return Task.FromResult<string?>(null);

//        var blobClient = GetContainerClient().GetBlobClient(blobName);

//        if (_opts.UseCdnUrl && !string.IsNullOrWhiteSpace(_opts.CdnBaseUrl))
//        {
//            var url = $"{_opts.CdnBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(blobName)}";
//            return Task.FromResult<string?>(url);
//        }

//        try
//        {
//            if (blobClient.CanGenerateSasUri)
//            {
//                var minutes = expiry?.TotalMinutes ?? _opts.SasExpiryMinutes;
//                var sasUri = blobClient.GenerateSasUri(
//                    BlobSasPermissions.Read,
//                    DateTimeOffset.UtcNow.AddMinutes(minutes));

//                return Task.FromResult<string?>(sasUri.ToString());
//            }
//        }
//        catch
//        {
//        }

//        return Task.FromResult<string?>(blobClient.Uri.ToString());
//    }

//    public async Task<string> SaveTextFileAsync(string key, string content)
//    {
//        if (string.IsNullOrWhiteSpace(key))
//            throw new ArgumentNullException(nameof(key));

//        await EnsureContainerExistsAsync();

//        var normalized = ExtractBlobNameFromUrlOrKey(key) ?? throw new ArgumentException("Invalid key", nameof(key));
//        var blob = GetContainerClient().GetBlobClient(normalized);
//        var bytes = Encoding.UTF8.GetBytes(content ?? string.Empty);

//        await using var ms = new MemoryStream(bytes);
//        await blob.UploadAsync(ms, overwrite: true);

//        return normalized;
//    }

//    private static string? ExtractBlobNameFromUrlOrKey(string fileUrlOrKey)
//    {
//        if (string.IsNullOrWhiteSpace(fileUrlOrKey)) return null;

//        if (fileUrlOrKey.Contains("://", StringComparison.OrdinalIgnoreCase))
//        {
//            if (Uri.TryCreate(fileUrlOrKey, UriKind.Absolute, out var uri))
//            {
//                return Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/');
//            }

//            return null;
//        }

//        return fileUrlOrKey.Trim().TrimStart('/').Replace('\\', '/');
//    }
//}

