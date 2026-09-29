// File: Edu.Application.IServices/IFileStorageService.cs
using Microsoft.AspNetCore.Http;

namespace Edu.Application.IServices;
public interface IFileStorageService
{
    Task<string> SaveFileAsync(IFormFile file, string folder);
    Task DeleteFileAsync(string fileUrlOrKey);
    Task<Stream?> OpenReadAsync(string fileUrlOrKey);
    Task<bool> ExistsAsync(string fileUrlOrKey);
    string? GetContentType(string fileName);
    Task<string?> GetPublicUrlAsync(string storageKeyOrUrl, TimeSpan? expiry = null);
    Task<string> SaveTextFileAsync(string key, string content);
}

