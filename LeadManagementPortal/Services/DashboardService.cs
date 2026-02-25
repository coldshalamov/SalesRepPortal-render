using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using Microsoft.EntityFrameworkCore;

namespace LeadManagementPortal.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _context;

        public DashboardService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardStats> GetDashboardStatsAsync(string userId, string userRole)
        {
            var leadsQuery = _context.Leads
                .Include(l => l.AssignedTo).ThenInclude(u => u!.SalesOrg)
                .AsQueryable();
            var customersQuery = _context.Customers
                .Include(c => c.SalesRep).ThenInclude(u => u!.SalesOrg)
                .Where(c => !c.IsDeleted)
                .AsQueryable();

            // Apply role-based filtering
            if (userRole == UserRoles.OrganizationAdmin)
            {
                // No filtering
            }
            else if (userRole == UserRoles.GroupAdmin)
            {
                var user = await _context.Users.FindAsync(userId);
                if (user?.SalesGroupId != null)
                {
                    leadsQuery = leadsQuery.Where(l => l.SalesGroupId == user.SalesGroupId);
                    customersQuery = customersQuery.Where(c => c.SalesGroupId == user.SalesGroupId);
                }
                else
                {
                    return new DashboardStats();
                }
            }
            else if (userRole == UserRoles.SalesOrgAdmin)
            {
                var user = await _context.Users.FindAsync(userId);
                if (user?.SalesOrgId != null)
                {
                    leadsQuery = leadsQuery.Where(l => l.AssignedTo != null && l.AssignedTo.SalesOrgId == user.SalesOrgId);
                    customersQuery = customersQuery.Where(c => c.SalesRep != null && c.SalesRep.SalesOrgId == user.SalesOrgId);
                }
                else
                {
                    return new DashboardStats();
                }
            }
            else if (userRole == UserRoles.SalesRep)
            {
                leadsQuery = leadsQuery.Where(l => l.AssignedToId == userId);
                customersQuery = customersQuery.Where(c => c.ConvertedById == userId);
            }
            else
            {
                return new DashboardStats();
            }

            var leads = await leadsQuery.ToListAsync();
            var customers = await customersQuery.ToListAsync();

            var totalLeads = leads.Count;
            var pendingLeads = leads.Count(l =>
                l.Status != LeadStatus.Converted &&
                l.Status != LeadStatus.Lost &&
                l.Status != LeadStatus.Expired &&
                !l.IsExpired);
            var convertedLeads = leads.Count(l => l.Status == LeadStatus.Converted);
            var expiredLeads = leads.Count(l => l.IsExpired);

            var criticalLeads = leads.Count(l => l.UrgencyLevel == "Critical" && l.Status != LeadStatus.Converted && !l.IsExpired);
            var highPriorityLeads = leads.Count(l => l.UrgencyLevel == "High" && l.Status != LeadStatus.Converted && !l.IsExpired);
            var lowPriorityLeads = leads.Count(l => l.UrgencyLevel == "Low" && l.Status != LeadStatus.Converted && !l.IsExpired);

            var totalCustomers = customers.Count;
            var conversionRate = totalLeads > 0 ? (decimal)convertedLeads / totalLeads * 100 : 0;
            var averageDaysToConvert = customers.Any() ? customers.Average(c => c.DaysToConvert) : 0;

            // Build last-30-day conversion trend
            var cutoff = DateTime.UtcNow.AddDays(-30);
            var trend = customers
                .Where(c => c.ConversionDate >= cutoff)
                .GroupBy(c => c.ConversionDate.Date)
                .OrderBy(g => g.Key)
                .Select(g => new ConversionDataPoint(g.Key.ToString("MMM dd"), g.Count()))
                .ToList();

            return new DashboardStats
            {
                TotalLeads = totalLeads,
                PendingLeads = pendingLeads,
                ConvertedLeads = convertedLeads,
                ExpiredLeads = expiredLeads,
                CriticalLeads = criticalLeads,
                HighPriorityLeads = highPriorityLeads,
                LowPriorityLeads = lowPriorityLeads,
                TotalCustomers = totalCustomers,
                ConversionRate = Math.Round(conversionRate, 2),
                AverageDaysToConvert = Math.Round(averageDaysToConvert, 1),
                ConversionTrend = trend
            };
        }
    }
}
