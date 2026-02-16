using LeadManagementPortal.Models;

namespace LeadManagementPortal.Services
{
    public interface ILeadDocumentService
    {
        Task<LeadDocument> AddAsync(string leadId, string fileName, string contentType, long sizeBytes, Stream content, string? uploadedByUserId, CancellationToken ct = default);
        Task<IReadOnlyList<LeadDocument>> ListAsync(string leadId, CancellationToken ct = default);
        Task<LeadDocument?> GetAsync(int id, CancellationToken ct = default);
        Task<string?> GetDownloadUrlAsync(int id, TimeSpan? expires = null, CancellationToken ct = default);
    }
}
