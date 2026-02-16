using LeadManagementPortal.Models;

namespace LeadManagementPortal.Services
{
    public interface ISettingsService
    {
        Task<SystemSettings> GetAsync();
        Task UpdateAsync(SystemSettings settings);
    }
}
