using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace LeadManagementPortal.Tests
{
    public class SeedDataRoleTests
    {
        private static Mock<RoleManager<ApplicationRole>> CreateRoleManagerMock(List<string> createdRoleNames)
        {
            var store = new Mock<IRoleStore<ApplicationRole>>();
            var roleValidators = new IRoleValidator<ApplicationRole>[] { };
            var normalizer = new UpperInvariantLookupNormalizer();
            var errorDescriber = new IdentityErrorDescriber();
            var logger = new Mock<ILogger<RoleManager<ApplicationRole>>>().Object;

            var mock = new Mock<RoleManager<ApplicationRole>>(
                store.Object,
                roleValidators,
                normalizer,
                errorDescriber,
                logger);

            mock.Setup(m => m.RoleExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            mock.Setup(m => m.CreateAsync(It.IsAny<ApplicationRole>()))
                .Callback<ApplicationRole>(role => createdRoleNames.Add(role.Name!))
                .ReturnsAsync(IdentityResult.Success);

            return mock;
        }

        private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            var options = Options.Create(new IdentityOptions());
            var passwordHasher = new PasswordHasher<ApplicationUser>();
            var userValidators = new IUserValidator<ApplicationUser>[] { };
            var passwordValidators = new IPasswordValidator<ApplicationUser>[] { };
            var normalizer = new UpperInvariantLookupNormalizer();
            var errorDescriber = new IdentityErrorDescriber();
            var services = new Mock<IServiceProvider>().Object;
            var logger = new Mock<ILogger<UserManager<ApplicationUser>>>().Object;

            var mock = new Mock<UserManager<ApplicationUser>>(
                store.Object,
                options,
                passwordHasher,
                userValidators,
                passwordValidators,
                normalizer,
                errorDescriber,
                services,
                logger);

            mock.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
            mock.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
            mock.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);

            return mock;
        }

        private sealed class TestHostEnvironment : IHostEnvironment
        {
            public string EnvironmentName { get; set; } = "Development";
            public string ApplicationName { get; set; } = "LeadManagementPortal.Tests";
            public string ContentRootPath { get; set; } = System.IO.Directory.GetCurrentDirectory();
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }

        [Fact]
        public async Task Initialize_SeedsAffiliateRoleAlongsideExistingRoles()
        {
            var createdRoleNames = new List<string>();
            var roleManager = CreateRoleManagerMock(createdRoleNames);
            var userManager = CreateUserManagerMock();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
            services.AddLogging();
            services.AddSingleton(roleManager.Object);
            services.AddSingleton(userManager.Object);
            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(System.Guid.NewGuid().ToString()));

            var serviceProvider = services.BuildServiceProvider();

            await SeedData.Initialize(serviceProvider);

            Assert.Contains(UserRoles.OrganizationAdmin, createdRoleNames);
            Assert.Contains(UserRoles.GroupAdmin, createdRoleNames);
            Assert.Contains(UserRoles.SalesRep, createdRoleNames);
            Assert.Contains(UserRoles.SalesOrgAdmin, createdRoleNames);
            Assert.Contains(UserRoles.Affiliate, createdRoleNames);
        }
    }
}
