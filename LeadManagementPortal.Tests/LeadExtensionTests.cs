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
    public class LeadExtensionTests
    {
        private ApplicationDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        private static Mock<ISettingsService> CreateSettingsMock(int extensionDays = 5)
        {
            var mockSettingsService = new Mock<ISettingsService>();
            mockSettingsService
                .Setup(s => s.GetAsync())
                .ReturnsAsync(new SystemSettings
                {
                    Id = 1,
                    CoolingPeriodDays = 15,
                    LeadInitialExpiryDays = 15,
                    LeadExtensionDays = extensionDays
                });
            return mockSettingsService;
        }

        [Fact]
        public async Task GrantExtensionAsync_FirstTime_SetsIsExtended_AndBlocksSecondTime()
        {
            using var context = GetInMemoryDbContext();

            var group = new SalesGroup { Id = "group-a", Name = "Group A" };
            var org = new SalesOrg { Id = 1, Name = "Org A", SalesGroupId = group.Id };
            var rep = new ApplicationUser { Id = "rep-1", UserName = "rep1", SalesGroupId = group.Id, SalesOrgId = org.Id };

            context.SalesGroups.Add(group);
            context.SalesOrgs.Add(org);
            context.Users.Add(rep);

            var lead = new Lead
            {
                Id = "lead-1",
                FirstName = "A",
                LastName = "B",
                Email = "a@example.com",
                Phone = "1111111111",
                Company = "Co",
                AssignedToId = rep.Id,
                CreatedById = rep.Id,
                SalesGroupId = group.Id,
                SalesOrgId = org.Id,
                Status = LeadStatus.New,
                CreatedDate = DateTime.UtcNow.AddDays(-1),
                ExpiryDate = DateTime.UtcNow.AddDays(1),
                IsExpired = false
            };

            context.Leads.Add(lead);
            await context.SaveChangesAsync();

            var mockCustomerService = new Mock<ICustomerService>();
            var mockSettingsService = CreateSettingsMock(extensionDays: 5);
            var leadService = new LeadService(context, mockCustomerService.Object, mockSettingsService.Object);

            var ok1 = await leadService.GrantExtensionAsync(lead.Id, "admin");
            Assert.True(ok1);

            var updated = await context.Leads.FirstAsync(l => l.Id == lead.Id);
            Assert.True(updated.IsExtended);
            Assert.NotNull(updated.ExtensionGrantedDate);
            Assert.Equal("admin", updated.ExtensionGrantedBy);

            var ok2 = await leadService.GrantExtensionAsync(lead.Id, "admin");
            Assert.False(ok2);
        }

        [Fact]
        public async Task GrantExtensionAsync_LegacyExtensionGrantedDate_BlocksExtensionEvenIfFlagFalse()
        {
            using var context = GetInMemoryDbContext();

            var rep = new ApplicationUser { Id = "rep-1", UserName = "rep1" };
            context.Users.Add(rep);

            var lead = new Lead
            {
                Id = "lead-legacy",
                FirstName = "A",
                LastName = "B",
                Email = "a@example.com",
                Phone = "1111111111",
                Company = "Co",
                AssignedToId = rep.Id,
                CreatedById = rep.Id,
                Status = LeadStatus.New,
                CreatedDate = DateTime.UtcNow.AddDays(-1),
                ExpiryDate = DateTime.UtcNow.AddDays(1),
                IsExpired = false,
                IsExtended = false,
                ExtensionGrantedDate = DateTime.UtcNow.AddDays(-2),
                ExtensionGrantedBy = "someone"
            };

            context.Leads.Add(lead);
            await context.SaveChangesAsync();

            var mockCustomerService = new Mock<ICustomerService>();
            var mockSettingsService = CreateSettingsMock(extensionDays: 5);
            var leadService = new LeadService(context, mockCustomerService.Object, mockSettingsService.Object);

            var ok = await leadService.GrantExtensionAsync(lead.Id, "admin");
            Assert.False(ok);
        }
    }
}

