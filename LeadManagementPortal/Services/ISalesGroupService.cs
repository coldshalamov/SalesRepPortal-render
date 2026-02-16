using LeadManagementPortal.Models;

namespace LeadManagementPortal.Services
{
    public interface ISalesGroupService
    {
        Task<SalesGroup?> GetByIdAsync(string id);
        Task<IEnumerable<SalesGroup>> GetAllAsync();
        Task<bool> CreateAsync(SalesGroup salesGroup);
        Task<bool> UpdateAsync(SalesGroup salesGroup);
        Task<bool> DeleteAsync(string id);
        Task<IEnumerable<ApplicationUser>> GetGroupMembersAsync(string groupId);
    }
}
