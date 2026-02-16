using LeadManagementPortal.Models;

namespace LeadManagementPortal.Services
{
    public interface IProductService
    {
        Task<IEnumerable<Product>> GetAllAsync(bool activeOnly = true);
        Task<List<Product>> GetByIdsAsync(IEnumerable<string> ids);
    }
}
