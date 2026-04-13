using LeadManagementPortal.Controllers;
using LeadManagementPortal.Models;

namespace LeadManagementPortal.Services
{
    public interface ICommissionControlPlaneService
    {
        Task<ImportBatch> CreateBatchFromLegacySalesAsync(
            IEnumerable<SalesIngestRecordRequest> records,
            string sourceSystem,
            CancellationToken cancellationToken = default);

        Task<ImportBatch> CreateBatchFromRawRowsAsync(
            string sourceSystem,
            IEnumerable<IDictionary<string, string?>> rows,
            int? importProfileId,
            string? uploadedById,
            string? sourceFileName,
            CancellationToken cancellationToken = default);

        Task EvaluateBatchAsync(int batchId, CancellationToken cancellationToken = default);
        Task EvaluateRowAsync(int rowId, CancellationToken cancellationToken = default);
        Task PostReadyRowsAsync(int batchId, string postedById, CancellationToken cancellationToken = default);
        Task<CommissionStatementSummary> BuildStatementAsync(string beneficiaryId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<OutstandingPayoutItem>> GetOutstandingItemsAsync(string beneficiaryId, CancellationToken cancellationToken = default);
        Task<PayoutBatch> CreatePayoutBatchAsync(
            string createdById,
            string reference,
            string? notes,
            IEnumerable<PayoutSelectionRequest> selections,
            CancellationToken cancellationToken = default);
    }
}
