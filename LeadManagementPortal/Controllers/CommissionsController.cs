using System.Security.Claims;
using System.Text.Json;
using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using LeadManagementPortal.Models.ViewModels;
using LeadManagementPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeadManagementPortal.Controllers
{
    [Authorize]
    public class CommissionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICommissionControlPlaneService _commissionControlPlaneService;

        public CommissionsController(ApplicationDbContext context, ICommissionControlPlaneService commissionControlPlaneService)
        {
            _context = context;
            _commissionControlPlaneService = commissionControlPlaneService;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            if (User.IsInRole(UserRoles.OrganizationAdmin))
            {
                var ledgerEntries = await _context.CommissionLedgerEntries
                    .AsNoTracking()
                    .Include(e => e.SaleEvent)
                        .ThenInclude(se => se!.BusinessAccount)
                    .Include(e => e.Beneficiary)
                    .Include(e => e.PayoutEntries)
                    .OrderByDescending(e => e.EarnedAtUtc)
                    .ToListAsync();

                var legacyLedgerRows = await QueryLegacyLedgerRowsAsync();
                var adjustmentsTotal = await _context.CommissionAdjustments.SumAsync(a => a.Amount);
                var paidTotal = await _context.PayoutEntries.SumAsync(p => p.Amount);
                var detailRows = ledgerEntries.Select(ToViewModel)
                    .Concat(legacyLedgerRows.Select(ToViewModel))
                    .OrderByDescending(r => r.SaleDate)
                    .ThenByDescending(r => r.Id)
                    .ToList();

                var viewModel = new CommissionDashboardViewModel
                {
                    IsAdminView = true,
                    TotalCommissionEarned = detailRows.Sum(r => r.CommissionAmount),
                    CurrentMonthCommission = detailRows
                        .Where(r => r.SaleDate.Month == DateTime.UtcNow.Month && r.SaleDate.Year == DateTime.UtcNow.Year)
                        .Sum(r => r.CommissionAmount),
                    TotalAdjustments = adjustmentsTotal,
                    TotalPaid = paidTotal,
                    OutstandingBalance = detailRows.Sum(r => r.OutstandingAmount) + adjustmentsTotal,
                    TotalLedgerRows = detailRows.Count,
                    BusinessAccountCount = await _context.BusinessAccounts.CountAsync(),
                    ActiveAgreementCount = await _context.CommissionAgreements.CountAsync(a => a.IsActive),
                    PendingReviewRows = await _context.ImportRows.CountAsync(r => r.Status == ImportRowStatus.PendingReview || r.Status == ImportRowStatus.PendingMapping),
                    ReadyToPostRows = await _context.ImportRows.CountAsync(r => r.Status == ImportRowStatus.ReadyToPost),
                    BreakdownByDealType = detailRows
                        .GroupBy(r => r.DealType)
                        .OrderByDescending(g => g.Sum(r => r.CommissionAmount))
                        .Select(g => new CommissionDealBreakdownViewModel
                        {
                            DealType = g.Key,
                            TotalCommission = g.Sum(r => r.CommissionAmount),
                            RowCount = g.Count()
                        })
                        .ToList(),
                    DetailRows = detailRows.Take(20).ToList(),
                    RecentImportBatches = await _context.ImportBatches
                        .AsNoTracking()
                        .OrderByDescending(b => b.ReceivedAtUtc)
                        .Take(10)
                        .Select(b => new CommissionImportBatchViewModel
                        {
                            Id = b.Id,
                            SourceSystem = b.SourceSystem,
                            Status = b.Status.ToString(),
                            RowCount = b.Rows.Count,
                            ReceivedAtUtc = b.ReceivedAtUtc
                        })
                        .ToListAsync(),
                    OutstandingBeneficiaryBalances = await BuildOutstandingBalancesAsync()
                };

                return View(viewModel);
            }

            var statement = await _commissionControlPlaneService.BuildStatementAsync(userId);
            var legacyRows = (await QueryLegacyLedgerRowsAsync(userId)).Select(ToViewModel).ToList();
            var statementRows = statement.EarnedRows
                .Select(r => new CommissionLedgerRowViewModel
                {
                    Id = r.LedgerEntryId,
                    SaleEventId = r.LedgerEntryId,
                    BeneficiaryId = userId,
                    BeneficiaryName = User.Identity?.Name ?? "Me",
                    SaleDate = r.SaleDate,
                    BusinessAccountName = r.BusinessAccountName,
                    ProductName = r.ProductName,
                    GrossAmount = r.GrossAmount,
                    NetAmount = r.NetAmount,
                    CommissionAmount = r.CommissionAmount,
                    PaidAmount = r.PaidAmount,
                    OutstandingAmount = r.OutstandingAmount,
                    DealType = r.CalculationType,
                    CalculationBasis = r.CalculationType,
                    CalculationNotes = r.CalculationDetails
                })
                .Concat(legacyRows)
                .OrderByDescending(r => r.SaleDate)
                .ThenByDescending(r => r.Id)
                .ToList();

            return View(new CommissionDashboardViewModel
            {
                IsAdminView = false,
                TotalCommissionEarned = statement.TotalEarned + legacyRows.Sum(r => r.CommissionAmount),
                CurrentMonthCommission = statementRows
                    .Where(r => r.SaleDate.Month == DateTime.UtcNow.Month && r.SaleDate.Year == DateTime.UtcNow.Year)
                    .Sum(r => r.CommissionAmount),
                TotalAdjustments = statement.TotalAdjustments,
                TotalPaid = statement.TotalPaid,
                OutstandingBalance = statement.OutstandingBalance + legacyRows.Sum(r => r.OutstandingAmount),
                TotalLedgerRows = statementRows.Count,
                BreakdownByDealType = statementRows
                    .GroupBy(r => r.DealType)
                    .OrderByDescending(g => g.Sum(r => r.CommissionAmount))
                    .Select(g => new CommissionDealBreakdownViewModel
                    {
                        DealType = g.Key,
                        TotalCommission = g.Sum(r => r.CommissionAmount),
                        RowCount = g.Count()
                    })
                    .ToList(),
                DetailRows = statementRows.Take(20).ToList()
            });
        }

        public async Task<IActionResult> Details()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            if (User.IsInRole(UserRoles.OrganizationAdmin))
            {
                var rows = await _context.CommissionLedgerEntries
                    .AsNoTracking()
                    .Include(e => e.SaleEvent)
                        .ThenInclude(se => se!.BusinessAccount)
                    .Include(e => e.Beneficiary)
                    .Include(e => e.PayoutEntries)
                    .OrderByDescending(e => e.EarnedAtUtc)
                    .ToListAsync();

                var combinedRows = rows.Select(ToViewModel)
                    .Concat((await QueryLegacyLedgerRowsAsync()).Select(ToViewModel))
                    .OrderByDescending(r => r.SaleDate)
                    .ThenByDescending(r => r.Id)
                    .ToList();

                return View(combinedRows);
            }

            var statement = await _commissionControlPlaneService.BuildStatementAsync(userId);
            var detailRows = statement.EarnedRows.Select(r => new CommissionLedgerRowViewModel
                {
                    Id = r.LedgerEntryId,
                    SaleEventId = r.LedgerEntryId,
                    BeneficiaryId = userId,
                    BeneficiaryName = User.Identity?.Name ?? "Me",
                    SaleDate = r.SaleDate,
                    BusinessAccountName = r.BusinessAccountName,
                    ProductName = r.ProductName,
                    GrossAmount = r.GrossAmount,
                    NetAmount = r.NetAmount,
                    CommissionAmount = r.CommissionAmount,
                    PaidAmount = r.PaidAmount,
                    OutstandingAmount = r.OutstandingAmount,
                    DealType = r.CalculationType,
                    CalculationBasis = r.CalculationType,
                    CalculationNotes = r.CalculationDetails
                })
                .Concat((await QueryLegacyLedgerRowsAsync(userId)).Select(ToViewModel))
                .OrderByDescending(r => r.SaleDate)
                .ThenByDescending(r => r.Id)
                .ToList();

            return View(detailRows);
        }

        private async Task<List<OutstandingBeneficiaryBalanceViewModel>> BuildOutstandingBalancesAsync()
        {
            var earnings = await _context.CommissionLedgerEntries
                .AsNoTracking()
                .GroupBy(e => e.BeneficiaryId)
                .Select(g => new { BeneficiaryId = g.Key, Amount = g.Sum(x => x.CommissionAmount) })
                .ToListAsync();

            var adjustments = await _context.CommissionAdjustments
                .AsNoTracking()
                .GroupBy(a => a.BeneficiaryId)
                .Select(g => new { BeneficiaryId = g.Key, Amount = g.Sum(x => x.Amount) })
                .ToListAsync();

            var paid = await _context.PayoutEntries
                .AsNoTracking()
                .GroupBy(p => p.BeneficiaryId)
                .Select(g => new { BeneficiaryId = g.Key, Amount = g.Sum(x => x.Amount) })
                .ToListAsync();

            var users = await _context.Users
                .AsNoTracking()
                .ToDictionaryAsync(
                    u => u.Id,
                    u => string.IsNullOrWhiteSpace(u.FullName) ? (u.Email ?? u.Id) : u.FullName);

            var legacyEarnings = await _context.CommissionLedgers
                .AsNoTracking()
                .GroupBy(e => e.BeneficiaryId)
                .Select(g => new { BeneficiaryId = g.Key, Amount = g.Sum(x => x.CommissionAmount) })
                .ToListAsync();

            return earnings
                .Concat(legacyEarnings)
                .GroupBy(e => e.BeneficiaryId)
                .Select(g => new { BeneficiaryId = g.Key, Amount = g.Sum(x => x.Amount) })
                .Select(e =>
                {
                    var adjustmentAmount = adjustments.FirstOrDefault(a => a.BeneficiaryId == e.BeneficiaryId)?.Amount ?? 0m;
                    var paidAmount = paid.FirstOrDefault(p => p.BeneficiaryId == e.BeneficiaryId)?.Amount ?? 0m;
                    return new OutstandingBeneficiaryBalanceViewModel
                    {
                        BeneficiaryId = e.BeneficiaryId,
                        BeneficiaryName = users.TryGetValue(e.BeneficiaryId, out var name) ? name : e.BeneficiaryId,
                        OutstandingBalance = e.Amount + adjustmentAmount - paidAmount
                    };
                })
                .Where(x => x.OutstandingBalance != 0m)
                .OrderByDescending(x => x.OutstandingBalance)
                .Take(10)
                .ToList();
        }

        private async Task<List<CommissionLedger>> QueryLegacyLedgerRowsAsync(string? beneficiaryId = null)
        {
            var query = _context.CommissionLedgers
                .AsNoTracking()
                .Include(l => l.SaleRecord)
                    .ThenInclude(sr => sr!.Account)
                .Include(l => l.Beneficiary)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(beneficiaryId))
            {
                query = query.Where(l => l.BeneficiaryId == beneficiaryId);
            }

            return await query
                .OrderByDescending(l => l.SaleRecord!.SaleDate)
                .ThenBy(l => l.Id)
                .ToListAsync();
        }

        private static CommissionLedgerRowViewModel ToViewModel(CommissionLedgerEntry ledger)
        {
            var calculationDetails = TryFormatCalculationDetails(ledger.CalculationDetailsJson);
            var beneficiaryName = ledger.Beneficiary == null || string.IsNullOrWhiteSpace(ledger.Beneficiary.FullName)
                ? ledger.Beneficiary?.Email ?? ledger.BeneficiaryId
                : ledger.Beneficiary.FullName;
            var paidAmount = ledger.PayoutEntries.Sum(p => p.Amount);

            return new CommissionLedgerRowViewModel
            {
                Id = ledger.Id,
                SaleEventId = ledger.SaleEventId,
                BeneficiaryId = ledger.BeneficiaryId,
                BeneficiaryName = beneficiaryName,
                SaleDate = ledger.SaleEvent?.SaleDate ?? DateTime.MinValue,
                BusinessAccountName = ledger.SaleEvent?.BusinessAccount?.Name ?? "Unknown Account",
                ProductName = ledger.SaleEvent?.ProductName ?? string.Empty,
                GrossAmount = ledger.GrossAmount,
                NetAmount = ledger.NetAmount,
                CommissionAmount = ledger.CommissionAmount,
                PaidAmount = paidAmount,
                OutstandingAmount = ledger.CommissionAmount - paidAmount,
                DealType = ledger.CalculationType.ToString(),
                CalculationBasis = ledger.CalculationType.ToString(),
                CalculationNotes = calculationDetails
            };
        }

        private static CommissionLedgerRowViewModel ToViewModel(CommissionLedger ledger)
        {
            var snapshot = TryParseLegacySnapshot(ledger.DealSnapshot);
            var beneficiaryName = ledger.Beneficiary == null || string.IsNullOrWhiteSpace(ledger.Beneficiary.FullName)
                ? ledger.Beneficiary?.Email ?? ledger.BeneficiaryId
                : ledger.Beneficiary.FullName;
            var businessAccountName = ledger.SaleRecord?.Account?.FullName
                ?? ledger.SaleRecord?.Account?.Email
                ?? ledger.SaleRecord?.AccountId
                ?? "Legacy Account";

            return new CommissionLedgerRowViewModel
            {
                Id = ledger.Id,
                SaleEventId = 0,
                BeneficiaryId = ledger.BeneficiaryId,
                BeneficiaryName = beneficiaryName,
                SaleDate = ledger.SaleRecord?.SaleDate ?? DateTime.MinValue,
                BusinessAccountName = businessAccountName,
                ProductName = ledger.SaleRecord?.ProductName ?? string.Empty,
                GrossAmount = ledger.GrossAmount,
                NetAmount = ledger.NetAmount,
                CommissionAmount = ledger.CommissionAmount,
                PaidAmount = 0m,
                OutstandingAmount = ledger.CommissionAmount,
                DealType = snapshot?.DealType ?? "Legacy",
                CalculationBasis = snapshot?.CalculationBasis ?? "Legacy",
                CalculationNotes = ledger.CalculationNotes
            };
        }

        private static string TryFormatCalculationDetails(string json)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                var rate = root.TryGetProperty("rateOrAmount", out var rateValue) ? rateValue.ToString() : "0";
                return $"{root.GetProperty("calculationType").GetString()} @ {rate}";
            }
            catch
            {
                return json;
            }
        }

        private static LegacyCommissionLedgerSnapshot? TryParseLegacySnapshot(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<LegacyCommissionLedgerSnapshot>(json);
            }
            catch
            {
                return null;
            }
        }

        private sealed class LegacyCommissionLedgerSnapshot
        {
            public string DealType { get; set; } = string.Empty;
            public string CalculationBasis { get; set; } = string.Empty;
        }
    }
}
