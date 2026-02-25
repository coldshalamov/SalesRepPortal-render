using System;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using LeadManagementPortal.Controllers;
using LeadManagementPortal.Models;
using LeadManagementPortal.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LeadManagementPortal.Tests
{
    public class LeadDocumentsControllerTests
    {
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

            return new Mock<UserManager<ApplicationUser>>(
                store.Object,
                options,
                passwordHasher,
                userValidators,
                passwordValidators,
                normalizer,
                errorDescriber,
                services,
                logger);
        }

        private static ClaimsPrincipal BuildUser(string userId, string role, string? name = null)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role),
                new Claim(ClaimTypes.Name, name ?? "user@example.com")
            }, "TestAuth");

            return new ClaimsPrincipal(identity);
        }

        [Fact]
        public async Task Upload_UsesNameIdentifierClaim_ForUploadedByUserId()
        {
            var leadDocs = new Mock<ILeadDocumentService>();
            var leadService = new Mock<ILeadService>();
            var userManager = CreateUserManagerMock();

            leadService
                .Setup(s => s.GetByIdAsync("lead-1"))
                .ReturnsAsync(new Lead { Id = "lead-1", AssignedToId = "rep-1", SalesGroupId = "group-1" });

            string? capturedUploadedBy = null;
            leadDocs
                .Setup(s => s.AddAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<Stream>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()))
                .Callback((string _, string _, string _, long _, Stream _, string? uploadedBy, CancellationToken _) =>
                {
                    capturedUploadedBy = uploadedBy;
                })
                .ReturnsAsync(new LeadDocument { Id = 1, LeadId = "lead-1", StorageKey = "key", FileName = "file.txt" });

            var controller = new LeadDocumentsController(leadDocs.Object, leadService.Object, userManager.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = BuildUser("user-guid-123", UserRoles.OrganizationAdmin, name: "changed@email.local")
                    }
                }
            };

            var bytes = new byte[] { 1, 2, 3 };
            var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "file.txt")
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/plain"
            };

            var result = await controller.Upload("lead-1", file, CancellationToken.None);

            Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("user-guid-123", capturedUploadedBy);
        }

        [Fact]
        public async Task List_SalesOrgAdmin_AllowsAccess_ByOrgScope_NotGroupScope()
        {
            var leadDocs = new Mock<ILeadDocumentService>();
            var leadService = new Mock<ILeadService>();
            var userManager = CreateUserManagerMock();

            // Lead is in org 1 but group-b.
            leadService
                .Setup(s => s.GetByIdAsync("lead-1"))
                .ReturnsAsync(new Lead
                {
                    Id = "lead-1",
                    AssignedToId = "rep-1",
                    SalesGroupId = "group-b",
                    AssignedTo = new ApplicationUser { Id = "rep-1", SalesOrgId = 1, SalesGroupId = "group-b" }
                });

            // Sales org admin is in org 1 but group-a.
            userManager
                .Setup(m => m.FindByIdAsync("admin-1"))
                .ReturnsAsync(new ApplicationUser { Id = "admin-1", SalesOrgId = 1, SalesGroupId = "group-a" });

            leadDocs
                .Setup(s => s.ListAsync("lead-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { });

            var controller = new LeadDocumentsController(leadDocs.Object, leadService.Object, userManager.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = BuildUser("admin-1", UserRoles.SalesOrgAdmin)
                    }
                }
            };

            var result = await controller.List("lead-1", CancellationToken.None);

            Assert.IsType<PartialViewResult>(result);
        }
    }
}
