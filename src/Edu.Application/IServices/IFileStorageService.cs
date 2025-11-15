// File: Edu.Application.IServices/IFileStorageService.cs
using Microsoft.AspNetCore.Http;

namespace Edu.Application.IServices;
public interface IFileStorageService
{
    // Save uploaded file. Return a storage key or URL; for local returns relative path like "/uploads/..."
    Task<string> SaveFileAsync(IFormFile file, string folder);

    // Delete by storage key or URL
    Task DeleteFileAsync(string fileUrl);

    // Optional: open a read stream for fileUrl/storage key
    Task<Stream?> OpenReadAsync(string fileUrl);

    // Optional: check whether file exists
    Task<bool> ExistsAsync(string fileUrl);

    // Optional: get content type by file name
    string? GetContentType(string fileName);
    Task<string?> GetPublicUrlAsync(string storageKeyOrUrl, TimeSpan? expiry = null);
    Task<string> SaveTextFileAsync(string key, string content);
}

