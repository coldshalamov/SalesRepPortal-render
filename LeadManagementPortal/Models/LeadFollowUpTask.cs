using System.ComponentModel.DataAnnotations;

namespace LeadManagementPortal.Models
{
    public class LeadFollowUpTask
    {
        public int Id { get; set; }

        [Required]
        public string LeadId { get; set; } = string.Empty;
        public virtual Lead? Lead { get; set; }

        [Required]
        [StringLength(32)]
        public string Type { get; set; } = "call";

        [Required]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        public DateTime? DueDate { get; set; }

        public bool IsCompleted { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        [StringLength(450)]
        public string? CreatedById { get; set; }

        [StringLength(450)]
        public string? CompletedById { get; set; }
    }
}
