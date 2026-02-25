using System.Threading.Tasks;

namespace LeadManagementPortal.Services
{
    public interface IFileStorageService
    {
        Task<string> UploadAsync(Stream content, string contentType, string key, CancellationToken ct = default);
        Task<string> GetPreSignedDownloadUrlAsync(string key, TimeSpan expires, CancellationToken ct = default);
        Task<bool> ExistsAsync(string key, CancellationToken ct = default);
        Task<bool> DeleteAsync(string key, CancellationToken ct = default);
    }
}
