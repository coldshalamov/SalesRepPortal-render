using System.ComponentModel.DataAnnotations;

namespace LeadManagementPortal.Models.ViewModels
{
    public class CustomerEditViewModel
    {
        [Required]
        public string Id { get; set; } = string.Empty;

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

        [Display(Name = "Sales Rep")]
        public string? SalesRepId { get; set; }
    }
}

