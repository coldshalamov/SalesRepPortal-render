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
    public class CustomerVisibilityHardeningTests
    {
        private static ApplicationDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task GetByUserAsync_GroupAdmin_WithNoGroup_ShouldSeeNoCustomers()
        {
            using var context = GetInMemoryDbContext();

            var groupAdmin = new ApplicationUser { Id = "ga", UserName = "ga", SalesGroupId = null };
            var rep = new ApplicationUser { Id = "rep", UserName = "rep", SalesGroupId = "group-1" };
            context.Users.AddRange(groupAdmin, rep);

            context.Customers.Add(new Customer
            {
                Id = "c1",
                FirstName = "A",
                LastName = "One",
                Email = "a1@example.com",
                Phone = "1111111111",
                Company = "Acme",
                ConvertedById = "rep",
                SalesRepId = "rep",
                SalesGroupId = "group-1"
            });

            await context.SaveChangesAsync();

            var service = new CustomerService(context);
            var result = await service.GetByUserAsync("ga", UserRoles.GroupAdmin);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetByUserAsync_SalesOrgAdmin_WithNoOrg_ShouldSeeNoCustomers()
        {
            using var context = GetInMemoryDbContext();

            var orgAdmin = new ApplicationUser { Id = "oa", UserName = "oa", SalesOrgId = null };
            var rep = new ApplicationUser { Id = "rep", UserName = "rep", SalesOrgId = 1, SalesGroupId = "group-1" };
            context.Users.AddRange(orgAdmin, rep);

            context.Customers.Add(new Customer
            {
                Id = "c1",
                FirstName = "A",
                LastName = "One",
                Email = "a1@example.com",
                Phone = "1111111111",
                Company = "Acme",
                ConvertedById = "rep",
                SalesRepId = "rep",
                SalesGroupId = "group-1"
            });

            await context.SaveChangesAsync();

            var service = new CustomerService(context);
            var result = await service.GetByUserAsync("oa", UserRoles.SalesOrgAdmin);

            Assert.Empty(result);
        }

        [Fact]
        public async Task SearchTopAsync_UnknownRole_ShouldReturnEmpty()
        {
            using var context = GetInMemoryDbContext();

            var user = new ApplicationUser { Id = "u1", UserName = "u1", SalesGroupId = "group-1", SalesOrgId = 1 };
            context.Users.Add(user);

            context.Customers.Add(new Customer
            {
                Id = "c1",
                FirstName = "John",
                LastName = "Doe",
                Email = "john@example.com",
                Phone = "1111111111",
                Company = "Acme",
                ConvertedById = "u1",
                SalesRepId = "u1",
                SalesGroupId = "group-1"
            });

            await context.SaveChangesAsync();

            var service = new CustomerService(context);
            var result = await service.SearchTopAsync("John", "u1", "UnknownRole", 10);

            Assert.Empty(result);
        }

        [Fact]
        public async Task SearchTopAsync_SalesRep_ShouldOnlyReturnAccessibleCustomers()
        {
            using var context = GetInMemoryDbContext();

            var repA = new ApplicationUser { Id = "rep-a", UserName = "rep-a" };
            var repB = new ApplicationUser { Id = "rep-b", UserName = "rep-b" };
            context.Users.AddRange(repA, repB);

            context.Customers.AddRange(
                new Customer
                {
                    Id = "c-a",
                    FirstName = "John",
                    LastName = "Doe",
                    Email = "john.a@example.com",
                    Phone = "1111111111",
                    Company = "Acme",
                    ConvertedById = "rep-a",
                    SalesRepId = "rep-a"
                },
                new Customer
                {
                    Id = "c-b",
                    FirstName = "John",
                    LastName = "Doe",
                    Email = "john.b@example.com",
                    Phone = "2222222222",
                    Company = "Beta",
                    ConvertedById = "rep-b",
                    SalesRepId = "rep-b"
                }
            );

            await context.SaveChangesAsync();

            var service = new CustomerService(context);
            var result = (await service.SearchTopAsync("John", "rep-a", UserRoles.SalesRep, 10)).ToList();

            Assert.Single(result);
            Assert.Equal("c-a", result[0].Id);
        }
    }
}

