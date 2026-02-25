using System;
using System.Collections.Generic;
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
    public class SalesOrgAdminVisibilityTests
    {
        private ApplicationDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task GetByUserAsync_SalesOrgAdmin_ShouldOnlySeeLeadsInTheirOrg()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            
            // Create Sales Groups
            var groupA = new SalesGroup { Id = "group-a", Name = "Group A" };
            var groupB = new SalesGroup { Id = "group-b", Name = "Group B" };
            context.SalesGroups.AddRange(groupA, groupB);

            // Create Sales Orgs
            var orgA = new SalesOrg { Id = 1, Name = "Org A", SalesGroupId = "group-a" };
            var orgB = new SalesOrg { Id = 2, Name = "Org B", SalesGroupId = "group-b" };
            context.SalesOrgs.AddRange(orgA, orgB);

            // Create Users
            var repA = new ApplicationUser { Id = "rep-a", UserName = "repa", SalesOrgId = 1, SalesGroupId = "group-a" };
            var repB = new ApplicationUser { Id = "rep-b", UserName = "repb", SalesOrgId = 2, SalesGroupId = "group-b" };
            
            var orgAdminA = new ApplicationUser { Id = "admin-a", UserName = "admina", SalesOrgId = 1, SalesGroupId = "group-a" };
            
            context.Users.AddRange(repA, repB, orgAdminA);

            // Create Leads
            var leadA = new Lead 
            { 
                Id = "lead-a", 
                FirstName = "Lead", 
                LastName = "A", 
                AssignedToId = "rep-a", 
                SalesGroupId = "group-a",
                CreatedDate = DateTime.UtcNow 
            };
            
            var leadB = new Lead 
            { 
                Id = "lead-b", 
                FirstName = "Lead", 
                LastName = "B", 
                AssignedToId = "rep-b", 
                SalesGroupId = "group-b",
                CreatedDate = DateTime.UtcNow 
            };

            context.Leads.AddRange(leadA, leadB);
            await context.SaveChangesAsync();

            // Mock dependencies
            var mockCustomerService = new Mock<ICustomerService>();
            var mockSettingsService = new Mock<ISettingsService>();
            var mockLeadDocumentService = new Mock<ILeadDocumentService>();

            var leadService = new LeadService(context, mockCustomerService.Object, mockSettingsService.Object, mockLeadDocumentService.Object);

            // Act
            var result = await leadService.GetByUserAsync("admin-a", UserRoles.SalesOrgAdmin);

            // Assert
            Assert.Single(result);
            Assert.Equal("lead-a", result.First().Id);
        }

        [Fact]
        public async Task GetByUserAsync_SalesOrgAdmin_WithNoOrg_ShouldSeeNoLeads()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            
            // Create User with no Org
            var orgAdminNoOrg = new ApplicationUser { Id = "admin-no-org", UserName = "adminnoorg", SalesOrgId = null };
            context.Users.Add(orgAdminNoOrg);

            // Create some leads
            var lead = new Lead { Id = "lead-1", FirstName = "L", LastName = "1", AssignedToId = "some-rep" };
            context.Leads.Add(lead);
            await context.SaveChangesAsync();

            var mockCustomerService = new Mock<ICustomerService>();
            var mockSettingsService = new Mock<ISettingsService>();
            var mockLeadDocumentService = new Mock<ILeadDocumentService>();
            var leadService = new LeadService(context, mockCustomerService.Object, mockSettingsService.Object, mockLeadDocumentService.Object);

            // Act
            var result = await leadService.GetByUserAsync("admin-no-org", UserRoles.SalesOrgAdmin);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task SearchAsync_SalesOrgAdmin_ShouldOnlySearchLeadsInTheirOrg()
        {
             // Arrange
            using var context = GetInMemoryDbContext();
            
            // Create Sales Orgs
            var orgA = new SalesOrg { Id = 1, Name = "Org A", SalesGroupId = "group-a" };
            var orgB = new SalesOrg { Id = 2, Name = "Org B", SalesGroupId = "group-b" };
            context.SalesOrgs.AddRange(orgA, orgB);

            // Create Users
            var repA = new ApplicationUser { Id = "rep-a", UserName = "repa", SalesOrgId = 1 };
            var repB = new ApplicationUser { Id = "rep-b", UserName = "repb", SalesOrgId = 2 };
            var orgAdminA = new ApplicationUser { Id = "admin-a", UserName = "admina", SalesOrgId = 1 };
            
            context.Users.AddRange(repA, repB, orgAdminA);

            // Create Leads with same name to test search
            var leadA = new Lead 
            { 
                Id = "lead-a", 
                FirstName = "John", 
                LastName = "Doe", 
                AssignedToId = "rep-a", 
                SalesGroupId = "group-a"
            };
            
            var leadB = new Lead 
            { 
                Id = "lead-b", 
                FirstName = "John", 
                LastName = "Doe", 
                AssignedToId = "rep-b", 
                SalesGroupId = "group-b"
            };

            context.Leads.AddRange(leadA, leadB);
            await context.SaveChangesAsync();

            var mockCustomerService = new Mock<ICustomerService>();
            var mockSettingsService = new Mock<ISettingsService>();
            var mockLeadDocumentService = new Mock<ILeadDocumentService>();
            var leadService = new LeadService(context, mockCustomerService.Object, mockSettingsService.Object, mockLeadDocumentService.Object);

            // Act
            var result = await leadService.SearchAsync("John", "admin-a", UserRoles.SalesOrgAdmin);

            // Assert
            Assert.Single(result);
            Assert.Equal("lead-a", result.First().Id);
        }

        [Fact]
        public async Task GetDashboardStatsAsync_SalesOrgAdmin_ShouldOnlyCountLeadsInTheirOrg()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            
            // Create Sales Orgs
            var orgA = new SalesOrg { Id = 1, Name = "Org A", SalesGroupId = "group-a" };
            var orgB = new SalesOrg { Id = 2, Name = "Org B", SalesGroupId = "group-b" };
            context.SalesOrgs.AddRange(orgA, orgB);

            // Create Users
            var repA = new ApplicationUser { Id = "rep-a", UserName = "repa", SalesOrgId = 1 };
            var repB = new ApplicationUser { Id = "rep-b", UserName = "repb", SalesOrgId = 2 };
            var orgAdminA = new ApplicationUser { Id = "admin-a", UserName = "admina", SalesOrgId = 1 };
            
            context.Users.AddRange(repA, repB, orgAdminA);

            // Create Leads
            var leadA = new Lead 
            { 
                Id = "lead-a", 
                AssignedToId = "rep-a", 
                SalesGroupId = "group-a",
                Status = LeadStatus.New
            };
            
            var leadB = new Lead 
            { 
                Id = "lead-b", 
                AssignedToId = "rep-b", 
                SalesGroupId = "group-b",
                Status = LeadStatus.New
            };

            context.Leads.AddRange(leadA, leadB);
            await context.SaveChangesAsync();

            var dashboardService = new DashboardService(context);

            // Act
            var stats = await dashboardService.GetDashboardStatsAsync("admin-a", UserRoles.SalesOrgAdmin);

            // Assert
            Assert.Equal(1, stats.TotalLeads);
            Assert.Equal(1, stats.PendingLeads);
        }
    }
}
