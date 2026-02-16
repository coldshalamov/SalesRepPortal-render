using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LeadManagementPortal.Services
{
    public class SalesOrgService : ISalesOrgService
    {
        private readonly ApplicationDbContext _db;
        public SalesOrgService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IEnumerable<SalesOrg>> GetAllAsync()
        {
            return await _db.SalesOrgs.Include(o => o.SalesGroup).ToListAsync();
        }

        public async Task<SalesOrg?> GetByIdAsync(int id)
        {
            return await _db.SalesOrgs.Include(o => o.SalesGroup).FirstOrDefaultAsync(o => o.Id == id);
        }

        public async Task<IEnumerable<SalesOrg>> GetByGroupIdAsync(string salesGroupId)
        {
            return await _db.SalesOrgs.Where(o => o.SalesGroupId == salesGroupId).ToListAsync();
        }

        public async Task<SalesOrg> CreateAsync(SalesOrg org)
        {
            _db.SalesOrgs.Add(org);
            await _db.SaveChangesAsync();
            return org;
        }

        public async Task<SalesOrg> UpdateAsync(SalesOrg org)
        {
            _db.SalesOrgs.Update(org);
            await _db.SaveChangesAsync();
            return org;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var existing = await _db.SalesOrgs.FindAsync(id);
            if (existing == null) return false;
            _db.SalesOrgs.Remove(existing);
            await _db.SaveChangesAsync();
            return true;
        }
    }
}
