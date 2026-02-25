using System;
using System.Linq;
using System.Threading.Tasks;
using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using LeadManagementPortal.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LeadManagementPortal.Tests
{
    public class NotificationServiceDeduplicationTests
    {
        private static ApplicationDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task NotifyUserDedupedAsync_SuppressesDuplicateWithinWindow_ByTypeAndLink()
        {
            using var context = GetInMemoryDbContext();
            var svc = new NotificationService(context);

            var inserted1 = await svc.NotifyUserDedupedAsync(
                userId: "user-1",
                type: "lead_expiring_soon",
                title: "Lead Expiring Soon",
                message: "First",
                link: "/Leads/Details/lead-1",
                dedupeWindow: TimeSpan.FromHours(24));

            var inserted2 = await svc.NotifyUserDedupedAsync(
                userId: "user-1",
                type: "lead_expiring_soon",
                title: "Lead Expiring Soon",
                message: "Second",
                link: "/Leads/Details/lead-1",
                dedupeWindow: TimeSpan.FromHours(24));

            Assert.True(inserted1);
            Assert.False(inserted2);
            Assert.Equal(1, await context.Notifications.CountAsync());
        }

        [Fact]
        public async Task NotifyUserDedupedAsync_AllowsNewAfterWindowExpires()
        {
            using var context = GetInMemoryDbContext();
            var svc = new NotificationService(context);

            var inserted1 = await svc.NotifyUserDedupedAsync(
                userId: "user-1",
                type: "lead_expiring_soon",
                title: "Lead Expiring Soon",
                message: "First",
                link: "/Leads/Details/lead-1",
                dedupeWindow: TimeSpan.FromDays(1));
            Assert.True(inserted1);

            var existing = await context.Notifications.SingleAsync();
            existing.CreatedAt = DateTime.UtcNow.AddDays(-2);
            await context.SaveChangesAsync();

            var inserted2 = await svc.NotifyUserDedupedAsync(
                userId: "user-1",
                type: "lead_expiring_soon",
                title: "Lead Expiring Soon",
                message: "Second",
                link: "/Leads/Details/lead-1",
                dedupeWindow: TimeSpan.FromDays(1));

            Assert.True(inserted2);
            Assert.Equal(2, await context.Notifications.CountAsync());
        }

        [Fact]
        public async Task CleanupOldAsync_IncludeUnread_RemovesUnreadOlderThanCutoff()
        {
            using var context = GetInMemoryDbContext();
            var svc = new NotificationService(context);

            context.Notifications.Add(new Notification
            {
                UserId = "user-1",
                Type = "t1",
                Title = "Unread Old",
                Message = "m",
                Link = "/x",
                IsRead = false,
                CreatedAt = DateTime.UtcNow.AddDays(-90)
            });

            context.Notifications.Add(new Notification
            {
                UserId = "user-1",
                Type = "t2",
                Title = "Read Old",
                Message = "m",
                Link = "/y",
                IsRead = true,
                CreatedAt = DateTime.UtcNow.AddDays(-90),
                ReadAt = DateTime.UtcNow.AddDays(-90)
            });

            context.Notifications.Add(new Notification
            {
                UserId = "user-1",
                Type = "t3",
                Title = "Unread Recent",
                Message = "m",
                Link = "/z",
                IsRead = false,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            });

            await context.SaveChangesAsync();

            await svc.CleanupOldAsync(daysOld: 30, includeUnread: true);

            var remaining = await context.Notifications.OrderBy(n => n.Title).Select(n => n.Title).ToListAsync();
            Assert.Equal(new[] { "Unread Recent" }, remaining);
        }
    }
}

