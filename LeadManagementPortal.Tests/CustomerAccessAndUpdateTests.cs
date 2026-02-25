using System;
using System.Threading.Tasks;
using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using LeadManagementPortal.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LeadManagementPortal.Tests
{
    public class CustomerAccessAndUpdateTests
    {
        private ApplicationDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task GetAccessibleByIdAsync_SalesOrgAdmin_OnlySeesCustomersInTheirOrg()
        {
            using var context = GetInMemoryDbContext();

            var groupA = new SalesGroup { Id = "group-a", Name = "Group A" };
            var groupB = new SalesGroup { Id = "group-b", Name = "Group B" };
            var orgA = new SalesOrg { Id = 1, Name = "Org A", SalesGroupId = groupA.Id };
            var orgB = new SalesOrg { Id = 2, Name = "Org B", SalesGroupId = groupB.Id };
            context.SalesGroups.AddRange(groupA, groupB);
            context.SalesOrgs.AddRange(orgA, orgB);

            var repA = new ApplicationUser { Id = "rep-a", UserName = "repa", SalesGroupId = groupA.Id, SalesOrgId = orgA.Id };
            var repB = new ApplicationUser { Id = "rep-b", UserName = "repb", SalesGroupId = groupB.Id, SalesOrgId = orgB.Id };
            var orgAdminA = new ApplicationUser { Id = "admin-a", UserName = "admina", SalesGroupId = groupA.Id, SalesOrgId = orgA.Id };
            context.Users.AddRange(repA, repB, orgAdminA);

            var c1 = new Customer
            {
                Id = "cust-1",
                FirstName = "F",
                LastName = "L",
                Email = "c1@example.com",
                Phone = "1111111111",
                ConvertedById = repA.Id,
                SalesRepId = repA.Id,
                SalesGroupId = groupA.Id,
                OriginalLeadId = "lead-1",
                LeadCreatedDate = DateTime.UtcNow.AddDays(-10),
                DaysToConvert = 10
            };
            var c2 = new Customer
            {
                Id = "cust-2",
                FirstName = "F",
                LastName = "L",
                Email = "c2@example.com",
                Phone = "2222222222",
                ConvertedById = repB.Id,
                SalesRepId = repB.Id,
                SalesGroupId = groupB.Id,
                OriginalLeadId = "lead-2",
                LeadCreatedDate = DateTime.UtcNow.AddDays(-5),
                DaysToConvert = 5
            };
            context.Customers.AddRange(c1, c2);
            await context.SaveChangesAsync();

            var svc = new CustomerService(context);

            var visible1 = await svc.GetAccessibleByIdAsync("cust-1", "admin-a", UserRoles.SalesOrgAdmin);
            Assert.NotNull(visible1);

            var visible2 = await svc.GetAccessibleByIdAsync("cust-2", "admin-a", UserRoles.SalesOrgAdmin);
            Assert.Null(visible2);
        }

        [Fact]
        public async Task GetAccessibleByIdAsync_SalesRep_SeesOwnedOrConvertedCustomers()
        {
            using var context = GetInMemoryDbContext();

            var rep = new ApplicationUser { Id = "rep-1", UserName = "rep1" };
            var other = new ApplicationUser { Id = "rep-2", UserName = "rep2" };
            context.Users.AddRange(rep, other);

            var cOwned = new Customer
            {
                Id = "cust-owned",
                FirstName = "F",
                LastName = "L",
                Email = "owned@example.com",
                Phone = "1111111111",
                ConvertedById = other.Id,
                SalesRepId = rep.Id,
                OriginalLeadId = "lead-1",
                LeadCreatedDate = DateTime.UtcNow.AddDays(-3),
                DaysToConvert = 3
            };
            var cConverted = new Customer
            {
                Id = "cust-converted",
                FirstName = "F",
                LastName = "L",
                Email = "converted@example.com",
                Phone = "2222222222",
                ConvertedById = rep.Id,
                SalesRepId = other.Id,
                OriginalLeadId = "lead-2",
                LeadCreatedDate = DateTime.UtcNow.AddDays(-2),
                DaysToConvert = 2
            };
            var cOther = new Customer
            {
                Id = "cust-other",
                FirstName = "F",
                LastName = "L",
                Email = "other@example.com",
                Phone = "3333333333",
                ConvertedById = other.Id,
                SalesRepId = other.Id,
                OriginalLeadId = "lead-3",
                LeadCreatedDate = DateTime.UtcNow.AddDays(-1),
                DaysToConvert = 1
            };

            context.Customers.AddRange(cOwned, cConverted, cOther);
            await context.SaveChangesAsync();

            var svc = new CustomerService(context);

            Assert.NotNull(await svc.GetAccessibleByIdAsync("cust-owned", rep.Id, UserRoles.SalesRep));
            Assert.NotNull(await svc.GetAccessibleByIdAsync("cust-converted", rep.Id, UserRoles.SalesRep));
            Assert.Null(await svc.GetAccessibleByIdAsync("cust-other", rep.Id, UserRoles.SalesRep));
        }

        [Fact]
        public async Task UpdateAsync_UpdatesEditableFieldsOnly()
        {
            using var context = GetInMemoryDbContext();

            var rep = new ApplicationUser { Id = "rep-1", UserName = "rep1", SalesGroupId = "group-a", SalesOrgId = 1 };
            context.Users.Add(rep);

            var customer = new Customer
            {
                Id = "cust-1",
                FirstName = "Old",
                LastName = "Name",
                Email = "old@example.com",
                Phone = "1111111111",
                Company = "OldCo",
                Address = "OldAddr",
                City = "OldCity",
                State = "OS",
                ZipCode = "00000",
                Notes = "OldNotes",
                ConvertedById = rep.Id,
                SalesRepId = rep.Id,
                SalesGroupId = rep.SalesGroupId,
                ConversionDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                OriginalLeadId = "lead-1",
                LeadCreatedDate = new DateTime(2024, 12, 1, 0, 0, 0, DateTimeKind.Utc),
                DaysToConvert = 31
            };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var svc = new CustomerService(context);

            var ok = await svc.UpdateAsync(new Customer
            {
                Id = customer.Id,
                FirstName = "New",
                LastName = "Name2",
                Email = "new@example.com",
                Phone = "2222222222",
                Company = "NewCo",
                Address = "NewAddr",
                City = "NewCity",
                State = "NS",
                ZipCode = "99999",
                Notes = "NewNotes",
                SalesRepId = rep.Id,
                SalesGroupId = rep.SalesGroupId
            });
            Assert.True(ok);

            var updated = await context.Customers.FirstAsync(c => c.Id == customer.Id);
            Assert.Equal("New", updated.FirstName);
            Assert.Equal("Name2", updated.LastName);
            Assert.Equal("new@example.com", updated.Email);
            Assert.Equal("2222222222", updated.Phone);
            Assert.Equal("NewCo", updated.Company);
            Assert.Equal("NewAddr", updated.Address);
            Assert.Equal("NewCity", updated.City);
            Assert.Equal("NS", updated.State);
            Assert.Equal("99999", updated.ZipCode);
            Assert.Equal("NewNotes", updated.Notes);

            // System fields should remain unchanged
            Assert.Equal(rep.Id, updated.ConvertedById);
            Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), updated.ConversionDate);
            Assert.Equal("lead-1", updated.OriginalLeadId);
            Assert.Equal(new DateTime(2024, 12, 1, 0, 0, 0, DateTimeKind.Utc), updated.LeadCreatedDate);
            Assert.Equal(31, updated.DaysToConvert);
        }
    }
}

