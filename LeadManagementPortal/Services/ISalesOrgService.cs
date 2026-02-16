using LeadManagementPortal.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LeadManagementPortal.Services
{
    public interface ISalesOrgService
    {
        Task<IEnumerable<SalesOrg>> GetAllAsync();
        Task<SalesOrg?> GetByIdAsync(int id);
        Task<SalesOrg> CreateAsync(SalesOrg org);
        Task<SalesOrg> UpdateAsync(SalesOrg org);
        Task<bool> DeleteAsync(int id);
        Task<IEnumerable<SalesOrg>> GetByGroupIdAsync(string salesGroupId);
    }
}
