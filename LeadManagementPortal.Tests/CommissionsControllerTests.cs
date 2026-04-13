using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using LeadManagementPortal.Controllers;
using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using LeadManagementPortal.Models.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeadManagementPortal.Tests
{
    public class CommissionsControllerTests
    {
        private static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
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

        [Fact]
        public async Task Index_AsSalesRep_OnlyShowsBeneficiaryRowsForCurrentUser()
        {
            await using var context = CreateContext();

            context.Users.AddRange(
                new ApplicationUser { Id = "rep-1", UserName = "rep1@example.com", Email = "rep1@example.com" },
                new ApplicationUser { Id = "rep-2", UserName = "rep2@example.com", Email = "rep2@example.com" });
            context.SaleRecords.AddRange(
                new SaleRecord
                {
                    Id = 1,
                    AccountId = "rep-1",
                    ProductName = "Starter Pack",
                    Quantity = 1,
                    GrossAmount = 1000m,
                    CostAmount = 400m,
                    SaleDate = DateTime.UtcNow.AddDays(-1),
                    ImportBatchId = "batch-a",
                    ImportedAt = DateTime.UtcNow,
                    RawPayload = "{}"
                },
                new SaleRecord
                {
                    Id = 2,
                    AccountId = "rep-2",
                    ProductName = "Advanced Pack",
                    Quantity = 1,
                    GrossAmount = 800m,
                    CostAmount = 500m,
                    SaleDate = DateTime.UtcNow.AddDays(-2),
                    ImportBatchId = "batch-b",
                    ImportedAt = DateTime.UtcNow,
                    RawPayload = "{}"
                });

            context.CommissionLedgers.AddRange(
                new CommissionLedger
                {
                    SaleRecordId = 1,
                    BeneficiaryId = "rep-1",
                    GrossAmount = 1000m,
                    NetAmount = 600m,
                    CommissionAmount = 100m,
                    ChainDepth = 0,
                    DealSnapshot = "{\"DealType\":\"GrossPercent\"}",
                    CalculationNotes = "10% of gross"
                },
                new CommissionLedger
                {
                    SaleRecordId = 2,
                    BeneficiaryId = "rep-2",
                    GrossAmount = 800m,
                    NetAmount = 300m,
                    CommissionAmount = 80m,
                    ChainDepth = 0,
                    DealSnapshot = "{\"DealType\":\"GrossPercent\"}",
                    CalculationNotes = "10% of gross"
                });

            await context.SaveChangesAsync();

            var controller = new CommissionsController(context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = BuildUser("rep-1", UserRoles.SalesRep)
                    }
                }
            };

            var result = await controller.Index();

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<CommissionDashboardViewModel>(view.Model);
            Assert.Equal(100m, model.TotalCommissionEarned);
            Assert.Single(model.DetailRows);
            Assert.All(model.DetailRows, row => Assert.Equal("rep-1", row.BeneficiaryId));
        }

        [Fact]
        public async Task Index_AsAffiliate_OnlyShowsBeneficiaryRowsForCurrentUser()
        {
            await using var context = CreateContext();

            context.Users.AddRange(
                new ApplicationUser { Id = "affiliate-1", UserName = "affiliate1@example.com", Email = "affiliate1@example.com" },
                new ApplicationUser { Id = "affiliate-2", UserName = "affiliate2@example.com", Email = "affiliate2@example.com" });
            context.SaleRecords.AddRange(
                new SaleRecord
                {
                    Id = 1,
                    AccountId = "affiliate-1",
                    ProductName = "Starter Pack",
                    Quantity = 1,
                    GrossAmount = 1000m,
                    CostAmount = 400m,
                    SaleDate = DateTime.UtcNow.AddDays(-1),
                    ImportBatchId = "batch-a1",
                    ImportedAt = DateTime.UtcNow,
                    RawPayload = "{}"
                },
                new SaleRecord
                {
                    Id = 2,
                    AccountId = "affiliate-2",
                    ProductName = "Advanced Pack",
                    Quantity = 1,
                    GrossAmount = 800m,
                    CostAmount = 500m,
                    SaleDate = DateTime.UtcNow.AddDays(-2),
                    ImportBatchId = "batch-a2",
                    ImportedAt = DateTime.UtcNow,
                    RawPayload = "{}"
                });

            context.CommissionLedgers.AddRange(
                new CommissionLedger
                {
                    SaleRecordId = 1,
                    BeneficiaryId = "affiliate-1",
                    GrossAmount = 1000m,
                    NetAmount = 600m,
                    CommissionAmount = 30m,
                    ChainDepth = 1,
                    DealSnapshot = "{\"DealType\":\"GrossPercent\"}",
                    CalculationNotes = "3% of gross"
                },
                new CommissionLedger
                {
                    SaleRecordId = 2,
                    BeneficiaryId = "affiliate-2",
                    GrossAmount = 800m,
                    NetAmount = 300m,
                    CommissionAmount = 20m,
                    ChainDepth = 1,
                    DealSnapshot = "{\"DealType\":\"GrossPercent\"}",
                    CalculationNotes = "2.5% of gross"
                });

            await context.SaveChangesAsync();

            var controller = new CommissionsController(context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = BuildUser("affiliate-1", UserRoles.Affiliate)
                    }
                }
            };

            var result = await controller.Details();

            var view = Assert.IsType<ViewResult>(result);
            var rows = Assert.IsAssignableFrom<IReadOnlyList<CommissionLedgerRowViewModel>>(view.Model);
            Assert.Single(rows);
            Assert.Equal("affiliate-1", rows[0].BeneficiaryId);
        }

        [Fact]
        public async Task Details_AsOrganizationAdmin_ReturnsAllLedgerRows()
        {
            await using var context = CreateContext();

            context.Users.AddRange(
                new ApplicationUser { Id = "admin-1", UserName = "admin@example.com", Email = "admin@example.com" },
                new ApplicationUser { Id = "rep-1", UserName = "rep1@example.com", Email = "rep1@example.com" },
                new ApplicationUser { Id = "rep-2", UserName = "rep2@example.com", Email = "rep2@example.com" });
            context.SaleRecords.Add(new SaleRecord
            {
                Id = 1,
                AccountId = "rep-1",
                ProductName = "Referral Program",
                Quantity = 1,
                GrossAmount = 1000m,
                CostAmount = 400m,
                SaleDate = DateTime.UtcNow.AddDays(-1),
                ImportBatchId = "batch-admin",
                ImportedAt = DateTime.UtcNow,
                RawPayload = "{}"
            });
            context.CommissionLedgers.AddRange(
                new CommissionLedger
                {
                    SaleRecordId = 1,
                    BeneficiaryId = "rep-1",
                    GrossAmount = 1000m,
                    NetAmount = 600m,
                    CommissionAmount = 100m,
                    ChainDepth = 0,
                    DealSnapshot = "{\"DealType\":\"GrossPercent\"}",
                    CalculationNotes = "10% of gross"
                },
                new CommissionLedger
                {
                    SaleRecordId = 1,
                    BeneficiaryId = "rep-2",
                    GrossAmount = 1000m,
                    NetAmount = 600m,
                    CommissionAmount = 25m,
                    ChainDepth = 1,
                    DealSnapshot = "{\"DealType\":\"ProfitSplit\"}",
                    CalculationNotes = "25% of commission"
                });

            await context.SaveChangesAsync();

            var controller = new CommissionsController(context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = BuildUser("admin-1", UserRoles.OrganizationAdmin)
                    }
                }
            };

            var result = await controller.Details();

            var view = Assert.IsType<ViewResult>(result);
            var rows = Assert.IsAssignableFrom<IReadOnlyList<CommissionLedgerRowViewModel>>(view.Model);
            Assert.Equal(2, rows.Count);
            Assert.Equal(125m, rows.Sum(row => row.CommissionAmount));
        }

        [Fact]
        public async Task Details_AsSalesOrgAdmin_OnlyShowsRowsForThatOrg()
        {
            await using var context = CreateContext();

            context.Users.AddRange(
                new ApplicationUser { Id = "org-admin-1", UserName = "orgadmin@example.com", Email = "orgadmin@example.com", SalesOrgId = 1 },
                new ApplicationUser { Id = "rep-1", UserName = "rep1@example.com", Email = "rep1@example.com", SalesOrgId = 1 },
                new ApplicationUser { Id = "rep-2", UserName = "rep2@example.com", Email = "rep2@example.com", SalesOrgId = 2 });
            context.SaleRecords.AddRange(
                new SaleRecord
                {
                    Id = 1,
                    AccountId = "rep-1",
                    ProductName = "Org One Sale",
                    Quantity = 1,
                    GrossAmount = 500m,
                    CostAmount = 250m,
                    SaleDate = DateTime.UtcNow.AddDays(-1),
                    ImportBatchId = "batch-org-1",
                    ImportedAt = DateTime.UtcNow,
                    RawPayload = "{}"
                },
                new SaleRecord
                {
                    Id = 2,
                    AccountId = "rep-2",
                    ProductName = "Org Two Sale",
                    Quantity = 1,
                    GrossAmount = 900m,
                    CostAmount = 400m,
                    SaleDate = DateTime.UtcNow.AddDays(-2),
                    ImportBatchId = "batch-org-2",
                    ImportedAt = DateTime.UtcNow,
                    RawPayload = "{}"
                });
            context.CommissionLedgers.AddRange(
                new CommissionLedger
                {
                    SaleRecordId = 1,
                    BeneficiaryId = "rep-1",
                    GrossAmount = 500m,
                    NetAmount = 250m,
                    CommissionAmount = 50m,
                    ChainDepth = 0,
                    DealSnapshot = "{\"DealType\":\"GrossPercent\"}",
                    CalculationNotes = "10% of gross"
                },
                new CommissionLedger
                {
                    SaleRecordId = 2,
                    BeneficiaryId = "rep-2",
                    GrossAmount = 900m,
                    NetAmount = 500m,
                    CommissionAmount = 90m,
                    ChainDepth = 0,
                    DealSnapshot = "{\"DealType\":\"GrossPercent\"}",
                    CalculationNotes = "10% of gross"
                });

            await context.SaveChangesAsync();

            var controller = new CommissionsController(context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = BuildUser("org-admin-1", UserRoles.SalesOrgAdmin)
                    }
                }
            };

            var result = await controller.Details();

            var view = Assert.IsType<ViewResult>(result);
            var rows = Assert.IsAssignableFrom<IReadOnlyList<CommissionLedgerRowViewModel>>(view.Model);
            var row = Assert.Single(rows);
            Assert.Equal("rep-1", row.BeneficiaryId);
            Assert.Equal(50m, row.CommissionAmount);
        }

        [Fact]
        public async Task Hierarchy_AsOrganizationAdmin_ReturnsHierarchyViewModelWithOrphansAndLinks()
        {
            await using var context = CreateContext();

            context.Users.AddRange(
                new ApplicationUser { Id = "admin-1", UserName = "admin@example.com", Email = "admin@example.com" },
                new ApplicationUser { Id = "root-1", UserName = "root@example.com", Email = "root@example.com", FirstName = "Root", LastName = "User" },
                new ApplicationUser { Id = "child-1", UserName = "child@example.com", Email = "child@example.com", FirstName = "Child", LastName = "User" });
            context.CommissionDeals.Add(new CommissionDeal
            {
                ApplicationUserId = "child-1",
                DealType = CommissionDealType.GrossPercent,
                Rate = 15m,
                CalculationBasis = CommissionCalculationBasis.DownlineGross
            });
            context.CommissionLinks.Add(new CommissionLink
            {
                DownlineId = "child-1",
                SponsorId = "root-1"
            });

            await context.SaveChangesAsync();

            var controller = new CommissionsController(context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = BuildUser("admin-1", UserRoles.OrganizationAdmin)
                    }
                }
            };

            var result = await controller.Hierarchy();

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<CommissionHierarchyViewModel>(view.Model);
            Assert.Equal(3, model.TotalAccounts);
            Assert.Equal(2, model.RootAccounts);
            Assert.Equal(1, model.LinkedAccounts);
            Assert.Equal(1, model.ConfiguredDeals);
            Assert.Contains(model.Nodes, node => node.Id == "child-1" && node.SponsorId == "root-1");
        }

        [Fact]
        public async Task SaveHierarchy_UpsertsSponsorAndDealAndReturnsUpdatedHierarchy()
        {
            await using var context = CreateContext();

            context.Users.AddRange(
                new ApplicationUser { Id = "admin-1", UserName = "admin@example.com", Email = "admin@example.com" },
                new ApplicationUser { Id = "owner-1", UserName = "owner@example.com", Email = "owner@example.com", FirstName = "Owner", LastName = "User" },
                new ApplicationUser { Id = "child-1", UserName = "child@example.com", Email = "child@example.com", FirstName = "Child", LastName = "User" });

            await context.SaveChangesAsync();

            var controller = new CommissionsController(context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = BuildUser("admin-1", UserRoles.OrganizationAdmin)
                    }
                }
            };

            var result = await controller.SaveHierarchy(new SaveCommissionHierarchyRequest
            {
                AccountId = "child-1",
                SponsorId = "owner-1",
                CommissionDealType = CommissionDealType.NetPercent,
                CommissionCalculationBasis = CommissionCalculationBasis.DownlineNet,
                CommissionRate = 8m
            });

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);

            var link = await context.CommissionLinks.SingleAsync(item => item.DownlineId == "child-1");
            Assert.Equal("owner-1", link.SponsorId);

            var deal = await context.CommissionDeals.SingleAsync(item => item.ApplicationUserId == "child-1");
            Assert.Equal(CommissionDealType.NetPercent, deal.DealType);
            Assert.Equal(CommissionCalculationBasis.DownlineNet, deal.CalculationBasis);
            Assert.Equal(8m, deal.Rate);
        }

        [Fact]
        public async Task SaveHierarchy_WhenCycleWouldBeCreated_ReturnsBadRequest()
        {
            await using var context = CreateContext();

            context.Users.AddRange(
                new ApplicationUser { Id = "admin-1", UserName = "admin@example.com", Email = "admin@example.com" },
                new ApplicationUser { Id = "owner-1", UserName = "owner@example.com", Email = "owner@example.com" },
                new ApplicationUser { Id = "child-1", UserName = "child@example.com", Email = "child@example.com" });
            context.CommissionLinks.Add(new CommissionLink
            {
                DownlineId = "owner-1",
                SponsorId = "child-1"
            });

            await context.SaveChangesAsync();

            var controller = new CommissionsController(context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = BuildUser("admin-1", UserRoles.OrganizationAdmin)
                    }
                }
            };

            var result = await controller.SaveHierarchy(new SaveCommissionHierarchyRequest
            {
                AccountId = "child-1",
                SponsorId = "owner-1"
            });

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task Hierarchy_AsGroupAdmin_PreservesOutOfScopeSponsorWithoutMarkingNodeAsOrphan()
        {
            await using var context = CreateContext();

            context.Users.AddRange(
                new ApplicationUser { Id = "group-admin-1", UserName = "groupadmin@example.com", Email = "groupadmin@example.com", SalesGroupId = "group-a" },
                new ApplicationUser { Id = "child-1", UserName = "child@example.com", Email = "child@example.com", FirstName = "Child", LastName = "User", SalesGroupId = "group-a" },
                new ApplicationUser { Id = "owner-outside", UserName = "outside@example.com", Email = "outside@example.com", FirstName = "Outside", LastName = "Owner", SalesGroupId = "group-b" });
            context.CommissionLinks.Add(new CommissionLink
            {
                DownlineId = "child-1",
                SponsorId = "owner-outside"
            });

            await context.SaveChangesAsync();

            var controller = new CommissionsController(context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = BuildUser("group-admin-1", UserRoles.GroupAdmin)
                    }
                }
            };

            var result = await controller.Hierarchy();

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<CommissionHierarchyViewModel>(view.Model);
            var childNode = Assert.Single(model.Nodes.Where(node => node.Id == "child-1"));
            Assert.Equal("owner-outside", childNode.SponsorId);
            Assert.Equal("Owner outside current scope", childNode.SponsorName);
            Assert.False(childNode.IsOrphan);
            Assert.Equal(1, model.RootAccounts);
            Assert.Equal(1, model.LinkedAccounts);
        }

        [Fact]
        public async Task SaveHierarchy_AllowsUpdatingDealWhenExistingSponsorIsOutsideScopeAndUnchanged()
        {
            await using var context = CreateContext();

            context.Users.AddRange(
                new ApplicationUser { Id = "group-admin-1", UserName = "groupadmin@example.com", Email = "groupadmin@example.com", SalesGroupId = "group-a" },
                new ApplicationUser { Id = "child-1", UserName = "child@example.com", Email = "child@example.com", SalesGroupId = "group-a" },
                new ApplicationUser { Id = "owner-outside", UserName = "outside@example.com", Email = "outside@example.com", SalesGroupId = "group-b" });
            context.CommissionLinks.Add(new CommissionLink
            {
                DownlineId = "child-1",
                SponsorId = "owner-outside"
            });

            await context.SaveChangesAsync();

            var controller = new CommissionsController(context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = BuildUser("group-admin-1", UserRoles.GroupAdmin)
                    }
                }
            };

            var result = await controller.SaveHierarchy(new SaveCommissionHierarchyRequest
            {
                AccountId = "child-1",
                SponsorId = "owner-outside",
                CommissionDealType = CommissionDealType.GrossPercent,
                CommissionCalculationBasis = CommissionCalculationBasis.DownlineGross,
                CommissionRate = 12m
            });

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);

            var link = await context.CommissionLinks.SingleAsync(item => item.DownlineId == "child-1");
            Assert.Equal("owner-outside", link.SponsorId);

            var deal = await context.CommissionDeals.SingleAsync(item => item.ApplicationUserId == "child-1");
            Assert.Equal(12m, deal.Rate);
        }
    }
}
