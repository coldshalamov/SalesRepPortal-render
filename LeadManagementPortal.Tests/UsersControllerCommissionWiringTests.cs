using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using LeadManagementPortal.Controllers;
using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace LeadManagementPortal.Tests
{
    public class UsersControllerCommissionWiringTests
    {
        private static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(System.Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock(IQueryable<ApplicationUser> users)
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

            mock.Setup(m => m.Users).Returns(users);
            mock.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);
            mock.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(new List<string> { UserRoles.SalesRep });
            mock.Setup(m => m.RemoveFromRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>())).ReturnsAsync(IdentityResult.Success);
            mock.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);

            return mock;
        }

        private static Mock<RoleManager<ApplicationRole>> CreateRoleManagerMock()
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

            mock.Setup(m => m.Roles).Returns(new List<ApplicationRole>
            {
                new() { Name = UserRoles.OrganizationAdmin },
                new() { Name = UserRoles.SalesRep },
                new() { Name = UserRoles.Affiliate }
            }.AsQueryable());

            return mock;
        }

        private static ClaimsPrincipal BuildUser(string userId, string role)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role)
            }, "TestAuth");

            return new ClaimsPrincipal(identity);
        }

        private static void AttachTempData(Controller controller)
        {
            controller.TempData = new TempDataDictionary(
                controller.ControllerContext.HttpContext!,
                Mock.Of<ITempDataProvider>());
        }

        [Fact]
        public async Task Edit_WhenCommissionConfigured_UpsertsDealAndSponsorLink()
        {
            await using var context = CreateContext();

            var admin = new ApplicationUser { Id = "admin-1", UserName = "admin@example.com", Email = "admin@example.com" };
            var editedUser = new ApplicationUser { Id = "user-1", UserName = "user@example.com", Email = "user@example.com", FirstName = "User", LastName = "One" };
            var sponsor = new ApplicationUser { Id = "sponsor-1", UserName = "sponsor@example.com", Email = "sponsor@example.com", FirstName = "Sponsor", LastName = "One" };

            context.Users.AddRange(admin, editedUser, sponsor);
            await context.SaveChangesAsync();

            var userManager = CreateUserManagerMock(context.Users);
            userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(admin);

            var controller = new UsersController(userManager.Object, CreateRoleManagerMock().Object, context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = BuildUser(admin.Id, UserRoles.OrganizationAdmin)
                    }
                }
            };
            AttachTempData(controller);

            var result = await controller.Edit(new EditUserViewModel
            {
                Id = editedUser.Id,
                FirstName = "User",
                LastName = "One",
                Email = "user@example.com",
                Role = UserRoles.Affiliate,
                IsActive = true,
                SponsorId = sponsor.Id,
                CommissionDealType = CommissionDealType.GrossPercent,
                CommissionRate = 12.5m,
                CommissionBaseCost = 25m,
                CommissionCalculationBasis = CommissionCalculationBasis.DownlineGross
            });

            Assert.IsType<RedirectToActionResult>(result);

            var deal = await context.CommissionDeals.SingleAsync(d => d.ApplicationUserId == editedUser.Id);
            Assert.Equal(CommissionDealType.GrossPercent, deal.DealType);
            Assert.Equal(12.5m, deal.Rate);
            Assert.Equal(25m, deal.BaseCost);
            Assert.Equal(CommissionCalculationBasis.DownlineGross, deal.CalculationBasis);

            var link = await context.CommissionLinks.SingleAsync(l => l.DownlineId == editedUser.Id);
            Assert.Equal(sponsor.Id, link.SponsorId);
        }

        [Fact]
        public async Task Edit_Get_SponsorPickerExcludesEditedUser()
        {
            await using var context = CreateContext();

            var admin = new ApplicationUser { Id = "admin-1", UserName = "admin@example.com", Email = "admin@example.com" };
            var editedUser = new ApplicationUser { Id = "user-1", UserName = "user@example.com", Email = "user@example.com", FirstName = "User", LastName = "One" };
            var sponsor = new ApplicationUser { Id = "sponsor-1", UserName = "sponsor@example.com", Email = "sponsor@example.com", FirstName = "Sponsor", LastName = "One" };

            context.Users.AddRange(admin, editedUser, sponsor);
            await context.SaveChangesAsync();

            var userManager = CreateUserManagerMock(context.Users);
            userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(admin);
            userManager.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(new List<string> { UserRoles.Affiliate });

            var controller = new UsersController(userManager.Object, CreateRoleManagerMock().Object, context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = BuildUser(admin.Id, UserRoles.OrganizationAdmin)
                    }
                }
            };
            AttachTempData(controller);

            var result = await controller.Edit(editedUser.Id);

            Assert.IsType<ViewResult>(result);
            object sponsorsObject = controller.ViewBag.Sponsors;
            var sponsors = Assert.IsType<SelectList>(sponsorsObject);
            List<SelectListItem> items = sponsors.Cast<SelectListItem>().ToList();
            Assert.DoesNotContain(items, item => item.Value == editedUser.Id);
            Assert.Contains(items, item => item.Value == sponsor.Id);
        }

        [Fact]
        public async Task Edit_WhenSponsorCleared_RemovesExistingLinkAndUpdatesDeal()
        {
            await using var context = CreateContext();

            var admin = new ApplicationUser { Id = "admin-1", UserName = "admin@example.com", Email = "admin@example.com" };
            var editedUser = new ApplicationUser { Id = "user-1", UserName = "user@example.com", Email = "user@example.com", FirstName = "User", LastName = "One" };
            var sponsor = new ApplicationUser { Id = "sponsor-1", UserName = "sponsor@example.com", Email = "sponsor@example.com", FirstName = "Sponsor", LastName = "One" };

            context.Users.AddRange(admin, editedUser, sponsor);
            context.CommissionDeals.Add(new CommissionDeal
            {
                ApplicationUserId = editedUser.Id,
                DealType = CommissionDealType.GrossPercent,
                Rate = 10m,
                CalculationBasis = CommissionCalculationBasis.DownlineGross
            });
            context.CommissionLinks.Add(new CommissionLink
            {
                DownlineId = editedUser.Id,
                SponsorId = sponsor.Id
            });
            await context.SaveChangesAsync();

            var userManager = CreateUserManagerMock(context.Users.Include(u => u.CommissionDeal).Include(u => u.SponsorLink));
            userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(admin);

            var controller = new UsersController(userManager.Object, CreateRoleManagerMock().Object, context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = BuildUser(admin.Id, UserRoles.OrganizationAdmin)
                    }
                }
            };
            AttachTempData(controller);

            var result = await controller.Edit(new EditUserViewModel
            {
                Id = editedUser.Id,
                FirstName = "User",
                LastName = "One",
                Email = "user@example.com",
                Role = UserRoles.Affiliate,
                IsActive = true,
                SponsorId = null,
                CommissionDealType = CommissionDealType.NetPercent,
                CommissionRate = 8m,
                CommissionBaseCost = null,
                CommissionCalculationBasis = CommissionCalculationBasis.DownlineNet
            });

            Assert.IsType<RedirectToActionResult>(result);

            var deal = await context.CommissionDeals.SingleAsync(d => d.ApplicationUserId == editedUser.Id);
            Assert.Equal(CommissionDealType.NetPercent, deal.DealType);
            Assert.Equal(8m, deal.Rate);
            Assert.Equal(CommissionCalculationBasis.DownlineNet, deal.CalculationBasis);
            Assert.Empty(await context.CommissionLinks.Where(l => l.DownlineId == editedUser.Id).ToListAsync());
        }

        [Fact]
        public async Task Edit_AsGroupAdmin_PreservesAffiliateRoleWhileSavingCommissionChanges()
        {
            await using var context = CreateContext();

            var admin = new ApplicationUser
            {
                Id = "admin-1",
                UserName = "admin@example.com",
                Email = "admin@example.com",
                SalesGroupId = "group-1"
            };
            var editedUser = new ApplicationUser
            {
                Id = "user-1",
                UserName = "user@example.com",
                Email = "user@example.com",
                FirstName = "User",
                LastName = "One",
                SalesGroupId = "group-1",
                SalesOrgId = 3
            };
            var sponsor = new ApplicationUser
            {
                Id = "sponsor-1",
                UserName = "sponsor@example.com",
                Email = "sponsor@example.com",
                FirstName = "Sponsor",
                LastName = "One",
                SalesGroupId = "group-1",
                SalesOrgId = 3
            };

            context.SalesGroups.Add(new SalesGroup
            {
                Id = "group-1",
                Name = "Group One",
                IsActive = true
            });
            context.SalesOrgs.Add(new SalesOrg
            {
                Id = 3,
                Name = "Org Three",
                SalesGroupId = "group-1"
            });
            context.Users.AddRange(admin, editedUser, sponsor);
            await context.SaveChangesAsync();

            var userManager = CreateUserManagerMock(context.Users);
            userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(admin);
            userManager.Setup(m => m.GetRolesAsync(It.Is<ApplicationUser>(u => u.Id == editedUser.Id)))
                .ReturnsAsync(new List<string> { UserRoles.Affiliate });

            var controller = new UsersController(userManager.Object, CreateRoleManagerMock().Object, context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = BuildUser(admin.Id, UserRoles.GroupAdmin)
                    }
                }
            };
            AttachTempData(controller);

            var result = await controller.Edit(new EditUserViewModel
            {
                Id = editedUser.Id,
                FirstName = "User",
                LastName = "One",
                Email = "user@example.com",
                Role = UserRoles.SalesRep,
                SalesGroupId = "other-group",
                SalesOrgId = 3,
                IsActive = true,
                SponsorId = sponsor.Id,
                CommissionDealType = CommissionDealType.GrossPercent,
                CommissionRate = 12m,
                CommissionCalculationBasis = CommissionCalculationBasis.DownlineGross
            });

            Assert.IsType<RedirectToActionResult>(result);
            userManager.Verify(m => m.AddToRoleAsync(
                It.Is<ApplicationUser>(u => u.Id == editedUser.Id),
                UserRoles.Affiliate), Times.Once);
        }
    }
}
