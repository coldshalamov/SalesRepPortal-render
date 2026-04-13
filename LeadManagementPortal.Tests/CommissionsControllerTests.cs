using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using LeadManagementPortal.Controllers;
using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using LeadManagementPortal.Models.ViewModels;
using LeadManagementPortal.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

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

        private static ClaimsPrincipal BuildUser(string userId, string role, string? name = null)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId),
                new(ClaimTypes.Role, role)
            };

            if (!string.IsNullOrWhiteSpace(name))
            {
                claims.Add(new Claim(ClaimTypes.Name, name));
            }

            var identity = new ClaimsIdentity(claims, "TestAuth");
            return new ClaimsPrincipal(identity);
        }

        private static Mock<ICommissionControlPlaneService> CreateControlPlaneServiceMock()
        {
            var service = new Mock<ICommissionControlPlaneService>(MockBehavior.Strict);
            service.Setup(s => s.BuildStatementAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommissionStatementSummary());
            return service;
        }

        [Fact]
        public async Task Index_AsSalesRep_OnlyShowsStatementRowsForCurrentUser()
        {
            await using var context = CreateContext();
            context.Users.Add(new ApplicationUser
            {
                Id = "rep-1",
                UserName = "rep1@example.com",
                Email = "rep1@example.com",
                FirstName = "Rep",
                LastName = "One"
            });
            context.SaleRecords.Add(new SaleRecord
            {
                Id = 50,
                AccountId = "rep-1",
                ProductName = "Legacy Referral",
                Quantity = 1,
                GrossAmount = 200m,
                CostAmount = 50m,
                SaleDate = new DateTime(2026, 3, 20),
                ImportBatchId = "legacy-batch",
                ImportedAt = DateTime.UtcNow,
                RawPayload = "{}"
            });
            context.CommissionLedgers.Add(new CommissionLedger
            {
                Id = 51,
                SaleRecordId = 50,
                BeneficiaryId = "rep-1",
                GrossAmount = 200m,
                NetAmount = 150m,
                CommissionAmount = 30m,
                ChainDepth = 0,
                DealSnapshot = "{\"DealType\":\"GrossPercent\",\"CalculationBasis\":\"DownlineGross\"}",
                CalculationNotes = "Legacy 15%"
            });
            await context.SaveChangesAsync();

            var service = new Mock<ICommissionControlPlaneService>(MockBehavior.Strict);
            service.Setup(s => s.BuildStatementAsync("rep-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommissionStatementSummary
                {
                    TotalEarned = 100m,
                    TotalAdjustments = -10m,
                    TotalPaid = 25m,
                    OutstandingBalance = 65m,
                    EarnedRows = new List<CommissionStatementRow>
                    {
                        new()
                        {
                            LedgerEntryId = 12,
                            SaleDate = new DateTime(2026, 4, 1),
                            BusinessAccountName = "Acme Clinic",
                            ProductName = "Starter Pack",
                            GrossAmount = 1000m,
                            NetAmount = 600m,
                            CommissionAmount = 100m,
                            PaidAmount = 25m,
                            OutstandingAmount = 75m,
                            CalculationType = "PercentOfGross",
                            CalculationDetails = "{\"calculationType\":\"PercentOfGross\"}"
                        }
                    }
                });

            var controller = new CommissionsController(context, service.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = BuildUser("rep-1", UserRoles.SalesRep, "Rep One")
                    }
                }
            };

            var result = await controller.Index();

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<CommissionDashboardViewModel>(view.Model);
            Assert.False(model.IsAdminView);
            Assert.Equal(130m, model.TotalCommissionEarned);
            Assert.Equal(-10m, model.TotalAdjustments);
            Assert.Equal(25m, model.TotalPaid);
            Assert.Equal(95m, model.OutstandingBalance);
            Assert.Equal(2, model.DetailRows.Count);
            Assert.All(model.DetailRows, row => Assert.Equal("rep-1", row.BeneficiaryId));
            Assert.Contains(model.DetailRows, row => row.ProductName == "Legacy Referral" && row.CommissionAmount == 30m);
            service.Verify(s => s.BuildStatementAsync("rep-1", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Details_AsAffiliate_OnlyShowsStatementRowsForCurrentUser()
        {
            await using var context = CreateContext();
            var service = new Mock<ICommissionControlPlaneService>(MockBehavior.Strict);
            service.Setup(s => s.BuildStatementAsync("affiliate-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommissionStatementSummary
                {
                    EarnedRows = new List<CommissionStatementRow>
                    {
                        new()
                        {
                            LedgerEntryId = 21,
                            SaleDate = new DateTime(2026, 4, 3),
                            BusinessAccountName = "Beta Wellness",
                            ProductName = "TRT",
                            GrossAmount = 800m,
                            NetAmount = 300m,
                            CommissionAmount = 20m,
                            PaidAmount = 0m,
                            OutstandingAmount = 20m,
                            CalculationType = "PercentOfRecipientCommission",
                            CalculationDetails = "{\"calculationType\":\"PercentOfRecipientCommission\"}"
                        }
                    }
                });

            var controller = new CommissionsController(context, service.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = BuildUser("affiliate-1", UserRoles.Affiliate, "Affiliate One")
                    }
                }
            };

            var result = await controller.Details();

            var view = Assert.IsType<ViewResult>(result);
            var rows = Assert.IsAssignableFrom<IReadOnlyList<CommissionLedgerRowViewModel>>(view.Model);
            var row = Assert.Single(rows);
            Assert.Equal("affiliate-1", row.BeneficiaryId);
            Assert.Equal("Affiliate One", row.BeneficiaryName);
            Assert.Equal(20m, row.CommissionAmount);
            service.Verify(s => s.BuildStatementAsync("affiliate-1", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Details_AsOrganizationAdmin_ReturnsAllLedgerRows()
        {
            await using var context = CreateContext();

            var admin = new ApplicationUser { Id = "admin-1", UserName = "admin@example.com", Email = "admin@example.com" };
            var rep1 = new ApplicationUser { Id = "rep-1", UserName = "rep1@example.com", Email = "rep1@example.com", FirstName = "Rep", LastName = "One" };
            var rep2 = new ApplicationUser { Id = "rep-2", UserName = "rep2@example.com", Email = "rep2@example.com", FirstName = "Rep", LastName = "Two" };
            var account = new BusinessAccount { Id = 100, Name = "Gamma Pharmacy", IsActive = true };
            var saleEvent = new SaleEvent
            {
                Id = 200,
                BusinessAccountId = account.Id,
                SaleDate = new DateTime(2026, 4, 5),
                ProductName = "Bulk GLP-1",
                Quantity = 1,
                GrossAmount = 1000m,
                CostAmount = 400m,
                SourceSystem = "manual",
                RawPayloadJson = "{}",
                PostedById = admin.Id
            };

            context.Users.AddRange(admin, rep1, rep2);
            context.BusinessAccounts.Add(account);
            context.SaleEvents.Add(saleEvent);
            context.CommissionLedgerEntries.AddRange(
                new CommissionLedgerEntry
                {
                    Id = 301,
                    SaleEventId = saleEvent.Id,
                    BeneficiaryId = rep1.Id,
                    CommissionAmount = 100m,
                    GrossAmount = 1000m,
                    NetAmount = 600m,
                    CalculationType = CommissionRecipientCalculationType.PercentOfGross,
                    CalculationDetailsJson = "{\"calculationType\":\"PercentOfGross\",\"rateOrAmount\":10}",
                    EarnedAtUtc = DateTime.UtcNow
                },
                new CommissionLedgerEntry
                {
                    Id = 302,
                    SaleEventId = saleEvent.Id,
                    BeneficiaryId = rep2.Id,
                    CommissionAmount = 25m,
                    GrossAmount = 1000m,
                    NetAmount = 600m,
                    CalculationType = CommissionRecipientCalculationType.PercentOfRecipientCommission,
                    CalculationDetailsJson = "{\"calculationType\":\"PercentOfRecipientCommission\",\"rateOrAmount\":25}",
                    EarnedAtUtc = DateTime.UtcNow
                });
            context.SaleRecords.Add(new SaleRecord
            {
                Id = 210,
                AccountId = rep1.Id,
                ProductName = "Legacy Consult",
                Quantity = 1,
                GrossAmount = 500m,
                CostAmount = 200m,
                SaleDate = new DateTime(2026, 4, 1),
                ImportBatchId = "legacy-admin",
                ImportedAt = DateTime.UtcNow,
                RawPayload = "{}"
            });
            context.CommissionLedgers.Add(new CommissionLedger
            {
                Id = 211,
                SaleRecordId = 210,
                BeneficiaryId = rep1.Id,
                GrossAmount = 500m,
                NetAmount = 300m,
                CommissionAmount = 50m,
                ChainDepth = 0,
                DealSnapshot = "{\"DealType\":\"GrossPercent\",\"CalculationBasis\":\"DownlineGross\"}",
                CalculationNotes = "Legacy 10%"
            });
            await context.SaveChangesAsync();

            var service = new Mock<ICommissionControlPlaneService>(MockBehavior.Strict);
            var controller = new CommissionsController(context, service.Object)
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
            Assert.Equal(3, rows.Count);
            Assert.Equal(175m, rows.Sum(row => row.CommissionAmount));
            Assert.Contains(rows, row => row.BeneficiaryId == "rep-1" && row.BusinessAccountName == "Gamma Pharmacy");
            Assert.Contains(rows, row => row.BeneficiaryId == "rep-2" && row.CommissionAmount == 25m);
            Assert.Contains(rows, row => row.ProductName == "Legacy Consult" && row.CommissionAmount == 50m);
            service.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Index_AsOrganizationAdmin_ReturnsGlobalDashboardCounts()
        {
            await using var context = CreateContext();

            var admin = new ApplicationUser { Id = "admin-1", UserName = "admin@example.com", Email = "admin@example.com" };
            var rep = new ApplicationUser { Id = "rep-1", UserName = "rep@example.com", Email = "rep@example.com", FirstName = "Rep", LastName = "One" };
            var account = new BusinessAccount { Id = 101, Name = "Delta Care", IsActive = true };
            var activeAgreement = new CommissionAgreement
            {
                Id = 500,
                BusinessAccountId = account.Id,
                Name = "Delta 2026",
                EffectiveStartDate = new DateTime(2026, 1, 1),
                EffectiveEndDate = new DateTime(2026, 12, 31),
                IsActive = true
            };
            var saleEvent = new SaleEvent
            {
                Id = 600,
                BusinessAccountId = account.Id,
                SaleDate = DateTime.UtcNow.Date,
                ProductName = "Consult",
                Quantity = 1,
                GrossAmount = 250m,
                CostAmount = 100m,
                SourceSystem = "manual",
                RawPayloadJson = "{}",
                PostedById = admin.Id
            };

            context.Users.AddRange(admin, rep);
            context.BusinessAccounts.Add(account);
            context.CommissionAgreements.Add(activeAgreement);
            context.SaleEvents.Add(saleEvent);
            context.CommissionLedgerEntries.Add(new CommissionLedgerEntry
            {
                Id = 700,
                SaleEventId = saleEvent.Id,
                CommissionAgreementId = activeAgreement.Id,
                BeneficiaryId = rep.Id,
                CommissionAmount = 40m,
                GrossAmount = 250m,
                NetAmount = 150m,
                CalculationType = CommissionRecipientCalculationType.PercentOfGross,
                CalculationDetailsJson = "{\"calculationType\":\"PercentOfGross\",\"rateOrAmount\":16}",
                EarnedAtUtc = DateTime.UtcNow
            });
            context.CommissionAdjustments.Add(new CommissionAdjustment
            {
                Id = 701,
                BeneficiaryId = rep.Id,
                Amount = -5m,
                Reason = "Correction",
                CreatedById = admin.Id,
                CreatedAtUtc = DateTime.UtcNow
            });
            context.PayoutBatches.Add(new PayoutBatch
            {
                Id = 702,
                Reference = "CHK-100",
                CreatedById = admin.Id,
                CreatedAtUtc = DateTime.UtcNow,
                PaidAtUtc = DateTime.UtcNow
            });
            context.PayoutEntries.Add(new PayoutEntry
            {
                Id = 703,
                PayoutBatchId = 702,
                BeneficiaryId = rep.Id,
                CommissionLedgerEntryId = 700,
                Amount = 10m
            });
            context.ImportBatches.Add(new ImportBatch
            {
                Id = 704,
                SourceSystem = "csv",
                Status = ImportBatchStatus.PendingReview,
                ReceivedAtUtc = DateTime.UtcNow,
                Rows = new List<ImportRow>
                {
                    new()
                    {
                        Id = 705,
                        RowNumber = 1,
                        Status = ImportRowStatus.PendingReview,
                        RawPayloadJson = "{}",
                        MappedPayloadJson = "{}"
                    },
                    new()
                    {
                        Id = 706,
                        RowNumber = 2,
                        Status = ImportRowStatus.ReadyToPost,
                        RawPayloadJson = "{}",
                        MappedPayloadJson = "{}"
                    }
                }
            });

            await context.SaveChangesAsync();

            var service = new Mock<ICommissionControlPlaneService>(MockBehavior.Strict);
            var controller = new CommissionsController(context, service.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = BuildUser("admin-1", UserRoles.OrganizationAdmin)
                    }
                }
            };

            var result = await controller.Index();

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<CommissionDashboardViewModel>(view.Model);
            Assert.True(model.IsAdminView);
            Assert.Equal(40m, model.TotalCommissionEarned);
            Assert.Equal(-5m, model.TotalAdjustments);
            Assert.Equal(10m, model.TotalPaid);
            Assert.Equal(25m, model.OutstandingBalance);
            Assert.Equal(1, model.BusinessAccountCount);
            Assert.Equal(1, model.ActiveAgreementCount);
            Assert.Equal(1, model.PendingReviewRows);
            Assert.Equal(1, model.ReadyToPostRows);
            Assert.Single(model.RecentImportBatches);
            Assert.Single(model.OutstandingBeneficiaryBalances);
            service.VerifyNoOtherCalls();
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

            var controller = new CommissionsController(context, CreateControlPlaneServiceMock().Object)
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

            var controller = new CommissionsController(context, CreateControlPlaneServiceMock().Object)
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

            var controller = new CommissionsController(context, CreateControlPlaneServiceMock().Object)
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

            var controller = new CommissionsController(context, CreateControlPlaneServiceMock().Object)
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
            var childNode = Assert.Single(model.Nodes, node => node.Id == "child-1");
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

            var controller = new CommissionsController(context, CreateControlPlaneServiceMock().Object)
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
