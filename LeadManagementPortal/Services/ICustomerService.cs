using LeadManagementPortal.Models;

namespace LeadManagementPortal.Services
{
    public interface ICustomerService
    {
        Task<Customer?> GetByIdAsync(string id);
        Task<Customer?> GetAccessibleByIdAsync(string id, string userId, string userRole);
        Task<IEnumerable<Customer>> GetAllAsync();
        Task<IEnumerable<Customer>> GetByUserAsync(string userId, string userRole);
        Task<IEnumerable<Customer>> GetBySalesGroupAsync(string salesGroupId);
        Task<bool> CreateAsync(Customer customer);
        Task<bool> UpdateAsync(Customer customer);
        Task<IEnumerable<Customer>> SearchAsync(string searchTerm, string userId, string userRole);
        Task<bool> SoftDeleteAsync(string id, string deletedByUserId);
        Task<bool> IsDuplicateAsync(Customer customer);
    }
}
