using System.ComponentModel.DataAnnotations;

namespace LeadManagementPortal.Models
{
    public class Customer
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

        [StringLength(200)]
        public string? Company { get; set; }

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

        public string ConvertedById { get; set; } = string.Empty;
        public virtual ApplicationUser ConvertedBy { get; set; } = null!;

        public string? SalesRepId { get; set; }
        public virtual ApplicationUser? SalesRep { get; set; }

        public string? SalesGroupId { get; set; }
        public virtual SalesGroup? SalesGroup { get; set; }

        public DateTime ConversionDate { get; set; } = DateTime.UtcNow;
        public string OriginalLeadId { get; set; } = string.Empty;

        public DateTime LeadCreatedDate { get; set; }
        public int DaysToConvert { get; set; }

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedDate { get; set; }
    }
}
