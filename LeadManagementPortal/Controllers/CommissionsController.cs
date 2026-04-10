using System.Security.Claims;
using System.Text.Json;
using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LeadManagementPortal.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace LeadManagementPortal.Controllers
{
    [Authorize]
    public class CommissionsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CommissionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var rows = await (await QueryVisibleLedgerRowsAsync())
                .OrderByDescending(l => l.SaleRecord!.SaleDate)
                .ThenBy(l => l.ChainDepth)
                .ToListAsync();

            var detailRows = rows
                .Select(ToViewModel)
                .ToList();

            var viewModel = new CommissionDashboardViewModel
            {
                TotalCommissionEarned = detailRows.Sum(r => r.CommissionAmount),
                CurrentMonthCommission = detailRows
                    .Where(r => r.SaleDate.Month == DateTime.UtcNow.Month && r.SaleDate.Year == DateTime.UtcNow.Year)
                    .Sum(r => r.CommissionAmount),
                TotalLedgerRows = detailRows.Count,
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
                DetailRows = detailRows.Take(20).ToList()
            };

            return View(viewModel);
        }

        public async Task<IActionResult> Details()
        {
            var rows = await (await QueryVisibleLedgerRowsAsync())
                .OrderByDescending(l => l.SaleRecord!.SaleDate)
                .ThenBy(l => l.ChainDepth)
                .ToListAsync();

            return View(rows.Select(ToViewModel).ToList());
        }

        private async Task<IQueryable<CommissionLedger>> QueryVisibleLedgerRowsAsync()
        {
            var query = _context.CommissionLedgers
                .AsNoTracking()
                .Include(l => l.SaleRecord)
                .Include(l => l.Beneficiary)
                .AsQueryable();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return query.Where(_ => false);
            }

            if (User.IsInRole(UserRoles.OrganizationAdmin))
            {
                return query;
            }

            if (User.IsInRole(UserRoles.GroupAdmin))
            {
                var salesGroupId = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => u.SalesGroupId)
                    .SingleOrDefaultAsync();

                return string.IsNullOrWhiteSpace(salesGroupId)
                    ? query.Where(_ => false)
                    : query.Where(l => l.Beneficiary != null && l.Beneficiary.SalesGroupId == salesGroupId);
            }

            if (User.IsInRole(UserRoles.SalesOrgAdmin))
            {
                var salesOrgId = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => u.SalesOrgId)
                    .SingleOrDefaultAsync();

                return salesOrgId.HasValue
                    ? query.Where(l => l.Beneficiary != null && l.Beneficiary.SalesOrgId == salesOrgId.Value)
                    : query.Where(_ => false);
            }

            return query.Where(l => l.BeneficiaryId == userId);
        }

        private static CommissionLedgerRowViewModel ToViewModel(CommissionLedger ledger)
        {
            var snapshot = TryParseSnapshot(ledger.DealSnapshot);
            var beneficiaryName = ledger.Beneficiary == null || string.IsNullOrWhiteSpace(ledger.Beneficiary.FullName)
                ? ledger.Beneficiary?.Email ?? ledger.BeneficiaryId
                : ledger.Beneficiary.FullName;

            return new CommissionLedgerRowViewModel
            {
                Id = ledger.Id,
                SaleRecordId = ledger.SaleRecordId,
                BeneficiaryId = ledger.BeneficiaryId,
                BeneficiaryName = beneficiaryName,
                SaleDate = ledger.SaleRecord?.SaleDate ?? DateTime.MinValue,
                ProductName = ledger.SaleRecord?.ProductName ?? string.Empty,
                GrossAmount = ledger.GrossAmount,
                NetAmount = ledger.NetAmount,
                CommissionAmount = ledger.CommissionAmount,
                ChainDepth = ledger.ChainDepth,
                DealType = snapshot?.DealType ?? "Unknown",
                CalculationBasis = snapshot?.CalculationBasis ?? "Unknown",
                CalculationNotes = ledger.CalculationNotes
            };
        }

        private static CommissionLedgerSnapshot? TryParseSnapshot(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<CommissionLedgerSnapshot>(json);
            }
            catch
            {
                return null;
            }
        }

        private sealed class CommissionLedgerSnapshot
        {
            public string DealType { get; set; } = string.Empty;
            public string CalculationBasis { get; set; } = string.Empty;
        }
    }
}
