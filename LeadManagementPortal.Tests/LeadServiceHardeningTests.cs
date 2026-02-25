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
    public class LeadServiceHardeningTests
    {
        private static ApplicationDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        private static Mock<ISettingsService> CreateSettingsMock(int coolingDays = 15)
        {
            var mock = new Mock<ISettingsService>();
            mock.Setup(s => s.GetAsync()).ReturnsAsync(new SystemSettings
            {
                Id = 1,
                CoolingPeriodDays = coolingDays,
                LeadInitialExpiryDays = 15,
                LeadExtensionDays = 5
            });
            return mock;
        }

        [Fact]
        public async Task CanRegisterLeadForGroupAsync_TruncatesZip_ToMatchGlobalRules()
        {
            using var context = GetInMemoryDbContext();

            context.Leads.Add(new Lead
            {
                Id = "lost-1",
                Company = "Acme",
                Address = "123 Main",
                City = "Austin",
                State = "TX",
                ZipCode = "12345",
                Status = LeadStatus.Lost,
                IsExpired = false,
                SalesGroupId = "group-a",
                AssignedToId = "rep-1",
                CreatedById = "rep-1",
                CreatedDate = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var mockCustomerService = new Mock<ICustomerService>();
            var mockSettingsService = CreateSettingsMock();
            var mockLeadDocs = new Mock<ILeadDocumentService>();

            var svc = new LeadService(context, mockCustomerService.Object, mockSettingsService.Object, mockLeadDocs.Object);

            // Zip includes 9-digit variant; should still conflict with the existing 5-digit lead.
            var allowed = await svc.CanRegisterLeadForGroupAsync(
                company: "Acme",
                salesGroupId: "group-a",
                address: "123 Main",
                city: "Austin",
                state: "TX",
                zip: "12345-6789");

            Assert.False(allowed);
        }

        [Fact]
        public async Task DeleteAsync_InvokesLeadDocumentCleanup_BeforeDeletingLead()
        {
            using var context = GetInMemoryDbContext();

            context.Leads.Add(new Lead
            {
                Id = "lead-1",
                FirstName = "A",
                LastName = "B",
                Email = "a@example.com",
                Phone = "1111111111",
                Company = "Acme",
                AssignedToId = "rep-1",
                CreatedById = "rep-1",
                CreatedDate = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var mockCustomerService = new Mock<ICustomerService>();
            var mockSettingsService = CreateSettingsMock();
            var mockLeadDocs = new Mock<ILeadDocumentService>();
            mockLeadDocs.Setup(s => s.DeleteForLeadAsync("lead-1", default)).ReturnsAsync(0);

            var svc = new LeadService(context, mockCustomerService.Object, mockSettingsService.Object, mockLeadDocs.Object);

            var ok = await svc.DeleteAsync("lead-1");

            Assert.True(ok);
            mockLeadDocs.Verify(s => s.DeleteForLeadAsync("lead-1", default), Times.Once);
            Assert.Null(await context.Leads.FirstOrDefaultAsync(l => l.Id == "lead-1"));
        }
    }
}

