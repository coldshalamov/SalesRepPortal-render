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
    public class NotificationServiceBulkMarkTests
    {
        private static ApplicationDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task MarkReadBulkAsync_UpdatesOnlyUnreadVisibleNotifications()
        {
            using var context = GetInMemoryDbContext();
            var svc = new NotificationService(context);

            context.Notifications.AddRange(
                new Notification { Id = 1, UserId = "u1", Role = null, IsRead = false, CreatedAt = DateTime.UtcNow, Title = "t1", Message = "m", Type = "x" },
                new Notification { Id = 2, UserId = "u1", Role = null, IsRead = true, CreatedAt = DateTime.UtcNow, ReadAt = DateTime.UtcNow, Title = "t2", Message = "m", Type = "x" },
                new Notification { Id = 3, UserId = "u2", Role = null, IsRead = false, CreatedAt = DateTime.UtcNow, Title = "t3", Message = "m", Type = "x" },
                new Notification { Id = 4, UserId = null, Role = "SalesOrgAdmin", IsRead = false, CreatedAt = DateTime.UtcNow, Title = "t4", Message = "m", Type = "x" }
            );
            await context.SaveChangesAsync();

            var updated = await svc.MarkReadBulkAsync(
                notificationIds: new[] { 0, 1, 1, 2, 3, 4, 999 },
                userId: "u1",
                role: "SalesOrgAdmin");

            Assert.Equal(2, updated);

            var n1 = await context.Notifications.SingleAsync(n => n.Id == 1);
            var n2 = await context.Notifications.SingleAsync(n => n.Id == 2);
            var n3 = await context.Notifications.SingleAsync(n => n.Id == 3);
            var n4 = await context.Notifications.SingleAsync(n => n.Id == 4);

            Assert.True(n1.IsRead);
            Assert.NotNull(n1.ReadAt);

            Assert.True(n2.IsRead);
            Assert.NotNull(n2.ReadAt);

            Assert.False(n3.IsRead);
            Assert.Null(n3.ReadAt);

            Assert.True(n4.IsRead);
            Assert.NotNull(n4.ReadAt);
        }

        [Fact]
        public async Task MarkUnreadBulkAsync_UpdatesOnlyReadVisibleNotifications()
        {
            using var context = GetInMemoryDbContext();
            var svc = new NotificationService(context);

            context.Notifications.AddRange(
                new Notification { Id = 10, UserId = "u1", Role = null, IsRead = true, CreatedAt = DateTime.UtcNow, ReadAt = DateTime.UtcNow, Title = "t1", Message = "m", Type = "x" },
                new Notification { Id = 11, UserId = "u1", Role = null, IsRead = false, CreatedAt = DateTime.UtcNow, Title = "t2", Message = "m", Type = "x" },
                new Notification { Id = 12, UserId = null, Role = "SalesOrgAdmin", IsRead = true, CreatedAt = DateTime.UtcNow, ReadAt = DateTime.UtcNow, Title = "t3", Message = "m", Type = "x" }
            );
            await context.SaveChangesAsync();

            var updated = await svc.MarkUnreadBulkAsync(
                notificationIds: new[] { 10, 11, 12 },
                userId: "u1",
                role: "SalesOrgAdmin");

            Assert.Equal(2, updated);

            var unreadIds = await context.Notifications
                .Where(n => !n.IsRead)
                .Select(n => n.Id)
                .OrderBy(id => id)
                .ToListAsync();

            Assert.Equal(new[] { 10, 11, 12 }, unreadIds);
        }
    }
}

