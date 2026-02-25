using LeadManagementPortal.Models;

namespace LeadManagementPortal.Services
{
    public interface INotificationService
    {
        /// <summary>Send a notification to a specific user.</summary>
        Task NotifyUserAsync(string userId, string type, string title, string message, string? link = null);

        /// <summary>Send a notification to all users with a given role.</summary>
        Task NotifyRoleAsync(string role, string type, string title, string message, string? link = null);

        /// <summary>Get all notifications visible to a user (own + role-broadcast).</summary>
        Task<List<Notification>> GetForUserAsync(string userId, string role, int limit = 50, bool unreadOnly = false);

        /// <summary>Count of unread notifications for a user.</summary>
        Task<int> GetUnreadCountAsync(string userId, string role);

        /// <summary>Mark a single notification as read (ownership enforced).</summary>
        Task<bool> MarkReadAsync(int notificationId, string userId, string role);

        /// <summary>Mark a single notification as unread (ownership enforced).</summary>
        Task<bool> MarkUnreadAsync(int notificationId, string userId, string role);

        /// <summary>Mark all of a user's notifications as read.</summary>
        Task<bool> MarkAllReadAsync(string userId, string role);

        /// <summary>Delete read notifications older than the given cutoff.</summary>
        Task CleanupOldAsync(int daysOld = 30);
    }
}
