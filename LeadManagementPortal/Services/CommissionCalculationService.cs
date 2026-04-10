using System.Text.Json;
using System.Text.Json.Serialization;
using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LeadManagementPortal.Services
{
    public class CommissionCalculationService : ICommissionCalculationService
    {
        private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
        {
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly ApplicationDbContext _context;
        private readonly ILogger<CommissionCalculationService> _logger;

        public CommissionCalculationService(ApplicationDbContext context, ILogger<CommissionCalculationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IReadOnlyList<CommissionLedger>> CalculateForSaleAsync(SaleRecord saleRecord, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(saleRecord);

            if (saleRecord.Id == 0)
            {
                throw new InvalidOperationException("SaleRecord must be persisted before commissions can be calculated.");
            }

            var existingRows = await _context.CommissionLedgers
                .Where(l => l.SaleRecordId == saleRecord.Id)
                .ToListAsync(cancellationToken);

            if (existingRows.Count > 0)
            {
                _context.CommissionLedgers.RemoveRange(existingRows);
            }

            var grossAmount = Decimal.Round(saleRecord.GrossAmount, 2, MidpointRounding.AwayFromZero);
            var hasNetAmount = saleRecord.CostAmount.HasValue;
            var netAmount = hasNetAmount
                ? Decimal.Round(saleRecord.GrossAmount - saleRecord.CostAmount!.Value, 2, MidpointRounding.AwayFromZero)
                : 0m;

            var results = new List<CommissionLedger>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string? currentUserId = saleRecord.AccountId;
            decimal? previousCommission = null;
            var depth = 0;

            while (!string.IsNullOrWhiteSpace(currentUserId))
            {
                if (!visited.Add(currentUserId))
                {
                    _logger.LogWarning(
                        "Commission chain cycle detected for sale {SaleRecordId} at user {UserId}; stopping upward walk.",
                        saleRecord.Id,
                        currentUserId);

                    if (results.Count > 0)
                    {
                        results[^1].CalculationNotes += " Chain stopped after a cycle was detected.";
                    }
                    break;
                }

                var deal = await _context.CommissionDeals
                    .AsNoTracking()
                    .SingleOrDefaultAsync(d => d.ApplicationUserId == currentUserId, cancellationToken);

                if (deal != null)
                {
                    var (commissionAmount, notes) = CalculateCommissionAmount(
                        saleRecord,
                        deal,
                        grossAmount,
                        netAmount,
                        hasNetAmount,
                        previousCommission);

                    var ledger = new CommissionLedger
                    {
                        SaleRecordId = saleRecord.Id,
                        BeneficiaryId = currentUserId,
                        GrossAmount = grossAmount,
                        NetAmount = netAmount,
                        CommissionAmount = commissionAmount,
                        ChainDepth = depth,
                        DealSnapshot = JsonSerializer.Serialize(new CommissionDealSnapshot
                        {
                            DealType = deal.DealType,
                            Rate = deal.Rate,
                            BaseCost = deal.BaseCost,
                            CalculationBasis = deal.CalculationBasis
                        }, SnapshotJsonOptions),
                        CalculationNotes = notes
                    };

                    _context.CommissionLedgers.Add(ledger);
                    results.Add(ledger);
                    previousCommission = ledger.CommissionAmount;
                }
                else
                {
                    previousCommission = null;
                }

                currentUserId = await _context.CommissionLinks
                    .AsNoTracking()
                    .Where(l => l.DownlineId == currentUserId)
                    .Select(l => l.SponsorId)
                    .SingleOrDefaultAsync(cancellationToken);

                depth++;
            }

            if (results.Count > 0)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            else if (existingRows.Count > 0)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

            return results;
        }

        private static (decimal commissionAmount, string notes) CalculateCommissionAmount(
            SaleRecord saleRecord,
            CommissionDeal deal,
            decimal grossAmount,
            decimal netAmount,
            bool hasNetAmount,
            decimal? previousCommission)
        {
            if (deal.DealType == CommissionDealType.Markup)
            {
                var markupAmount = Decimal.Round(deal.Rate * saleRecord.Quantity, 2, MidpointRounding.AwayFromZero);
                var note = $"{deal.Rate:C} markup x {saleRecord.Quantity} = {markupAmount:C}.";

                if (deal.BaseCost.HasValue && saleRecord.Quantity > 0)
                {
                    var realizedMarkup = Decimal.Round(
                        saleRecord.GrossAmount - (deal.BaseCost.Value * saleRecord.Quantity),
                        2,
                        MidpointRounding.AwayFromZero);
                    note += $" Base cost {deal.BaseCost.Value:C} per unit, realized spread {realizedMarkup:C}.";
                }

                return (markupAmount, note);
            }

            var (basisAmount, basisLabel, basisAvailableMessage) = deal.CalculationBasis switch
            {
                CommissionCalculationBasis.DownlineGross => (grossAmount, "gross", string.Empty),
                CommissionCalculationBasis.DownlineNet when hasNetAmount => (netAmount, "net", string.Empty),
                CommissionCalculationBasis.DownlineNet => (0m, "net", "Skipped because cost is missing, so net could not be calculated."),
                CommissionCalculationBasis.DownlineCommission when previousCommission.HasValue => (previousCommission.Value, "commission", string.Empty),
                CommissionCalculationBasis.DownlineCommission => (0m, "commission", "Skipped because downline commission is unavailable at this chain depth."),
                _ => (grossAmount, "gross", string.Empty)
            };

            if (!string.IsNullOrWhiteSpace(basisAvailableMessage))
            {
                return (0m, basisAvailableMessage);
            }

            var rateFactor = deal.Rate / 100m;
            var commissionAmount = Decimal.Round(basisAmount * rateFactor, 2, MidpointRounding.AwayFromZero);
            var notes = deal.DealType switch
            {
                CommissionDealType.GrossPercent => $"{deal.Rate}% of {basisLabel} {basisAmount:C} = {commissionAmount:C}.",
                CommissionDealType.NetPercent => $"{deal.Rate}% of {basisLabel} {basisAmount:C} = {commissionAmount:C}.",
                CommissionDealType.ProfitSplit => $"{deal.Rate}% profit split on {basisLabel} {basisAmount:C} = {commissionAmount:C}.",
                _ => $"{deal.Rate}% of {basisLabel} {basisAmount:C} = {commissionAmount:C}."
            };

            return (commissionAmount, notes);
        }

        private sealed class CommissionDealSnapshot
        {
            public CommissionDealType DealType { get; set; }
            public decimal Rate { get; set; }
            public decimal? BaseCost { get; set; }
            public CommissionCalculationBasis CalculationBasis { get; set; }
        }
    }
}
