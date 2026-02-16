using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace LeadManagementPortal.Models
{
    public class Lead
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(200)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Phone]
        [StringLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Company { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Address { get; set; }

        [StringLength(100)]
        public string? City { get; set; }

        [StringLength(100)]
        public string? State { get; set; }

        [StringLength(20)]
        public string? ZipCode { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public LeadStatus Status { get; set; } = LeadStatus.New;

        public string AssignedToId { get; set; } = string.Empty;
        public virtual ApplicationUser? AssignedTo { get; set; }

        public string? SalesGroupId { get; set; }
        public virtual SalesGroup? SalesGroup { get; set; }

        public int? SalesOrgId { get; set; }
        public virtual SalesOrg? SalesOrg { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime ExpiryDate { get; set; }
        public DateTime? ConvertedDate { get; set; }
        public DateTime? LastContactDate { get; set; }

        public bool IsExpired { get; set; } = false;
        public bool IsExtended { get; set; } = false;
        public DateTime? ExtensionGrantedDate { get; set; }
        public string? ExtensionGrantedBy { get; set; }

        public string CreatedById { get; set; } = string.Empty;
        public virtual ApplicationUser? CreatedBy { get; set; }

        public virtual ICollection<Product> Products { get; set; } = new List<Product>();

        public virtual ICollection<LeadDocument> Documents { get; set; } = new List<LeadDocument>();

        public int DaysRemaining
        {
            get
            {
                if (Status == LeadStatus.Converted || IsExpired)
                    return 0;
                
                var days = (ExpiryDate - DateTime.UtcNow).Days;
                return days > 0 ? days : 0;
            }
        }

        public string UrgencyLevel
        {
            get
            {
                if (Status == LeadStatus.Converted || IsExpired)
                    return "None";

                var days = DaysRemaining;
                if (days <= 5)
                    return "Critical";
                else if (days <= 10)
                    return "High";
                else
                    return "Low";
            }
        }
    }

    public enum LeadStatus
    {
        New,
        Contacted,
        Qualified,
        Proposal,
        Negotiation,
        Converted,
        Lost,
        Expired
    }
}
