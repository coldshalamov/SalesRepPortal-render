using System;
using System.Linq;
using System.Threading.Tasks;
using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using LeadManagementPortal.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeadManagementPortal.Tests
{
    public class CommissionCalculationServiceTests
    {
        private static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task CalculateForSaleAsync_WalksUpFullSponsorChain_UsingDownlineCommissionBasis()
        {
            await using var context = CreateContext();

            var seller = new ApplicationUser
            {
                Id = "seller",
                UserName = "seller@example.com",
                Email = "seller@example.com",
                FirstName = "Seller",
                LastName = "One"
            };
            var sponsor = new ApplicationUser
            {
                Id = "sponsor",
                UserName = "sponsor@example.com",
                Email = "sponsor@example.com",
                FirstName = "Sponsor",
                LastName = "One"
            };
            var topSponsor = new ApplicationUser
            {
                Id = "top-sponsor",
                UserName = "topsponsor@example.com",
                Email = "topsponsor@example.com",
                FirstName = "Sponsor",
                LastName = "Two"
            };
            var fourthLevelSponsor = new ApplicationUser
            {
                Id = "fourth-level",
                UserName = "fourth@example.com",
                Email = "fourth@example.com",
                FirstName = "Sponsor",
                LastName = "Three"
            };

            context.Users.AddRange(seller, sponsor, topSponsor, fourthLevelSponsor);
            context.CommissionDeals.AddRange(
                new CommissionDeal
                {
                    ApplicationUserId = seller.Id,
                    DealType = CommissionDealType.GrossPercent,
                    Rate = 10m,
                    CalculationBasis = CommissionCalculationBasis.DownlineGross
                },
                new CommissionDeal
                {
                    ApplicationUserId = sponsor.Id,
                    DealType = CommissionDealType.GrossPercent,
                    Rate = 50m,
                    CalculationBasis = CommissionCalculationBasis.DownlineCommission
                },
                new CommissionDeal
                {
                    ApplicationUserId = topSponsor.Id,
                    DealType = CommissionDealType.GrossPercent,
                    Rate = 10m,
                    CalculationBasis = CommissionCalculationBasis.DownlineCommission
                },
                new CommissionDeal
                {
                    ApplicationUserId = fourthLevelSponsor.Id,
                    DealType = CommissionDealType.GrossPercent,
                    Rate = 10m,
                    CalculationBasis = CommissionCalculationBasis.DownlineCommission
                });

            context.CommissionLinks.AddRange(
                new CommissionLink { DownlineId = seller.Id, SponsorId = sponsor.Id },
                new CommissionLink { DownlineId = sponsor.Id, SponsorId = topSponsor.Id },
                new CommissionLink { DownlineId = topSponsor.Id, SponsorId = fourthLevelSponsor.Id });

            var sale = new SaleRecord
            {
                AccountId = seller.Id,
                ProductName = "GLP-1 Program",
                Quantity = 1,
                GrossAmount = 1000m,
                CostAmount = 400m,
                SaleDate = DateTime.UtcNow.AddDays(-1),
                ImportBatchId = "batch-1",
                ImportedAt = DateTime.UtcNow,
                RawPayload = "{}"
            };

            context.SaleRecords.Add(sale);
            await context.SaveChangesAsync();

            var service = new CommissionCalculationService(context, NullLogger<CommissionCalculationService>.Instance);

            var ledgers = await service.CalculateForSaleAsync(sale);

            Assert.Equal(4, ledgers.Count);

            var ordered = ledgers.OrderBy(l => l.ChainDepth).ToList();
            Assert.Equal("seller", ordered[0].BeneficiaryId);
            Assert.Equal(100m, ordered[0].CommissionAmount);

            Assert.Equal("sponsor", ordered[1].BeneficiaryId);
            Assert.Equal(50m, ordered[1].CommissionAmount);
            Assert.Contains("commission", ordered[1].CalculationNotes, StringComparison.OrdinalIgnoreCase);

            Assert.Equal("top-sponsor", ordered[2].BeneficiaryId);
            Assert.Equal(5m, ordered[2].CommissionAmount);

            Assert.Equal("fourth-level", ordered[3].BeneficiaryId);
            Assert.Equal(0.5m, ordered[3].CommissionAmount);
        }

        [Fact]
        public async Task CalculateForSaleAsync_WhenNetInputsMissing_WritesZeroAmountLedgerWithNotes()
        {
            await using var context = CreateContext();

            var seller = new ApplicationUser
            {
                Id = "seller",
                UserName = "seller@example.com",
                Email = "seller@example.com"
            };

            context.Users.Add(seller);
            context.CommissionDeals.Add(new CommissionDeal
            {
                ApplicationUserId = seller.Id,
                DealType = CommissionDealType.NetPercent,
                Rate = 10m,
                CalculationBasis = CommissionCalculationBasis.DownlineNet
            });

            var sale = new SaleRecord
            {
                AccountId = seller.Id,
                ProductName = "Membership",
                Quantity = 2,
                GrossAmount = 250m,
                CostAmount = null,
                SaleDate = DateTime.UtcNow,
                ImportBatchId = "batch-2",
                ImportedAt = DateTime.UtcNow,
                RawPayload = "{}"
            };

            context.SaleRecords.Add(sale);
            await context.SaveChangesAsync();

            var service = new CommissionCalculationService(context, NullLogger<CommissionCalculationService>.Instance);

            var ledgers = await service.CalculateForSaleAsync(sale);

            var ledger = Assert.Single(ledgers);
            Assert.Equal(0m, ledger.CommissionAmount);
            Assert.Contains("cost", ledger.CalculationNotes, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("missing", ledger.CalculationNotes, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CalculateForSaleAsync_SkipsUsersWithoutCommissionDeal_WithoutThrowing()
        {
            await using var context = CreateContext();

            var seller = new ApplicationUser { Id = "seller", UserName = "seller@example.com", Email = "seller@example.com" };
            var sponsorWithoutDeal = new ApplicationUser { Id = "sponsor", UserName = "sponsor@example.com", Email = "sponsor@example.com" };
            var topSponsor = new ApplicationUser { Id = "top", UserName = "top@example.com", Email = "top@example.com" };

            context.Users.AddRange(seller, sponsorWithoutDeal, topSponsor);
            context.CommissionDeals.Add(new CommissionDeal
            {
                ApplicationUserId = topSponsor.Id,
                DealType = CommissionDealType.GrossPercent,
                Rate = 5m,
                CalculationBasis = CommissionCalculationBasis.DownlineCommission
            });
            context.CommissionLinks.AddRange(
                new CommissionLink { DownlineId = seller.Id, SponsorId = sponsorWithoutDeal.Id },
                new CommissionLink { DownlineId = sponsorWithoutDeal.Id, SponsorId = topSponsor.Id });

            var sale = new SaleRecord
            {
                AccountId = seller.Id,
                ProductName = "Membership",
                Quantity = 1,
                GrossAmount = 100m,
                CostAmount = 20m,
                SaleDate = DateTime.UtcNow,
                ImportBatchId = "batch-3",
                ImportedAt = DateTime.UtcNow,
                RawPayload = "{}"
            };

            context.SaleRecords.Add(sale);
            await context.SaveChangesAsync();

            var service = new CommissionCalculationService(context, NullLogger<CommissionCalculationService>.Instance);

            var ledgers = await service.CalculateForSaleAsync(sale);

            var ledger = Assert.Single(ledgers);
            Assert.Equal("top", ledger.BeneficiaryId);
            Assert.Equal(0m, ledger.CommissionAmount);
            Assert.Contains("unavailable", ledger.CalculationNotes, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CalculateForSaleAsync_StoresImmutableDealSnapshotFromCurrentDeal()
        {
            await using var context = CreateContext();

            var seller = new ApplicationUser { Id = "seller", UserName = "seller@example.com", Email = "seller@example.com" };
            context.Users.Add(seller);

            context.CommissionDeals.Add(new CommissionDeal
            {
                ApplicationUserId = seller.Id,
                DealType = CommissionDealType.ProfitSplit,
                Rate = 17.5m,
                BaseCost = 42m,
                CalculationBasis = CommissionCalculationBasis.DownlineNet
            });

            var sale = new SaleRecord
            {
                AccountId = seller.Id,
                ProductName = "Program",
                Quantity = 1,
                GrossAmount = 200m,
                CostAmount = 50m,
                SaleDate = DateTime.UtcNow,
                ImportBatchId = "batch-4",
                ImportedAt = DateTime.UtcNow,
                RawPayload = "{}"
            };

            context.SaleRecords.Add(sale);
            await context.SaveChangesAsync();

            var service = new CommissionCalculationService(context, NullLogger<CommissionCalculationService>.Instance);

            var ledgers = await service.CalculateForSaleAsync(sale);

            var ledger = Assert.Single(ledgers);
            Assert.Contains("\"DealType\":\"ProfitSplit\"", ledger.DealSnapshot, StringComparison.Ordinal);
            Assert.Contains("\"Rate\":17.5", ledger.DealSnapshot, StringComparison.Ordinal);
            Assert.Contains("\"BaseCost\":42", ledger.DealSnapshot, StringComparison.Ordinal);
            Assert.Contains("\"CalculationBasis\":\"DownlineNet\"", ledger.DealSnapshot, StringComparison.Ordinal);
        }

        [Fact]
        public async Task CalculateForSaleAsync_WhenCycleEncountered_StopsWalkAndKeepsExistingRowsFinite()
        {
            await using var context = CreateContext();

            var seller = new ApplicationUser { Id = "seller", UserName = "seller@example.com", Email = "seller@example.com" };
            var sponsor = new ApplicationUser { Id = "sponsor", UserName = "sponsor@example.com", Email = "sponsor@example.com" };

            context.Users.AddRange(seller, sponsor);
            context.CommissionDeals.AddRange(
                new CommissionDeal
                {
                    ApplicationUserId = seller.Id,
                    DealType = CommissionDealType.GrossPercent,
                    Rate = 10m,
                    CalculationBasis = CommissionCalculationBasis.DownlineGross
                },
                new CommissionDeal
                {
                    ApplicationUserId = sponsor.Id,
                    DealType = CommissionDealType.GrossPercent,
                    Rate = 10m,
                    CalculationBasis = CommissionCalculationBasis.DownlineCommission
                });
            context.CommissionLinks.AddRange(
                new CommissionLink { DownlineId = seller.Id, SponsorId = sponsor.Id },
                new CommissionLink { DownlineId = sponsor.Id, SponsorId = seller.Id });

            var sale = new SaleRecord
            {
                AccountId = seller.Id,
                ProductName = "Cycle Test",
                Quantity = 1,
                GrossAmount = 100m,
                CostAmount = 20m,
                SaleDate = DateTime.UtcNow,
                ImportBatchId = "batch-5",
                ImportedAt = DateTime.UtcNow,
                RawPayload = "{}"
            };

            context.SaleRecords.Add(sale);
            await context.SaveChangesAsync();

            var service = new CommissionCalculationService(context, NullLogger<CommissionCalculationService>.Instance);

            var ledgers = await service.CalculateForSaleAsync(sale);

            Assert.Equal(2, ledgers.Count);
            Assert.Contains("cycle", ledgers.Last().CalculationNotes, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CommissionLink_ModelHasSelfSponsorCheckConstraint()
        {
            using var context = CreateContext();

            var entityType = context.Model.FindEntityType(typeof(CommissionLink));

            Assert.NotNull(entityType);
            Assert.Contains(
                entityType!.GetCheckConstraints(),
                constraint => constraint.Name == "CK_CommissionLinks_NoSelfSponsor");
        }
    }
}
