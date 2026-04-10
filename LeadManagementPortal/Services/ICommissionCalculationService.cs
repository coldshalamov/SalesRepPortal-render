using LeadManagementPortal.Models;

namespace LeadManagementPortal.Services
{
    public interface ICommissionCalculationService
    {
        Task<IReadOnlyList<CommissionLedger>> CalculateForSaleAsync(SaleRecord saleRecord, CancellationToken cancellationToken = default);
    }
}
