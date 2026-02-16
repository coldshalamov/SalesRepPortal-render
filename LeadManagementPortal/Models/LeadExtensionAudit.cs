namespace LeadManagementPortal.Models
{
    public class LeadExtensionAudit
    {
        public int Id { get; set; }
        public string LeadId { get; set; } = string.Empty;
        public string GrantedById { get; set; } = string.Empty;
        public DateTime GrantedAtUtc { get; set; } = DateTime.UtcNow;
        public int DaysAdded { get; set; }
        public DateTime PreviousExpiry { get; set; }
        public DateTime NewExpiry { get; set; }

        public virtual Lead? Lead { get; set; }
    }
}
