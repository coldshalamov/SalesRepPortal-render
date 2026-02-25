namespace LeadManagementPortal.Models
{
    /// <summary>
    /// Represents an in-app notification for a specific user or role.
    /// </summary>
    public class Notification
    {
        public int Id { get; set; }

        /// <summary>
        /// Target user ID. Null means this is a role-broadcast notification.
        /// </summary>
        public string? UserId { get; set; }
        public virtual ApplicationUser? User { get; set; }

        /// <summary>
        /// Target role (e.g., "OrganizationAdmin"). Null means this is a user-specific notification.
        /// </summary>
        public string? Role { get; set; }

        /// <summary>
        /// Notification type key (e.g., "lead_assigned", "lead_expired").
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>Short headline shown in the bell dropdown.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Full message body.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Optional URL to navigate to when the notification is clicked.</summary>
        public string? Link { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ReadAt { get; set; }
    }
}
