using System.ComponentModel.DataAnnotations;

namespace LeadManagementPortal.Models
{
    public class Product
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public virtual ICollection<Lead> Leads { get; set; } = new List<Lead>();
    }
}
