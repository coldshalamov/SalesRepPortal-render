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
        private const string HierarchyAdminRoles =
            UserRoles.OrganizationAdmin + "," + UserRoles.GroupAdmin + "," + UserRoles.SalesOrgAdmin;

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
                var adjustmentsTotal = await SumCommissionAdjustmentsAsync();
                var paidTotal = await SumPayoutEntriesAsync();
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
                    SourceSystem = r.SourceSystem,
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
                    SourceSystem = r.SourceSystem,
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

        [Authorize(Roles = HierarchyAdminRoles)]
        public async Task<IActionResult> Hierarchy()
        {
            return View(await BuildHierarchyViewModelAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = HierarchyAdminRoles)]
        public async Task<IActionResult> SaveHierarchy([FromForm] SaveCommissionHierarchyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.AccountId))
            {
                return BadRequest(new
                {
                    status = "invalid",
                    message = "Select an account to update."
                });
            }

            var scope = await GetCurrentScopeAsync();
            if (string.IsNullOrWhiteSpace(scope.UserId))
            {
                return Forbid();
            }

            var userQuery = ApplyScope(
                _context.Users
                    .Include(u => u.CommissionDeal)
                    .Include(u => u.SponsorLink)
                    .Where(u => u.IsActive),
                scope);

            var account = await userQuery.FirstOrDefaultAsync(u => u.Id == request.AccountId);
            if (account == null)
            {
                return NotFound(new
                {
                    status = "missing",
                    message = "The selected account is outside your management scope."
                });
            }

            if (!string.IsNullOrWhiteSpace(request.SponsorId))
            {
                if (string.Equals(request.SponsorId, request.AccountId, StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new
                    {
                        status = "invalid",
                        message = "An account cannot own itself."
                    });
                }

                var existingSponsorId = account.SponsorLink?.SponsorId;
                var sponsor = await userQuery
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == request.SponsorId);
                var isKeepingExistingSponsor = string.Equals(
                    request.SponsorId,
                    existingSponsorId,
                    StringComparison.OrdinalIgnoreCase);

                if (sponsor == null && !isKeepingExistingSponsor)
                {
                    return BadRequest(new
                    {
                        status = "invalid",
                        message = "Choose an owner that is visible inside your current scope."
                    });
                }

                if (!isKeepingExistingSponsor && sponsor != null && await WouldCreateCommissionCycleAsync(account.Id, sponsor.Id))
                {
                    return BadRequest(new
                    {
                        status = "invalid",
                        message = "That ownership link would create a cycle in the commission tree."
                    });
                }
            }

            var validationError = ValidateHierarchyRequest(request);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                return BadRequest(new
                {
                    status = "invalid",
                    message = validationError
                });
            }

            if (request.CommissionDealType.HasValue)
            {
                if (account.CommissionDeal == null)
                {
                    account.CommissionDeal = new CommissionDeal
                    {
                        ApplicationUserId = account.Id
                    };
                    _context.CommissionDeals.Add(account.CommissionDeal);
                }

                account.CommissionDeal.DealType = request.CommissionDealType.Value;
                account.CommissionDeal.Rate = request.CommissionRate ?? 0m;
                account.CommissionDeal.BaseCost = request.CommissionBaseCost;
                account.CommissionDeal.CalculationBasis =
                    request.CommissionCalculationBasis ?? CommissionCalculationBasis.DownlineGross;
            }
            else if (account.CommissionDeal != null)
            {
                _context.CommissionDeals.Remove(account.CommissionDeal);
            }

            if (string.IsNullOrWhiteSpace(request.SponsorId))
            {
                if (account.SponsorLink != null)
                {
                    _context.CommissionLinks.Remove(account.SponsorLink);
                }
            }
            else if (account.SponsorLink == null)
            {
                _context.CommissionLinks.Add(new CommissionLink
                {
                    DownlineId = account.Id,
                    SponsorId = request.SponsorId
                });
            }
            else
            {
                account.SponsorLink.SponsorId = request.SponsorId;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                status = "ok",
                message = "Hierarchy updated.",
                hierarchy = await BuildHierarchyViewModelAsync()
            });
        }

        private async Task<CommissionHierarchyViewModel> BuildHierarchyViewModelAsync()
        {
            var scope = await GetCurrentScopeAsync();
            var users = await ApplyScope(
                    _context.Users
                        .AsNoTracking()
                        .Where(u => u.IsActive)
                        .Include(u => u.CommissionDeal)
                        .Include(u => u.SponsorLink),
                    scope)
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .ThenBy(u => u.Email)
                .ToListAsync();

            var visibleIds = users
                .Select(u => u.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var roleLookup = await BuildRoleLookupAsync(users.Select(u => u.Id).ToList());
            var usersById = users.ToDictionary(u => u.Id, StringComparer.OrdinalIgnoreCase);
            var downlineCounts = users
                .Where(u => !string.IsNullOrWhiteSpace(u.SponsorLink?.SponsorId) && visibleIds.Contains(u.SponsorLink!.SponsorId))
                .GroupBy(u => u.SponsorLink!.SponsorId!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

            var nodes = users.Select(user =>
            {
                var sponsorId = user.SponsorLink?.SponsorId;
                var visibleSponsor = !string.IsNullOrWhiteSpace(sponsorId) && usersById.TryGetValue(sponsorId, out var sponsor)
                    ? sponsor
                    : null;
                var fullName = string.IsNullOrWhiteSpace(user.FullName) ? user.Email ?? user.Id : user.FullName;

                return new CommissionHierarchyNodeViewModel
                {
                    Id = user.Id,
                    FullName = fullName,
                    Email = user.Email ?? string.Empty,
                    Role = roleLookup.TryGetValue(user.Id, out var roleName) ? roleName : "Unassigned",
                    SponsorId = sponsorId,
                    SponsorName = visibleSponsor == null
                        ? (string.IsNullOrWhiteSpace(sponsorId) ? null : "Owner outside current scope")
                        : (string.IsNullOrWhiteSpace(visibleSponsor.FullName) ? visibleSponsor.Email ?? visibleSponsor.Id : visibleSponsor.FullName),
                    DealType = user.CommissionDeal?.DealType.ToString(),
                    CalculationBasis = user.CommissionDeal?.CalculationBasis.ToString(),
                    Rate = user.CommissionDeal?.Rate,
                    BaseCost = user.CommissionDeal?.BaseCost,
                    IsOrphan = string.IsNullOrWhiteSpace(sponsorId),
                    DownlineCount = downlineCounts.TryGetValue(user.Id, out var count) ? count : 0
                };
            })
            .OrderByDescending(node => node.IsOrphan)
            .ThenBy(node => node.FullName)
            .ToList();

            return new CommissionHierarchyViewModel
            {
                Nodes = nodes,
                TotalAccounts = nodes.Count,
                RootAccounts = nodes.Count(node => node.IsOrphan),
                LinkedAccounts = nodes.Count(node => !node.IsOrphan),
                ConfiguredDeals = nodes.Count(node => !string.IsNullOrWhiteSpace(node.DealType))
            };
        }

        private async Task<CurrentScope> GetCurrentScopeAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new CurrentScope();
            }

            if (User.IsInRole(UserRoles.OrganizationAdmin))
            {
                return new CurrentScope { UserId = userId };
            }

            if (User.IsInRole(UserRoles.GroupAdmin))
            {
                var salesGroupId = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => u.SalesGroupId)
                    .SingleOrDefaultAsync();

                return new CurrentScope
                {
                    UserId = userId,
                    SalesGroupId = salesGroupId
                };
            }

            if (User.IsInRole(UserRoles.SalesOrgAdmin))
            {
                var salesOrgId = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => u.SalesOrgId)
                    .SingleOrDefaultAsync();

                return new CurrentScope
                {
                    UserId = userId,
                    SalesOrgId = salesOrgId
                };
            }

            return new CurrentScope
            {
                UserId = userId
            };
        }

        private IQueryable<ApplicationUser> ApplyScope(IQueryable<ApplicationUser> query, CurrentScope scope)
        {
            if (string.IsNullOrWhiteSpace(scope.UserId))
            {
                return query.Where(_ => false);
            }

            if (User.IsInRole(UserRoles.OrganizationAdmin))
            {
                return query;
            }

            if (User.IsInRole(UserRoles.GroupAdmin))
            {
                return string.IsNullOrWhiteSpace(scope.SalesGroupId)
                    ? query.Where(_ => false)
                    : query.Where(u => u.SalesGroupId == scope.SalesGroupId);
            }

            if (User.IsInRole(UserRoles.SalesOrgAdmin))
            {
                return scope.SalesOrgId.HasValue
                    ? query.Where(u => u.SalesOrgId == scope.SalesOrgId.Value)
                    : query.Where(_ => false);
            }

            return query.Where(u => u.Id == scope.UserId);
        }

        private async Task<Dictionary<string, string>> BuildRoleLookupAsync(List<string> userIds)
        {
            if (userIds.Count == 0)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var orderedRoles = new[]
            {
                UserRoles.OrganizationAdmin,
                UserRoles.GroupAdmin,
                UserRoles.SalesOrgAdmin,
                UserRoles.Affiliate,
                UserRoles.SalesRep
            };

            var roleRows = await (
                    from userRole in _context.UserRoles
                    join role in _context.Roles on userRole.RoleId equals role.Id
                    where userIds.Contains(userRole.UserId)
                    select new
                    {
                        userRole.UserId,
                        RoleName = role.Name ?? string.Empty
                    })
                .ToListAsync();

            return roleRows
                .GroupBy(row => row.UserId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(row => row.RoleName)
                        .OrderBy(roleName =>
                        {
                            var index = Array.IndexOf(orderedRoles, roleName);
                            return index >= 0 ? index : int.MaxValue;
                        })
                        .ThenBy(roleName => roleName)
                        .FirstOrDefault() ?? "Unassigned",
                    StringComparer.OrdinalIgnoreCase);
        }

        private static string? ValidateHierarchyRequest(SaveCommissionHierarchyRequest request)
        {
            var hasCommissionInput = request.CommissionDealType.HasValue
                || request.CommissionCalculationBasis.HasValue
                || request.CommissionRate.HasValue
                || request.CommissionBaseCost.HasValue;

            if (!hasCommissionInput)
            {
                return null;
            }

            if (!request.CommissionDealType.HasValue)
            {
                return "Select a deal type or clear the commission fields.";
            }

            if (!request.CommissionRate.HasValue)
            {
                return "Enter the commission percentage or markup amount.";
            }

            if (!request.CommissionCalculationBasis.HasValue)
            {
                return "Select what the commission is calculated from.";
            }

            return null;
        }

        private async Task<bool> WouldCreateCommissionCycleAsync(string downlineId, string sponsorId)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { downlineId };
            var currentSponsorId = sponsorId;

            while (!string.IsNullOrWhiteSpace(currentSponsorId))
            {
                if (!visited.Add(currentSponsorId))
                {
                    return true;
                }

                currentSponsorId = await _context.CommissionLinks
                    .AsNoTracking()
                    .Where(link => link.DownlineId == currentSponsorId)
                    .Select(link => link.SponsorId)
                    .SingleOrDefaultAsync() ?? string.Empty;
            }

            return false;
        }

        private async Task<List<OutstandingBeneficiaryBalanceViewModel>> BuildOutstandingBalancesAsync()
        {
            var earnings = await LoadAmountsByBeneficiaryAsync(
                _context.CommissionLedgerEntries.AsNoTracking().Select(e => new BeneficiaryAmount(e.BeneficiaryId, e.CommissionAmount)));

            var adjustments = await LoadAmountsByBeneficiaryAsync(
                _context.CommissionAdjustments.AsNoTracking().Select(a => new BeneficiaryAmount(a.BeneficiaryId, a.Amount)));

            var paid = await LoadAmountsByBeneficiaryAsync(
                _context.PayoutEntries.AsNoTracking().Select(p => new BeneficiaryAmount(p.BeneficiaryId, p.Amount)));

            var users = await _context.Users
                .AsNoTracking()
                .ToDictionaryAsync(
                    u => u.Id,
                    u => string.IsNullOrWhiteSpace(u.FullName) ? (u.Email ?? u.Id) : u.FullName);

            var legacyEarnings = await LoadAmountsByBeneficiaryAsync(
                _context.CommissionLedgers.AsNoTracking().Select(e => new BeneficiaryAmount(e.BeneficiaryId, e.CommissionAmount)));

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

        private async Task<decimal> SumCommissionAdjustmentsAsync()
        {
            var amounts = await _context.CommissionAdjustments
                .AsNoTracking()
                .Select(a => a.Amount)
                .ToListAsync();

            return amounts.Sum();
        }

        private async Task<decimal> SumPayoutEntriesAsync()
        {
            var amounts = await _context.PayoutEntries
                .AsNoTracking()
                .Select(p => p.Amount)
                .ToListAsync();

            return amounts.Sum();
        }

        private static List<BeneficiaryAmount> AggregateAmountsByBeneficiary(IEnumerable<BeneficiaryAmount> rows) =>
            rows.GroupBy(row => row.BeneficiaryId)
                .Select(group => new BeneficiaryAmount(group.Key, group.Sum(row => row.Amount)))
                .ToList();

        private async Task<List<BeneficiaryAmount>> LoadAmountsByBeneficiaryAsync(IQueryable<BeneficiaryAmount> query)
        {
            var rows = await query.ToListAsync();
            return AggregateAmountsByBeneficiary(rows);
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
                SourceSystem = ledger.SaleEvent?.SourceSystem ?? "unknown",
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

        private sealed record BeneficiaryAmount(string BeneficiaryId, decimal Amount);

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
                SourceSystem = "legacy",
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

        private sealed class CurrentScope
        {
            public string? UserId { get; set; }
            public string? SalesGroupId { get; set; }
            public int? SalesOrgId { get; set; }
        }
    }
}
