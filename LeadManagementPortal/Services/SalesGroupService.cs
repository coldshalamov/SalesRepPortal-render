using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using Microsoft.EntityFrameworkCore;

namespace LeadManagementPortal.Services
{
    public class SalesGroupService : ISalesGroupService
    {
        private readonly ApplicationDbContext _context;

        public SalesGroupService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<SalesGroup?> GetByIdAsync(string id)
        {
            return await _context.SalesGroups
                .Include(g => g.GroupAdmin)
                .Include(g => g.SalesReps)
                .Include(g => g.SalesOrgs)
                .FirstOrDefaultAsync(g => g.Id == id);
        }

        public async Task<IEnumerable<SalesGroup>> GetAllAsync()
        {
            return await _context.SalesGroups
                .Include(g => g.GroupAdmin)
                .Include(g => g.SalesReps)
                .Include(g => g.SalesOrgs)
                .Where(g => g.IsActive)
                .OrderBy(g => g.Name)
                .ToListAsync();
        }

        public async Task<bool> CreateAsync(SalesGroup salesGroup)
        {
            try
            {
                _context.SalesGroups.Add(salesGroup);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpdateAsync(SalesGroup salesGroup)
        {
            try
            {
                _context.SalesGroups.Update(salesGroup);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteAsync(string id)
        {
            try
            {
                var salesGroup = await _context.SalesGroups.FindAsync(id);
                if (salesGroup == null) return false;

                salesGroup.IsActive = false;
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<IEnumerable<ApplicationUser>> GetGroupMembersAsync(string groupId)
        {
            return await _context.Users
                .Where(u => u.SalesGroupId == groupId && u.IsActive)
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .ToListAsync();
        }
    }
}
