using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using Microsoft.EntityFrameworkCore;

namespace LeadManagementPortal.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly ApplicationDbContext _context;

        public CustomerService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Customer?> GetByIdAsync(string id)
        {
            return await _context.Customers
                .Include(c => c.ConvertedBy)
                .Include(c => c.SalesRep).ThenInclude(u => u!.SalesOrg)
                .Include(c => c.SalesGroup)
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        }

        public async Task<Customer?> GetAccessibleByIdAsync(string id, string userId, string userRole)
        {
            var query = _context.Customers
                .Include(c => c.ConvertedBy)
                .Include(c => c.SalesRep).ThenInclude(u => u!.SalesOrg)
                .Include(c => c.SalesGroup)
                .Where(c => c.Id == id && !c.IsDeleted)
                .AsQueryable();

            if (userRole == UserRoles.OrganizationAdmin)
            {
                return await query.FirstOrDefaultAsync();
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return null;
            }

            if (userRole == UserRoles.GroupAdmin)
            {
                if (user.SalesGroupId == null) return null;
                query = query.Where(c => c.SalesGroupId == user.SalesGroupId);
            }
            else if (userRole == UserRoles.SalesOrgAdmin)
            {
                if (user.SalesOrgId == null) return null;
                query = query.Where(c => c.SalesRep != null && c.SalesRep.SalesOrgId == user.SalesOrgId);
            }
            else if (userRole == UserRoles.SalesRep)
            {
                query = query.Where(c => c.ConvertedById == userId || c.SalesRepId == userId);
            }
            else
            {
                return null;
            }

            return await query.FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<Customer>> GetAllAsync()
        {
            return await _context.Customers
                .Include(c => c.ConvertedBy)
                .Include(c => c.SalesRep)
                .Include(c => c.SalesGroup)
                .Where(c => !c.IsDeleted)
                .OrderByDescending(c => c.ConversionDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<Customer>> GetByUserAsync(string userId, string userRole)
        {
            var query = _context.Customers
                .Include(c => c.ConvertedBy)
                .Include(c => c.SalesRep)
                .Include(c => c.SalesGroup)
                .Where(c => !c.IsDeleted)
                .AsQueryable();

            if (userRole == UserRoles.OrganizationAdmin)
            {
                // Organization admin sees all customers
                return await query.OrderByDescending(c => c.ConversionDate).ToListAsync();
            }
            else if (userRole == UserRoles.GroupAdmin)
            {
                // Group admin sees all customers in their group
                var user = await _context.Users.FindAsync(userId);
                if (user?.SalesGroupId != null)
                {
                    query = query.Where(c => c.SalesGroupId == user.SalesGroupId);
                }
            }
            else if (userRole == UserRoles.SalesOrgAdmin)
            {
                // Sales Org Admin sees customers assigned to reps in their org
                var user = await _context.Users.FindAsync(userId);
                if (user?.SalesOrgId != null)
                {
                    query = query.Where(c => c.SalesRep != null && c.SalesRep.SalesOrgId == user.SalesOrgId);
                }
            }
            else if (userRole == UserRoles.SalesRep)
            {
                // Sales rep sees customers they converted OR customers belonging to them
                query = query.Where(c => c.ConvertedById == userId || c.SalesRepId == userId);
            }

            return await query.OrderByDescending(c => c.ConversionDate).ToListAsync();
        }

        public async Task<IEnumerable<Customer>> GetBySalesGroupAsync(string salesGroupId)
        {
            return await _context.Customers
                .Include(c => c.ConvertedBy)
                .Include(c => c.SalesRep)
                .Include(c => c.SalesGroup)
                .Where(c => c.SalesGroupId == salesGroupId && !c.IsDeleted)
                .OrderByDescending(c => c.ConversionDate)
                .ToListAsync();
        }

        public async Task<bool> CreateAsync(Customer customer)
        {
            try
            {
                // Duplicate attempt audit
                var isDup = await IsDuplicateAsync(customer);
                if (isDup)
                {
                    var actorEmail = customer.SalesRep?.Email ?? customer.ConvertedBy?.Email;
                    _context.CustomerAudits.Add(new CustomerAudit
                    {
                        UserId = customer.SalesRepId ?? customer.ConvertedById,
                        UserEmail = actorEmail,
                        Action = "DuplicateAttempt",
                        Term = customer.Company ?? customer.Email ?? customer.Phone,
                        TargetCustomerId = await _context.Customers
                            .Where(c => !c.IsDeleted && (
                                (!string.IsNullOrWhiteSpace(customer.Email) && c.Email == customer.Email) ||
                                (!string.IsNullOrWhiteSpace(customer.Phone) && c.Phone == customer.Phone) ||
                                (!string.IsNullOrWhiteSpace(customer.Company) && c.Company == customer.Company)
                            ))
                            .Select(c => c.Id)
                            .FirstOrDefaultAsync(),
                        OccurredAtUtc = DateTime.UtcNow
                    });
                }
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpdateAsync(Customer customer)
        {
            try
            {
                var existing = await _context.Customers.FirstOrDefaultAsync(c => c.Id == customer.Id && !c.IsDeleted);
                if (existing == null) return false;

                // Only update editable fields. Keep conversion/system fields unchanged.
                existing.FirstName = customer.FirstName;
                existing.LastName = customer.LastName;
                existing.Email = customer.Email;
                existing.Phone = customer.Phone;
                existing.Company = customer.Company;
                existing.Address = customer.Address;
                existing.City = customer.City;
                existing.State = customer.State;
                existing.ZipCode = customer.ZipCode;
                existing.Notes = customer.Notes;
                existing.SalesRepId = customer.SalesRepId;
                existing.SalesGroupId = customer.SalesGroupId;

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<IEnumerable<Customer>> SearchAsync(string searchTerm, string userId, string userRole)
        {
            var query = _context.Customers
                .Include(c => c.ConvertedBy)
                .Include(c => c.SalesRep)
                .Include(c => c.SalesGroup)
                .Where(c => !c.IsDeleted)
                .AsQueryable();

            // Apply role-based filtering
            if (userRole == UserRoles.GroupAdmin)
            {
                var user = await _context.Users.FindAsync(userId);
                if (user?.SalesGroupId != null)
                {
                    query = query.Where(c => c.SalesGroupId == user.SalesGroupId);
                }
            }
            else if (userRole == UserRoles.SalesOrgAdmin)
            {
                var user = await _context.Users.FindAsync(userId);
                if (user?.SalesOrgId != null)
                {
                    query = query.Where(c => c.SalesRep != null && c.SalesRep.SalesOrgId == user.SalesOrgId);
                }
            }
            else if (userRole == UserRoles.SalesRep)
            {
                query = query.Where(c => c.ConvertedById == userId || c.SalesRepId == userId);
            }

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                query = query.Where(c =>
                    c.FirstName.ToLower().Contains(searchTerm) ||
                    c.LastName.ToLower().Contains(searchTerm) ||
                    c.Email.ToLower().Contains(searchTerm) ||
                    c.Phone.Contains(searchTerm) ||
                    (c.Company != null && c.Company.ToLower().Contains(searchTerm))
                );

                // Audit search
                var user = await _context.Users.FindAsync(userId);
                _context.CustomerAudits.Add(new CustomerAudit
                {
                    UserId = userId,
                    UserEmail = user?.Email,
                    Action = "Search",
                    Term = searchTerm,
                    OccurredAtUtc = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }

            return await query.OrderByDescending(c => c.ConversionDate).ToListAsync();
        }

        public async Task<bool> SoftDeleteAsync(string id, string deletedByUserId)
        {
            try
            {
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
                if (customer == null) return false;

                customer.IsDeleted = true;
                customer.DeletedDate = DateTime.UtcNow;

                // Mark corresponding lead as Lost
                if (!string.IsNullOrWhiteSpace(customer.OriginalLeadId))
                {
                    var lead = await _context.Leads.FirstOrDefaultAsync(l => l.Id == customer.OriginalLeadId);
                    if (lead != null)
                    {
                        lead.Status = LeadStatus.Lost;
                        lead.IsExpired = false;
                        lead.ConvertedDate = null;
                    }
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> IsDuplicateAsync(Customer customer)
        {
            return await _context.Customers.AnyAsync(c => !c.IsDeleted && (
                (!string.IsNullOrWhiteSpace(customer.Email) && c.Email == customer.Email) ||
                (!string.IsNullOrWhiteSpace(customer.Phone) && c.Phone == customer.Phone) ||
                (!string.IsNullOrWhiteSpace(customer.Company) && c.Company == customer.Company)
            ));
        }
    }
}
