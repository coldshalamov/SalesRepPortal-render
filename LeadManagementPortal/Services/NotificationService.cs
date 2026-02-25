using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using Microsoft.EntityFrameworkCore;

namespace LeadManagementPortal.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;

        public NotificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task NotifyUserAsync(string userId, string type, string title, string message, string? link = null)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = userId,
                Role = null,
                Type = type,
                Title = title,
                Message = message,
                Link = link,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }

        public async Task NotifyRoleAsync(string role, string type, string title, string message, string? link = null)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = null,
                Role = role,
                Type = type,
                Title = title,
                Message = message,
                Link = link,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }

        public async Task<List<Notification>> GetForUserAsync(string userId, string role, int limit = 50, bool unreadOnly = false)
        {
            var query = _context.Notifications
                .Where(n => n.UserId == userId || n.Role == role)
                .AsQueryable();

            if (unreadOnly)
                query = query.Where(n => !n.IsRead);

            return await query
                .OrderByDescending(n => n.CreatedAt)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync(string userId, string role)
        {
            return await _context.Notifications
                .CountAsync(n => (n.UserId == userId || n.Role == role) && !n.IsRead);
        }

        public async Task<bool> MarkReadAsync(int notificationId, string userId, string role)
        {
            var n = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && (n.UserId == userId || n.Role == role));
            if (n == null) return false;

            n.IsRead = true;
            n.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> MarkUnreadAsync(int notificationId, string userId, string role)
        {
            var n = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && (n.UserId == userId || n.Role == role));
            if (n == null) return false;

            n.IsRead = false;
            n.ReadAt = null;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> MarkAllReadAsync(string userId, string role)
        {
            var unread = await _context.Notifications
                .Where(n => (n.UserId == userId || n.Role == role) && !n.IsRead)
                .ToListAsync();

            foreach (var n in unread)
            {
                n.IsRead = true;
                n.ReadAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task CleanupOldAsync(int daysOld = 30)
        {
            var cutoff = DateTime.UtcNow.AddDays(-daysOld);
            var old = await _context.Notifications
                .Where(n => n.IsRead && n.ReadAt < cutoff)
                .ToListAsync();

            _context.Notifications.RemoveRange(old);
            await _context.SaveChangesAsync();
        }
    }
}
