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
        Task<List<LeadExpiryCandidate>> GetLeadsExpiringSoonAsync(DateTime utcNow, int daysThreshold);
        Task<List<LeadExpiryCandidate>> ExpireOldLeadsAsync(DateTime utcNow);
        Task<IEnumerable<Lead>> SearchAsync(string searchTerm, string userId, string userRole);
        Task<IEnumerable<Lead>> SearchTopAsync(string searchTerm, string userId, string userRole, int maxResults);
        Task<Dictionary<string, List<LeadFollowUpTask>>> GetFollowUpsForLeadsAsync(IEnumerable<string> leadIds, string userId, string userRole);
        Task<List<LeadFollowUpTask>> GetFollowUpsForLeadAsync(string leadId, string userId, string userRole);
        Task<LeadFollowUpTask?> AddFollowUpAsync(string leadId, string userId, string userRole, string type, string description, DateTime? dueDate);
        Task<bool> CompleteFollowUpAsync(string leadId, int followUpId, string userId, string userRole);
        Task<int> DeleteFollowUpsAsync(string leadId, IEnumerable<int> followUpIds, string userId, string userRole);
        Task<int> GetOverdueFollowUpCountAsync(string userId, string userRole);
    }
}
