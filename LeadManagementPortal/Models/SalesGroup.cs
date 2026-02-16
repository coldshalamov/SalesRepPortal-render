namespace LeadManagementPortal.Models
{
    public class SalesGroup
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? LogoUrl { get; set; }
        public string? GroupAdminId { get; set; }
        public virtual ApplicationUser? GroupAdmin { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        public virtual ICollection<SalesOrg> SalesOrgs { get; set; } = new List<SalesOrg>();
        public virtual ICollection<ApplicationUser> SalesReps { get; set; } = new List<ApplicationUser>();
        public virtual ICollection<Lead> Leads { get; set; } = new List<Lead>();
    }
}
