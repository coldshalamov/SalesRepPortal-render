namespace LeadManagementPortal.Models
{
    public class CommissionLink
    {
        public string DownlineId { get; set; } = string.Empty;
        public virtual ApplicationUser? Downline { get; set; }

        public string SponsorId { get; set; } = string.Empty;
        public virtual ApplicationUser? Sponsor { get; set; }
    }
}
