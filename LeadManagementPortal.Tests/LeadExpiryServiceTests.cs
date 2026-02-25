using System;
using System.Linq;
using System.Threading.Tasks;
using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using LeadManagementPortal.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace LeadManagementPortal.Tests
{
    public class LeadExpiryServiceTests
    {
        private static ApplicationDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        private static LeadService CreateLeadService(ApplicationDbContext context)
        {
            var mockCustomerService = new Mock<ICustomerService>();
            var mockSettingsService = new Mock<ISettingsService>();
            mockSettingsService.Setup(s => s.GetAsync()).ReturnsAsync(new SystemSettings
            {
                Id = 1,
                CoolingPeriodDays = 15,
                LeadInitialExpiryDays = 15,
                LeadExtensionDays = 5
            });
            var mockLeadDocs = new Mock<ILeadDocumentService>();
            return new LeadService(context, mockCustomerService.Object, mockSettingsService.Object, mockLeadDocs.Object);
        }

        [Fact]
        public async Task GetLeadsExpiringSoonAsync_ReturnsOnlyLeadsExpiringInOneToThreeDays()
        {
            using var context = GetInMemoryDbContext();
            var svc = CreateLeadService(context);
            var now = new DateTime(2030, 1, 10, 12, 0, 0, DateTimeKind.Utc);

            context.Leads.AddRange(
                new Lead
                {
                    Id = "lead-1",
                    Company = "In 1d",
                    AssignedToId = "rep-1",
                    CreatedById = "rep-1",
                    ExpiryDate = now.AddDays(1)
                },
                new Lead
                {
                    Id = "lead-2",
                    Company = "In 3d",
                    AssignedToId = "rep-1",
                    CreatedById = "rep-1",
                    ExpiryDate = now.AddDays(3).AddHours(1)
                },
                new Lead
                {
                    Id = "lead-3",
                    Company = "In <1d (excluded)",
                    AssignedToId = "rep-1",
                    CreatedById = "rep-1",
                    ExpiryDate = now.AddHours(23)
                },
                new Lead
                {
                    Id = "lead-4",
                    Company = "In 4d (excluded)",
                    AssignedToId = "rep-1",
                    CreatedById = "rep-1",
                    ExpiryDate = now.AddDays(4)
                },
                new Lead
                {
                    Id = "lead-5",
                    Company = "Lost (excluded)",
                    AssignedToId = "rep-1",
                    CreatedById = "rep-1",
                    Status = LeadStatus.Lost,
                    ExpiryDate = now.AddDays(2)
                });

            await context.SaveChangesAsync();

            var results = await svc.GetLeadsExpiringSoonAsync(now, daysThreshold: 3);
            var ids = results.Select(r => r.LeadId).OrderBy(x => x).ToList();

            Assert.Equal(new[] { "lead-1", "lead-2" }, ids);
        }

        [Fact]
        public async Task ExpireOldLeadsAsync_ReturnsAndMarksOnlyActuallyExpiredLeads()
        {
            using var context = GetInMemoryDbContext();
            var svc = CreateLeadService(context);
            var now = new DateTime(2030, 1, 10, 12, 0, 0, DateTimeKind.Utc);

            context.Leads.AddRange(
                new Lead
                {
                    Id = "lead-expire",
                    Company = "Should Expire",
                    AssignedToId = "rep-1",
                    CreatedById = "rep-1",
                    ExpiryDate = now.AddMinutes(-1),
                    IsExpired = false,
                    Status = LeadStatus.New
                },
                new Lead
                {
                    Id = "lead-converted",
                    Company = "Converted Should Not Expire",
                    AssignedToId = "rep-1",
                    CreatedById = "rep-1",
                    ExpiryDate = now.AddDays(-10),
                    IsExpired = false,
                    Status = LeadStatus.Converted
                });

            await context.SaveChangesAsync();

            var expired = await svc.ExpireOldLeadsAsync(now);
            Assert.Single(expired);
            Assert.Equal("lead-expire", expired[0].LeadId);

            var lead1 = await context.Leads.SingleAsync(l => l.Id == "lead-expire");
            Assert.True(lead1.IsExpired);
            Assert.Equal(LeadStatus.Expired, lead1.Status);

            var lead2 = await context.Leads.SingleAsync(l => l.Id == "lead-converted");
            Assert.False(lead2.IsExpired);
            Assert.Equal(LeadStatus.Converted, lead2.Status);
        }
    }
}

