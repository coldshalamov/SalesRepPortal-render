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

        public string FullName => string.Join(" ", new[] { FirstName, LastName }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
    }
}
