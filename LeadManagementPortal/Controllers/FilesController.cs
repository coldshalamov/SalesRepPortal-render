using System.Security.Claims;
using System.Text;
using System.Text.Json;
using LeadManagementPortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace LeadManagementPortal.Controllers
{
    [Authorize]
    [Route("files")]
    public class FilesController : Controller
    {
        private readonly string _rootPath;
        private readonly IDataProtector _protector;
        private readonly FileExtensionContentTypeProvider _contentTypes = new();

        public FilesController(
            IWebHostEnvironment env,
            IOptions<LocalStorageOptions> options,
            IDataProtectionProvider dataProtectionProvider)
        {
            var configuredRoot = options.Value.RootPath?.Trim();
            _rootPath = string.IsNullOrWhiteSpace(configuredRoot)
                ? Path.Combine(env.ContentRootPath, "App_Data", "uploads")
                : configuredRoot;

            _protector = dataProtectionProvider.CreateProtector("LeadManagementPortal.LocalFiles.v1");
        }

        [HttpGet("{token}")]
        public async Task<IActionResult> Get(string token, CancellationToken ct)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(currentUserId))
                return Forbid();

            FileToken payload;
            try
            {
                var protectedText = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
                var json = _protector.Unprotect(protectedText);
                payload = JsonSerializer.Deserialize<FileToken>(json) ?? new FileToken();
            }
            catch
            {
                return NotFound();
            }

            if (payload.ExpiresAtUtc <= DateTimeOffset.UtcNow)
                return NotFound();

            if (!string.Equals(payload.UserId, currentUserId, StringComparison.Ordinal))
                return Forbid();

            var filePath = GetSafeFilePath(payload.Key);
            if (!System.IO.File.Exists(filePath))
                return NotFound();

            var fileName = Path.GetFileName(payload.Key.Replace('\\', '/'));
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = "download";

            var contentType = "application/octet-stream";
            if (_contentTypes.TryGetContentType(fileName, out var ctGuess))
                contentType = ctGuess;

            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            return File(stream, contentType, fileName);
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

