using Microsoft.AspNetCore.Identity;
using System.Linq;

namespace LeadManagementPortal.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? SalesGroupId { get; set; }
        public virtual SalesGroup? SalesGroup { get; set; }
        public int? SalesOrgId { get; set; }
        public virtual SalesOrg? SalesOrg { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        public virtual ICollection<Lead> Leads { get; set; } = new List<Lead>();
        public virtual CommissionDeal? CommissionDeal { get; set; }
        public virtual CommissionLink? SponsorLink { get; set; }
        public virtual ICollection<CommissionLink> SponsoredDownlines { get; set; } = new List<CommissionLink>();
        public virtual ICollection<SaleRecord> SaleRecords { get; set; } = new List<SaleRecord>();
        public virtual ICollection<CommissionLedger> CommissionLedgers { get; set; } = new List<CommissionLedger>();

        public string FullName => string.Join(" ", new[] { FirstName, LastName }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
    }
}
