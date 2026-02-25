using System.Security.Claims;
using System.Text;
using System.Text.Json;
using LeadManagementPortal.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace LeadManagementPortal.Services
{
    public class LocalFileStorageService : IFileStorageService
    {
        private readonly string _rootPath;
        private readonly string _baseUrlPath;
        private readonly IDataProtector _protector;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public LocalFileStorageService(
            IWebHostEnvironment env,
            IOptions<LocalStorageOptions> options,
            IDataProtectionProvider dataProtectionProvider,
            IHttpContextAccessor httpContextAccessor)
        {
            var configuredRoot = options.Value.RootPath?.Trim();
            _rootPath = string.IsNullOrWhiteSpace(configuredRoot)
                ? Path.Combine(env.ContentRootPath, "App_Data", "uploads")
                : configuredRoot;

            var baseUrlPath = options.Value.BaseUrlPath?.Trim();
            _baseUrlPath = string.IsNullOrWhiteSpace(baseUrlPath) ? "/files" : baseUrlPath;

            _protector = dataProtectionProvider.CreateProtector("LeadManagementPortal.LocalFiles.v1");
            _httpContextAccessor = httpContextAccessor;

            Directory.CreateDirectory(_rootPath);
        }

        public async Task<string> UploadAsync(Stream content, string contentType, string key, CancellationToken ct = default)
        {
            var filePath = GetSafeFilePath(key);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await content.CopyToAsync(fs, ct);
            return key;
        }

        public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        {
            var filePath = GetSafeFilePath(key);
            return Task.FromResult(System.IO.File.Exists(filePath));
        }

        public Task<bool> DeleteAsync(string key, CancellationToken ct = default)
        {
            var filePath = GetSafeFilePath(key);
            if (!System.IO.File.Exists(filePath))
            {
                return Task.FromResult(false);
            }

            System.IO.File.Delete(filePath);
            return Task.FromResult(true);
        }

        public Task<string> GetPreSignedDownloadUrlAsync(string key, TimeSpan expires, CancellationToken ct = default)
        {
            var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Task.FromResult(string.Empty);

            var payload = new FileToken
            {
                Key = key,
                UserId = userId,
                ExpiresAtUtc = DateTimeOffset.UtcNow.Add(expires)
            };

            var json = JsonSerializer.Serialize(payload);
            var protectedText = _protector.Protect(json);
            var token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(protectedText));

            var basePath = _baseUrlPath.TrimEnd('/');
            return Task.FromResult($"{basePath}/{token}");
        }

        private string GetSafeFilePath(string key)
        {
            var normalizedKey = (key ?? string.Empty).Replace('\\', '/').TrimStart('/');
            var rootFull = Path.GetFullPath(_rootPath);
            if (!rootFull.EndsWith(Path.DirectorySeparatorChar))
                rootFull += Path.DirectorySeparatorChar;
            var combinedFull = Path.GetFullPath(Path.Combine(rootFull, normalizedKey.Replace('/', Path.DirectorySeparatorChar)));

            if (!combinedFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Invalid storage key path.");

            return combinedFull;
        }

        private sealed class FileToken
        {
            public string Key { get; set; } = string.Empty;
            public string UserId { get; set; } = string.Empty;
            public DateTimeOffset ExpiresAtUtc { get; set; }
        }
    }
}

