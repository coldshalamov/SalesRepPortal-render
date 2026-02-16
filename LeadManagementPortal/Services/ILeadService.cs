using LeadManagementPortal.Models;

namespace LeadManagementPortal.Services
{
    public interface ILeadService
    {
        Task<Lead?> GetByIdAsync(string id);
        Task<IEnumerable<Lead>> GetAllAsync();
        Task<IEnumerable<Lead>> GetByUserAsync(string userId, string userRole);
        Task<IEnumerable<Lead>> GetBySalesGroupAsync(string salesGroupId);
        Task<bool> CreateAsync(Lead lead);
        Task<bool> UpdateAsync(Lead lead);
        Task<bool> DeleteAsync(string id);
        Task<bool> IsPhoneOrEmailExistsAsync(string phone, string email, string? excludeLeadId = null);
        Task<bool> CanRegisterLeadAsync(string? company, string? address = null, string? city = null, string? state = null, string? zip = null, string? excludeLeadId = null);
        Task<bool> CanRegisterLeadForGroupAsync(string? company, string? salesGroupId, string? address = null, string? city = null, string? state = null, string? zip = null);
        Task<bool> ConvertToCustomerAsync(string leadId, string userId);
        Task<bool> GrantExtensionAsync(string leadId, string grantedBy);
        Task ExpireOldLeadsAsync();
        Task<IEnumerable<Lead>> SearchAsync(string searchTerm, string userId, string userRole);
    }
}
