using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using Microsoft.EntityFrameworkCore;

namespace LeadManagementPortal.Services
{
    public class ProductService : IProductService
    {
        private readonly ApplicationDbContext _context;

        public ProductService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Product>> GetAllAsync(bool activeOnly = true)
        {
            var query = _context.Products.AsQueryable();
            if (activeOnly)
            {
                query = query.Where(p => p.IsActive);
            }
            return await query.OrderBy(p => p.Name).ToListAsync();
        }

        public async Task<List<Product>> GetByIdsAsync(IEnumerable<string> ids)
        {
            var idList = ids?.Where(i => !string.IsNullOrWhiteSpace(i)).ToList() ?? new List<string>();
            if (idList.Count == 0) return new List<Product>();
            return await _context.Products.Where(p => idList.Contains(p.Id)).ToListAsync();
        }
    }
}
