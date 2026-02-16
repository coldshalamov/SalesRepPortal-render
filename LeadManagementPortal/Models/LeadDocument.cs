using System.ComponentModel.DataAnnotations;

namespace LeadManagementPortal.Models
{
    public class LeadDocument
    {
        public int Id { get; set; }

        [Required]
        public string LeadId { get; set; } = string.Empty;

        [Required]
        [StringLength(260)]
        public string FileName { get; set; } = string.Empty;

        [StringLength(200)]
        public string? ContentType { get; set; }

        public long SizeBytes { get; set; }

        [Required]
        [StringLength(500)]
        public string StorageKey { get; set; } = string.Empty;

        public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;

        public string? UploadedByUserId { get; set; }
    }
}
