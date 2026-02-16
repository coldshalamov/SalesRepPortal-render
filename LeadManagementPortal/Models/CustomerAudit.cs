using System;

namespace LeadManagementPortal.Models
{
    public class CustomerAudit
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string? UserId { get; set; }
        public string Action { get; set; } = "Search"; // "Search" or "DuplicateAttempt"
        public string? Term { get; set; }
        public string? TargetCustomerId { get; set; }
        public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
        public string? UserEmail { get; set; }
    }
}
