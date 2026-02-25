using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using Microsoft.EntityFrameworkCore;

namespace LeadManagementPortal.Services
{
    public class LeadAuditService : ILeadAuditService
    {
        private readonly ApplicationDbContext _context;
        public LeadAuditService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task LogAsync(string leadId, string? userId, string action, string? details = null)
        {
            string? userEmail = null;
            if (!string.IsNullOrWhiteSpace(userId))
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                userEmail = user?.Email;
            }
            var audit = new LeadAudit
            {
                LeadId = leadId,
                UserId = userId,
                UserEmail = userEmail,
                Action = action,
                Details = details,
                OccurredAtUtc = DateTime.UtcNow
            };
            _context.LeadAudits.Add(audit);
            await _context.SaveChangesAsync();
        }

        public async Task<List<LeadAudit>> GetForLeadAsync(string leadId)
        {
            return await _context.LeadAudits
                .Where(a => a.LeadId == leadId)
                .OrderByDescending(a => a.OccurredAtUtc)
                .ToListAsync();
        }
    }
}
