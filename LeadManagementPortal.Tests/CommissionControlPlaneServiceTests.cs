using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using LeadManagementPortal.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeadManagementPortal.Tests
{
    public class CommissionControlPlaneServiceTests
    {
        private static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task EvaluateBatchAsync_WhenNoAgreementMatches_KeepsRowPendingReview()
        {
            await using var context = CreateContext();
            context.BusinessAccounts.Add(new BusinessAccount { Id = 10, Name = "Acme Wellness", IsActive = true });
            context.ImportBatches.Add(new ImportBatch
            {
                Id = 5,
                SourceSystem = "csv",
                Status = ImportBatchStatus.PendingReview,
                ReceivedAtUtc = DateTime.UtcNow
            });
            context.ImportRows.Add(new ImportRow
            {
                ImportBatchId = 5,
                RowNumber = 1,
                Status = ImportRowStatus.PendingReview,
                BusinessAccountName = "Acme Wellness",
                ProductName = "GLP-1",
                Quantity = 1,
                GrossAmount = 250m,
                SaleDate = DateTime.UtcNow.Date,
                RawPayloadJson = "{\"Account\":\"Acme Wellness\"}"
            });
            await context.SaveChangesAsync();

            var service = new CommissionControlPlaneService(context, NullLogger<CommissionControlPlaneService>.Instance);

            await service.EvaluateBatchAsync(5);

            var row = await context.ImportRows.SingleAsync();
            Assert.Equal(ImportRowStatus.PendingReview, row.Status);
            Assert.Null(row.SelectedAgreementId);
        }

        [Fact]
        public async Task PostReadyRowsAsync_CreatesSaleEvent_AndMultiRecipientLedgerRows()
        {
            await using var context = CreateContext();
            var admin = new ApplicationUser { Id = "admin-1", UserName = "admin@example.com", Email = "admin@example.com" };
            var rep = new ApplicationUser { Id = "rep-1", UserName = "rep@example.com", Email = "rep@example.com", FirstName = "Rep", LastName = "One" };
            var affiliate = new ApplicationUser { Id = "aff-1", UserName = "aff@example.com", Email = "aff@example.com", FirstName = "Aff", LastName = "One" };
            context.Users.AddRange(admin, rep, affiliate);

            context.BusinessAccounts.Add(new BusinessAccount { Id = 11, Name = "Beta Clinic", IsActive = true });
            context.CommissionAgreements.Add(new CommissionAgreement
            {
                Id = 20,
                BusinessAccountId = 11,
                Name = "Beta Clinic 2026",
                IsActive = true,
                EffectiveStartDate = new DateTime(2026, 1, 1),
                EffectiveEndDate = new DateTime(2026, 12, 31)
            });
            context.CommissionAgreementRecipients.AddRange(
                new CommissionAgreementRecipient
                {
                    Id = 30,
                    CommissionAgreementId = 20,
                    BeneficiaryId = rep.Id,
                    CalculationType = CommissionRecipientCalculationType.PercentOfGross,
                    RateOrAmount = 10m,
                    SortOrder = 1
                },
                new CommissionAgreementRecipient
                {
                    Id = 31,
                    CommissionAgreementId = 20,
                    BeneficiaryId = affiliate.Id,
                    CalculationType = CommissionRecipientCalculationType.PercentOfRecipientCommission,
                    RateOrAmount = 25m,
                    BasisRecipientId = 30,
                    SortOrder = 2
                });
            context.ImportBatches.Add(new ImportBatch
            {
                Id = 6,
                SourceSystem = "csv",
                Status = ImportBatchStatus.ReadyToPost,
                ReceivedAtUtc = DateTime.UtcNow
            });
            context.ImportRows.Add(new ImportRow
            {
                Id = 40,
                ImportBatchId = 6,
                RowNumber = 1,
                Status = ImportRowStatus.ReadyToPost,
                BusinessAccountId = 11,
                SelectedAgreementId = 20,
                BusinessAccountName = "Beta Clinic",
                ProductName = "Bulk GLP-1",
                Quantity = 1,
                GrossAmount = 1000m,
                CostAmount = 400m,
                SaleDate = new DateTime(2026, 4, 12),
                RawPayloadJson = "{\"Account\":\"Beta Clinic\"}"
            });
            await context.SaveChangesAsync();

            var service = new CommissionControlPlaneService(context, NullLogger<CommissionControlPlaneService>.Instance);

            await service.PostReadyRowsAsync(6, admin.Id);

            Assert.Equal(1, await context.SaleEvents.CountAsync());
            Assert.Equal(2, await context.CommissionLedgerEntries.CountAsync());

            var ledgerEntries = await context.CommissionLedgerEntries.OrderBy(e => e.Id).ToListAsync();
            Assert.Equal(100m, ledgerEntries.Single(e => e.BeneficiaryId == rep.Id).CommissionAmount);
            Assert.Equal(25m, ledgerEntries.Single(e => e.BeneficiaryId == affiliate.Id).CommissionAmount);

            var row = await context.ImportRows.SingleAsync();
            Assert.Equal(ImportRowStatus.Posted, row.Status);
            Assert.NotNull(row.SaleEventId);
        }

        [Fact]
        public async Task CreateBatchFromRawRowsAsync_WithImportProfile_AppliesMappingsAndMatchesAgreement()
        {
            await using var context = CreateContext();
            context.BusinessAccounts.Add(new BusinessAccount
            {
                Id = 100,
                Name = "Acme Wellness",
                ExternalKey = "acct-100",
                IsActive = true
            });
            context.CommissionAgreements.Add(new CommissionAgreement
            {
                Id = 101,
                BusinessAccountId = 100,
                Name = "Acme 2026",
                IsActive = true,
                EffectiveStartDate = new DateTime(2026, 1, 1),
                EffectiveEndDate = new DateTime(2026, 12, 31)
            });
            context.ImportProfiles.Add(new ImportProfile
            {
                Id = 102,
                Name = "Sheet Profile",
                IsActive = true,
                ColumnMappingsJson = JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    ["BusinessAccountExternalKey"] = "Account Key",
                    ["ProductName"] = "Medication",
                    ["Quantity"] = "Units",
                    ["GrossAmount"] = "Gross",
                    ["CostAmount"] = "Cost",
                    ["SaleDate"] = "Sold On"
                })
            });
            await context.SaveChangesAsync();

            var service = new CommissionControlPlaneService(context, NullLogger<CommissionControlPlaneService>.Instance);

            var batch = await service.CreateBatchFromRawRowsAsync(
                "csv",
                new[]
                {
                    (IDictionary<string, string?>)new Dictionary<string, string?>
                    {
                        ["Account Key"] = "acct-100",
                        ["Medication"] = "GLP-1",
                        ["Units"] = "2",
                        ["Gross"] = "300.00",
                        ["Cost"] = "100.00",
                        ["Sold On"] = "2026-04-12"
                    }
                },
                102,
                uploadedById: null,
                sourceFileName: "import.csv");

            var row = await context.ImportRows.SingleAsync(r => r.ImportBatchId == batch.Id);
            Assert.Equal(ImportRowStatus.ReadyToPost, row.Status);
            Assert.Equal(100, row.BusinessAccountId);
            Assert.Equal(101, row.SelectedAgreementId);
            Assert.Equal("GLP-1", row.ProductName);
            Assert.Equal(2, row.Quantity);
            Assert.Equal(300m, row.GrossAmount);
            Assert.Equal(100m, row.CostAmount);
        }

        [Fact]
        public async Task PostReadyRowsAsync_WhenNetBasedRecipientHasNoCost_KeepsRowPendingReview()
        {
            await using var context = CreateContext();
            var admin = new ApplicationUser { Id = "admin-1", UserName = "admin@example.com", Email = "admin@example.com" };
            var rep = new ApplicationUser { Id = "rep-1", UserName = "rep@example.com", Email = "rep@example.com" };
            context.Users.AddRange(admin, rep);
            context.BusinessAccounts.Add(new BusinessAccount { Id = 110, Name = "Net Clinic", IsActive = true });
            context.CommissionAgreements.Add(new CommissionAgreement
            {
                Id = 111,
                BusinessAccountId = 110,
                Name = "Net 2026",
                IsActive = true,
                EffectiveStartDate = new DateTime(2026, 1, 1),
                EffectiveEndDate = new DateTime(2026, 12, 31)
            });
            context.CommissionAgreementRecipients.Add(new CommissionAgreementRecipient
            {
                Id = 112,
                CommissionAgreementId = 111,
                BeneficiaryId = rep.Id,
                CalculationType = CommissionRecipientCalculationType.PercentOfNet,
                RateOrAmount = 10m,
                SortOrder = 1
            });
            context.ImportBatches.Add(new ImportBatch
            {
                Id = 113,
                SourceSystem = "csv",
                Status = ImportBatchStatus.ReadyToPost,
                ReceivedAtUtc = DateTime.UtcNow
            });
            context.ImportRows.Add(new ImportRow
            {
                Id = 114,
                ImportBatchId = 113,
                RowNumber = 1,
                Status = ImportRowStatus.ReadyToPost,
                BusinessAccountId = 110,
                SelectedAgreementId = 111,
                ProductName = "TRT",
                Quantity = 1,
                GrossAmount = 500m,
                CostAmount = null,
                SaleDate = new DateTime(2026, 4, 12),
                RawPayloadJson = "{}",
                MappedPayloadJson = "{}"
            });
            await context.SaveChangesAsync();

            var service = new CommissionControlPlaneService(context, NullLogger<CommissionControlPlaneService>.Instance);

            await service.PostReadyRowsAsync(113, admin.Id);

            var row = await context.ImportRows.SingleAsync(r => r.Id == 114);
            Assert.Equal(ImportRowStatus.PendingReview, row.Status);
            Assert.Contains("Cost amount is required", row.ReviewNotes, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, await context.SaleEvents.CountAsync());
            Assert.Equal(0, await context.CommissionLedgerEntries.CountAsync());
        }

        [Fact]
        public async Task BuildStatementAsync_IncludesAdjustments_Payouts_AndOutstandingBalance()
        {
            await using var context = CreateContext();
            var beneficiary = new ApplicationUser { Id = "rep-1", UserName = "rep@example.com", Email = "rep@example.com", FirstName = "Rep", LastName = "One" };
            var admin = new ApplicationUser { Id = "admin-1", UserName = "admin@example.com", Email = "admin@example.com" };
            context.Users.AddRange(beneficiary, admin);

            context.BusinessAccounts.Add(new BusinessAccount { Id = 12, Name = "Gamma Pharmacy", IsActive = true });
            context.CommissionAgreements.Add(new CommissionAgreement
            {
                Id = 21,
                BusinessAccountId = 12,
                Name = "Gamma 2026",
                IsActive = true,
                EffectiveStartDate = new DateTime(2026, 1, 1),
                EffectiveEndDate = new DateTime(2026, 12, 31)
            });
            context.SaleEvents.Add(new SaleEvent
            {
                Id = 50,
                BusinessAccountId = 12,
                SaleDate = new DateTime(2026, 4, 1),
                ProductName = "TRT",
                Quantity = 1,
                GrossAmount = 200m,
                CostAmount = 100m,
                SourceSystem = "manual",
                RawPayloadJson = "{}",
                PostedById = admin.Id
            });
            context.CommissionLedgerEntries.Add(new CommissionLedgerEntry
            {
                Id = 60,
                SaleEventId = 50,
                CommissionAgreementId = 21,
                BeneficiaryId = beneficiary.Id,
                CommissionAmount = 100m,
                GrossAmount = 200m,
                NetAmount = 100m,
                CalculationType = CommissionRecipientCalculationType.PercentOfGross,
                EarnedAtUtc = DateTime.UtcNow,
                CalculationDetailsJson = "{}"
            });
            context.CommissionAdjustments.Add(new CommissionAdjustment
            {
                Id = 70,
                BeneficiaryId = beneficiary.Id,
                Amount = -20m,
                Reason = "Correction",
                Notes = "Commission corrected",
                CreatedById = admin.Id,
                CreatedAtUtc = DateTime.UtcNow
            });
            context.PayoutBatches.Add(new PayoutBatch
            {
                Id = 80,
                Reference = "CHK-001",
                Notes = "April payout",
                CreatedById = admin.Id,
                CreatedAtUtc = DateTime.UtcNow,
                PaidAtUtc = DateTime.UtcNow
            });
            context.PayoutEntries.Add(new PayoutEntry
            {
                PayoutBatchId = 80,
                BeneficiaryId = beneficiary.Id,
                CommissionLedgerEntryId = 60,
                Amount = 30m
            });
            await context.SaveChangesAsync();

            var service = new CommissionControlPlaneService(context, NullLogger<CommissionControlPlaneService>.Instance);

            var statement = await service.BuildStatementAsync(beneficiary.Id);

            Assert.Equal(100m, statement.TotalEarned);
            Assert.Equal(-20m, statement.TotalAdjustments);
            Assert.Equal(30m, statement.TotalPaid);
            Assert.Equal(50m, statement.OutstandingBalance);
        }

        [Fact]
        public async Task CreatePayoutBatchAsync_WhenSelectionExceedsOutstanding_Throws()
        {
            await using var context = CreateContext();
            var beneficiary = new ApplicationUser { Id = "rep-1", UserName = "rep@example.com", Email = "rep@example.com" };
            var admin = new ApplicationUser { Id = "admin-1", UserName = "admin@example.com", Email = "admin@example.com" };
            context.Users.AddRange(beneficiary, admin);
            context.BusinessAccounts.Add(new BusinessAccount { Id = 120, Name = "Payout Pharmacy", IsActive = true });
            context.CommissionAgreements.Add(new CommissionAgreement
            {
                Id = 121,
                BusinessAccountId = 120,
                Name = "Payout 2026",
                IsActive = true,
                EffectiveStartDate = new DateTime(2026, 1, 1),
                EffectiveEndDate = new DateTime(2026, 12, 31)
            });
            context.SaleEvents.Add(new SaleEvent
            {
                Id = 122,
                BusinessAccountId = 120,
                SaleDate = new DateTime(2026, 4, 1),
                ProductName = "Consult",
                Quantity = 1,
                GrossAmount = 250m,
                CostAmount = 100m,
                SourceSystem = "manual",
                RawPayloadJson = "{}",
                PostedById = admin.Id
            });
            context.CommissionLedgerEntries.Add(new CommissionLedgerEntry
            {
                Id = 123,
                SaleEventId = 122,
                CommissionAgreementId = 121,
                BeneficiaryId = beneficiary.Id,
                CommissionAmount = 100m,
                GrossAmount = 250m,
                NetAmount = 150m,
                CalculationType = CommissionRecipientCalculationType.PercentOfGross,
                CalculationDetailsJson = "{}",
                EarnedAtUtc = DateTime.UtcNow
            });
            context.PayoutBatches.Add(new PayoutBatch
            {
                Id = 124,
                Reference = "CHK-100",
                CreatedById = admin.Id,
                CreatedAtUtc = DateTime.UtcNow,
                PaidAtUtc = DateTime.UtcNow
            });
            context.PayoutEntries.Add(new PayoutEntry
            {
                Id = 125,
                PayoutBatchId = 124,
                BeneficiaryId = beneficiary.Id,
                CommissionLedgerEntryId = 123,
                Amount = 20m
            });
            await context.SaveChangesAsync();

            var service = new CommissionControlPlaneService(context, NullLogger<CommissionControlPlaneService>.Instance);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreatePayoutBatchAsync(
                admin.Id,
                "CHK-101",
                null,
                new[]
                {
                    new PayoutSelectionRequest
                    {
                        BeneficiaryId = beneficiary.Id,
                        ItemType = "ledger",
                        SourceId = 123,
                        Amount = 90m
                    }
                }));

            Assert.Equal(1, await context.PayoutBatches.CountAsync());
        }
    }
}
