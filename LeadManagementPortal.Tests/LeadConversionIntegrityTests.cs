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
    public class LeadConversionIntegrityTests
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
        public async Task ConvertToCustomerAsync_CreatesCustomerAndMarksLeadConverted_Atomically()
        {
            using var context = GetInMemoryDbContext();
            var svc = CreateLeadService(context);

            var now = DateTime.UtcNow;
            context.Leads.Add(new Lead
            {
                Id = "lead-1",
                FirstName = "A",
                LastName = "B",
                Email = "a@example.com",
                Phone = "5550101",
                Company = "Acme",
                AssignedToId = "rep-1",
                SalesGroupId = "group-1",
                CreatedById = "rep-1",
                CreatedDate = now.AddDays(-3),
                ExpiryDate = now.AddDays(10),
                Status = LeadStatus.New,
                IsExpired = false
            });
            await context.SaveChangesAsync();

            var ok = await svc.ConvertToCustomerAsync("lead-1", "rep-1");

            Assert.True(ok);
            var lead = await context.Leads.SingleAsync(l => l.Id == "lead-1");
            Assert.Equal(LeadStatus.Converted, lead.Status);
            Assert.NotNull(lead.ConvertedDate);

            var customer = await context.Customers.SingleAsync();
            Assert.Equal("lead-1", customer.OriginalLeadId);
            Assert.Equal("Acme", customer.Company);
            Assert.Equal("a@example.com", customer.Email);
        }

        [Fact]
        public async Task ConvertToCustomerAsync_ReturnsFalse_WhenCustomerAlreadyExistsForLead()
        {
            using var context = GetInMemoryDbContext();
            var svc = CreateLeadService(context);

            var now = DateTime.UtcNow;
            context.Leads.Add(new Lead
            {
                Id = "lead-1",
                FirstName = "A",
                LastName = "B",
                Email = "a@example.com",
                Phone = "5550101",
                Company = "Acme",
                AssignedToId = "rep-1",
                SalesGroupId = "group-1",
                CreatedById = "rep-1",
                CreatedDate = now.AddDays(-3),
                ExpiryDate = now.AddDays(10),
                Status = LeadStatus.New,
                IsExpired = false
            });

            context.Customers.Add(new Customer
            {
                Id = "cust-1",
                FirstName = "A",
                LastName = "B",
                Email = "a@example.com",
                Phone = "5550101",
                Company = "Acme",
                ConvertedById = "rep-1",
                SalesGroupId = "group-1",
                ConversionDate = now.AddDays(-1),
                OriginalLeadId = "lead-1",
                LeadCreatedDate = now.AddDays(-3),
                DaysToConvert = 2,
                IsDeleted = false
            });

            await context.SaveChangesAsync();

            var ok = await svc.ConvertToCustomerAsync("lead-1", "rep-1");
            Assert.False(ok);
            Assert.Equal(1, await context.Customers.CountAsync());
        }
    }
}

