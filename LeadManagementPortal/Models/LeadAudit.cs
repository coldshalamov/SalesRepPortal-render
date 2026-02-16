using System;

namespace LeadManagementPortal.Models
{
    public class LeadAudit
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string LeadId { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public string? UserEmail { get; set; }
        public string Action { get; set; } = "Update"; // Create, Update, Reassign, Convert, GrantExtension, Expire
        public string? Details { get; set; }
        public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    }
}
