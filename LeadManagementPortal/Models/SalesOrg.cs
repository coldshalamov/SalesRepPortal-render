using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LeadManagementPortal.Models
{
    public class SalesOrg
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        public string? LogoUrl { get; set; }

        [Required]
        [ForeignKey(nameof(SalesGroup))]
        public string SalesGroupId { get; set; } = string.Empty;

        public SalesGroup? SalesGroup { get; set; }

        public ICollection<ApplicationUser>? SalesReps { get; set; }
    }
}
