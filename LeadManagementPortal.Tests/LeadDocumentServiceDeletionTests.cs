using System;
using System.Threading.Tasks;
using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using LeadManagementPortal.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace LeadManagementPortal.Tests
{
    public class LeadDocumentServiceDeletionTests
    {
        private static ApplicationDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task DeleteForLeadAsync_DeletesStorageKeys_AndRemovesRows()
        {
            using var context = GetInMemoryDbContext();

            context.LeadDocuments.AddRange(
                new LeadDocument { LeadId = "lead-1", FileName = "a.txt", StorageKey = "leads/lead-1/a.txt", UploadedAtUtc = DateTime.UtcNow },
                new LeadDocument { LeadId = "lead-1", FileName = "b.txt", StorageKey = "leads/lead-1/b.txt", UploadedAtUtc = DateTime.UtcNow }
            );
            await context.SaveChangesAsync();

            var storage = new Mock<IFileStorageService>();
            storage.Setup(s => s.DeleteAsync(It.IsAny<string>(), default)).ReturnsAsync(true);

            var service = new LeadDocumentService(context, storage.Object);
            var deleted = await service.DeleteForLeadAsync("lead-1");

            Assert.Equal(2, deleted);
            storage.Verify(s => s.DeleteAsync("leads/lead-1/a.txt", default), Times.Once);
            storage.Verify(s => s.DeleteAsync("leads/lead-1/b.txt", default), Times.Once);
            Assert.Empty(await context.LeadDocuments.ToListAsync());
        }
    }
}

