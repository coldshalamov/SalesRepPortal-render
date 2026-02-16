using LeadManagementPortal.Models;

namespace LeadManagementPortal.Services
{
    public interface ILeadAuditService
    {
        Task LogAsync(string leadId, string? userId, string action, string? details = null);
        Task LogAsync(Lead lead, string? userId, string action, string? details = null)
            => LogAsync(lead.Id, userId, action, details);
    }
}
