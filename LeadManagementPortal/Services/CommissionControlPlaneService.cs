using System.Globalization;
using System.Text.Json;
using CsvHelper;
using LeadManagementPortal.Controllers;
using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LeadManagementPortal.Services
{
    public sealed class CommissionStatementSummary
    {
        public decimal TotalEarned { get; set; }
        public decimal TotalAdjustments { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal OutstandingBalance { get; set; }
        public List<CommissionStatementRow> EarnedRows { get; set; } = new();
        public List<CommissionAdjustment> Adjustments { get; set; } = new();
        public List<PayoutEntry> PayoutEntries { get; set; } = new();
    }

    public sealed class CommissionStatementRow
    {
        public int LedgerEntryId { get; set; }
        public DateTime SaleDate { get; set; }
        public string BusinessAccountName { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public decimal GrossAmount { get; set; }
        public decimal NetAmount { get; set; }
        public decimal CommissionAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal OutstandingAmount { get; set; }
        public string CalculationType { get; set; } = string.Empty;
        public string CalculationDetails { get; set; } = string.Empty;
    }

    public sealed class OutstandingPayoutItem
    {
        public string ItemType { get; set; } = string.Empty;
        public int SourceId { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal OutstandingAmount { get; set; }
    }

    public sealed class PayoutSelectionRequest
    {
        public string BeneficiaryId { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public int SourceId { get; set; }
        public decimal Amount { get; set; }
    }

    public class CommissionControlPlaneService : ICommissionControlPlaneService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = false
        };

        private readonly ApplicationDbContext _context;
        private readonly ILogger<CommissionControlPlaneService> _logger;

        public CommissionControlPlaneService(ApplicationDbContext context, ILogger<CommissionControlPlaneService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ImportBatch> CreateBatchFromLegacySalesAsync(
            IEnumerable<SalesIngestRecordRequest> records,
            string sourceSystem,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var batch = new ImportBatch
            {
                SourceSystem = sourceSystem,
                ReceivedAtUtc = now,
                Status = ImportBatchStatus.PendingReview
            };

            var rows = records.Select((record, index) =>
            {
                var rawPayload = new Dictionary<string, object?>
                {
                    ["accountId"] = record.AccountId,
                    ["productName"] = record.ProductName,
                    ["quantity"] = record.Quantity,
                    ["grossAmount"] = record.GrossAmount,
                    ["costAmount"] = record.CostAmount,
                    ["saleDate"] = record.SaleDate
                };

                if (record.AdditionalData != null)
                {
                    foreach (var extra in record.AdditionalData)
                    {
                        rawPayload[extra.Key] = JsonSerializer.Deserialize<object?>(extra.Value.GetRawText(), JsonOptions);
                    }
                }

                var mappedPayload = new Dictionary<string, object?>
                {
                    ["BusinessAccountExternalKey"] = record.AccountId.Trim(),
                    ["ProductName"] = record.ProductName.Trim(),
                    ["Quantity"] = record.Quantity,
                    ["GrossAmount"] = record.GrossAmount,
                    ["CostAmount"] = record.CostAmount,
                    ["SaleDate"] = record.SaleDate
                };

                return new ImportRow
                {
                    RowNumber = index + 1,
                    Status = ImportRowStatus.PendingReview,
                    BusinessAccountExternalKey = record.AccountId.Trim(),
                    ProductName = record.ProductName.Trim(),
                    Quantity = record.Quantity,
                    GrossAmount = record.GrossAmount,
                    CostAmount = record.CostAmount,
                    SaleDate = record.SaleDate,
                    RawPayloadJson = JsonSerializer.Serialize(rawPayload, JsonOptions),
                    MappedPayloadJson = JsonSerializer.Serialize(mappedPayload, JsonOptions)
                };
            }).ToList();

            batch.Rows = rows;
            _context.ImportBatches.Add(batch);
            await _context.SaveChangesAsync(cancellationToken);
            return batch;
        }

        public async Task<ImportBatch> CreateBatchFromRawRowsAsync(
            string sourceSystem,
            IEnumerable<IDictionary<string, string?>> rows,
            int? importProfileId,
            string? uploadedById,
            string? sourceFileName,
            CancellationToken cancellationToken = default)
        {
            var batch = new ImportBatch
            {
                SourceSystem = sourceSystem,
                ImportProfileId = importProfileId,
                UploadedById = uploadedById,
                SourceFileName = sourceFileName,
                ReceivedAtUtc = DateTime.UtcNow,
                Status = importProfileId.HasValue ? ImportBatchStatus.PendingReview : ImportBatchStatus.PendingReview
            };

            batch.Rows = rows.Select((row, index) => new ImportRow
            {
                RowNumber = index + 1,
                Status = ImportRowStatus.PendingMapping,
                RawPayloadJson = JsonSerializer.Serialize(row, JsonOptions),
                MappedPayloadJson = "{}"
            }).ToList();

            _context.ImportBatches.Add(batch);
            await _context.SaveChangesAsync(cancellationToken);

            if (importProfileId.HasValue)
            {
                await EvaluateBatchAsync(batch.Id, cancellationToken);
            }

            return batch;
        }

        public async Task EvaluateBatchAsync(int batchId, CancellationToken cancellationToken = default)
        {
            var batch = await _context.ImportBatches
                .Include(b => b.ImportProfile)
                .Include(b => b.Rows)
                .SingleAsync(b => b.Id == batchId, cancellationToken);

            Dictionary<string, string>? mappings = null;
            if (batch.ImportProfile != null)
            {
                mappings = DeserializeMappings(batch.ImportProfile.ColumnMappingsJson);
            }

            foreach (var row in batch.Rows.Where(r => r.Status != ImportRowStatus.Posted && r.Status != ImportRowStatus.Rejected))
            {
                EvaluateRowInternal(row, mappings);
            }

            batch.Status = CalculateBatchStatus(batch.Rows);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task EvaluateRowAsync(int rowId, CancellationToken cancellationToken = default)
        {
            var row = await _context.ImportRows
                .Include(r => r.ImportBatch)
                    .ThenInclude(b => b!.ImportProfile)
                .SingleAsync(r => r.Id == rowId, cancellationToken);

            Dictionary<string, string>? mappings = null;
            if (row.ImportBatch?.ImportProfile != null)
            {
                mappings = DeserializeMappings(row.ImportBatch.ImportProfile.ColumnMappingsJson);
            }

            EvaluateRowInternal(row, mappings);

            if (row.ImportBatch != null)
            {
                row.ImportBatch.Status = await CalculateBatchStatusAsync(row.ImportBatch.Id, cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task PostReadyRowsAsync(int batchId, string postedById, CancellationToken cancellationToken = default)
        {
            var batch = await _context.ImportBatches
                .Include(b => b.Rows)
                .SingleAsync(b => b.Id == batchId, cancellationToken);

            var agreementIds = batch.Rows
                .Where(r => r.Status == ImportRowStatus.ReadyToPost && r.SelectedAgreementId.HasValue)
                .Select(r => r.SelectedAgreementId!.Value)
                .Distinct()
                .ToList();

            var agreements = await _context.CommissionAgreements
                .Include(a => a.BusinessAccount)
                .Include(a => a.Recipients)
                .ToDictionaryAsync(a => a.Id, cancellationToken);

            foreach (var row in batch.Rows.Where(r => r.Status == ImportRowStatus.ReadyToPost).OrderBy(r => r.RowNumber))
            {
                if (!row.SelectedAgreementId.HasValue || !row.BusinessAccountId.HasValue || !row.GrossAmount.HasValue || !row.SaleDate.HasValue || string.IsNullOrWhiteSpace(row.ProductName))
                {
                    row.Status = ImportRowStatus.PendingReview;
                    row.ReviewNotes = "Row is missing required mapped fields for posting.";
                    continue;
                }

                if (!agreements.TryGetValue(row.SelectedAgreementId.Value, out var agreement))
                {
                    row.Status = ImportRowStatus.PendingReview;
                    row.ReviewNotes = "Selected agreement no longer exists.";
                    continue;
                }

                if (agreement.Recipients.Any(r => r.CalculationType == CommissionRecipientCalculationType.PercentOfNet) && !row.CostAmount.HasValue)
                {
                    row.Status = ImportRowStatus.PendingReview;
                    row.ReviewNotes = "Cost amount is required before posting net-based commission rows.";
                    continue;
                }

                var saleEvent = new SaleEvent
                {
                    BusinessAccountId = row.BusinessAccountId.Value,
                    ExternalRowId = row.ExternalRowId,
                    SaleDate = row.SaleDate.Value,
                    ProductName = row.ProductName!,
                    Quantity = row.Quantity ?? 1,
                    GrossAmount = Decimal.Round(row.GrossAmount.Value, 2, MidpointRounding.AwayFromZero),
                    CostAmount = row.CostAmount.HasValue
                        ? Decimal.Round(row.CostAmount.Value, 2, MidpointRounding.AwayFromZero)
                        : null,
                    CreditedRepId = row.CreditedRepId,
                    SourceSystem = batch.SourceSystem,
                    RawPayloadJson = row.RawPayloadJson,
                    PostedById = postedById,
                    PostedAtUtc = DateTime.UtcNow
                };

                _context.SaleEvents.Add(saleEvent);
                await _context.SaveChangesAsync(cancellationToken);

                var computedAmounts = new Dictionary<int, decimal>();
                var netAmount = saleEvent.CostAmount.HasValue
                    ? Decimal.Round(saleEvent.GrossAmount - saleEvent.CostAmount.Value, 2, MidpointRounding.AwayFromZero)
                    : 0m;

                foreach (var recipient in agreement.Recipients.OrderBy(r => r.SortOrder).ThenBy(r => r.Id))
                {
                    var commissionAmount = CalculateRecipientAmount(recipient, saleEvent.GrossAmount, netAmount, saleEvent.Quantity, computedAmounts);
                    var details = BuildCalculationDetails(recipient, saleEvent, commissionAmount, netAmount);

                    var ledgerEntry = new CommissionLedgerEntry
                    {
                        SaleEventId = saleEvent.Id,
                        CommissionAgreementId = agreement.Id,
                        CommissionAgreementRecipientId = recipient.Id,
                        BeneficiaryId = recipient.BeneficiaryId,
                        GrossAmount = saleEvent.GrossAmount,
                        NetAmount = netAmount,
                        CommissionAmount = commissionAmount,
                        CalculationType = recipient.CalculationType,
                        CalculationDetailsJson = JsonSerializer.Serialize(details, JsonOptions),
                        EarnedAtUtc = DateTime.UtcNow
                    };

                    _context.CommissionLedgerEntries.Add(ledgerEntry);
                    computedAmounts[recipient.Id] = commissionAmount;
                }

                row.SaleEventId = saleEvent.Id;
                row.Status = ImportRowStatus.Posted;
                row.ReviewNotes = null;
            }

            batch.Status = CalculateBatchStatus(batch.Rows);

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<CommissionStatementSummary> BuildStatementAsync(string beneficiaryId, CancellationToken cancellationToken = default)
        {
            var ledgerEntries = await _context.CommissionLedgerEntries
                .AsNoTracking()
                .Include(e => e.SaleEvent)
                    .ThenInclude(se => se!.BusinessAccount)
                .Where(e => e.BeneficiaryId == beneficiaryId)
                .OrderByDescending(e => e.EarnedAtUtc)
                .ToListAsync(cancellationToken);

            var adjustments = await _context.CommissionAdjustments
                .AsNoTracking()
                .Where(a => a.BeneficiaryId == beneficiaryId)
                .OrderByDescending(a => a.CreatedAtUtc)
                .ToListAsync(cancellationToken);

            var payoutEntries = await _context.PayoutEntries
                .AsNoTracking()
                .Where(p => p.BeneficiaryId == beneficiaryId)
                .OrderByDescending(p => p.Id)
                .ToListAsync(cancellationToken);

            var paidByLedger = payoutEntries
                .Where(p => p.CommissionLedgerEntryId.HasValue)
                .GroupBy(p => p.CommissionLedgerEntryId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

            var summary = new CommissionStatementSummary
            {
                TotalEarned = ledgerEntries.Sum(e => e.CommissionAmount),
                TotalAdjustments = adjustments.Sum(a => a.Amount),
                TotalPaid = payoutEntries.Sum(p => p.Amount),
                Adjustments = adjustments,
                PayoutEntries = payoutEntries
            };

            summary.EarnedRows = ledgerEntries.Select(entry =>
            {
                var paidAmount = paidByLedger.TryGetValue(entry.Id, out var amountPaid) ? amountPaid : 0m;
                return new CommissionStatementRow
                {
                    LedgerEntryId = entry.Id,
                    SaleDate = entry.SaleEvent?.SaleDate ?? DateTime.MinValue,
                    BusinessAccountName = entry.SaleEvent?.BusinessAccount?.Name ?? "Unknown Account",
                    ProductName = entry.SaleEvent?.ProductName ?? string.Empty,
                    GrossAmount = entry.GrossAmount,
                    NetAmount = entry.NetAmount,
                    CommissionAmount = entry.CommissionAmount,
                    PaidAmount = paidAmount,
                    OutstandingAmount = entry.CommissionAmount - paidAmount,
                    CalculationType = entry.CalculationType.ToString(),
                    CalculationDetails = entry.CalculationDetailsJson
                };
            }).ToList();

            summary.OutstandingBalance = summary.TotalEarned + summary.TotalAdjustments - summary.TotalPaid;
            return summary;
        }

        public async Task<IReadOnlyList<OutstandingPayoutItem>> GetOutstandingItemsAsync(string beneficiaryId, CancellationToken cancellationToken = default)
        {
            var ledgerEntries = await _context.CommissionLedgerEntries
                .AsNoTracking()
                .Include(e => e.SaleEvent)
                    .ThenInclude(se => se!.BusinessAccount)
                .Where(e => e.BeneficiaryId == beneficiaryId)
                .OrderBy(e => e.EarnedAtUtc)
                .ToListAsync(cancellationToken);

            var payoutEntries = await _context.PayoutEntries
                .AsNoTracking()
                .Where(p => p.BeneficiaryId == beneficiaryId)
                .ToListAsync(cancellationToken);

            var paidLedger = payoutEntries
                .Where(p => p.CommissionLedgerEntryId.HasValue)
                .GroupBy(p => p.CommissionLedgerEntryId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

            var paidAdjustments = payoutEntries
                .Where(p => p.CommissionAdjustmentId.HasValue)
                .GroupBy(p => p.CommissionAdjustmentId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

            var items = ledgerEntries
                .Select(entry =>
                {
                    var paid = paidLedger.TryGetValue(entry.Id, out var ledgerPaid) ? ledgerPaid : 0m;
                    var outstanding = entry.CommissionAmount - paid;
                    return new OutstandingPayoutItem
                    {
                        ItemType = "ledger",
                        SourceId = entry.Id,
                        Description = $"{entry.SaleEvent?.BusinessAccount?.Name ?? "Account"} - {entry.SaleEvent?.ProductName ?? "Sale"} ({entry.SaleEvent?.SaleDate:yyyy-MM-dd})",
                        OutstandingAmount = outstanding
                    };
                })
                .Where(item => item.OutstandingAmount > 0m)
                .ToList();

            var adjustments = await _context.CommissionAdjustments
                .AsNoTracking()
                .Where(a => a.BeneficiaryId == beneficiaryId)
                .OrderBy(a => a.CreatedAtUtc)
                .ToListAsync(cancellationToken);

            items.AddRange(adjustments
                .Where(a => a.Amount > 0m)
                .Select(adjustment =>
                {
                    var paid = paidAdjustments.TryGetValue(adjustment.Id, out var adjustmentPaid) ? adjustmentPaid : 0m;
                    return new OutstandingPayoutItem
                    {
                        ItemType = "adjustment",
                        SourceId = adjustment.Id,
                        Description = $"Adjustment - {adjustment.Reason}",
                        OutstandingAmount = adjustment.Amount - paid
                    };
                })
                .Where(item => item.OutstandingAmount > 0m));

            return items.OrderBy(i => i.Description).ToList();
        }

        public async Task<PayoutBatch> CreatePayoutBatchAsync(
            string createdById,
            string reference,
            string? notes,
            IEnumerable<PayoutSelectionRequest> selections,
            CancellationToken cancellationToken = default)
        {
            var normalizedSelections = selections
                .Where(s => s.Amount > 0m)
                .Select(s => new PayoutSelectionRequest
                {
                    BeneficiaryId = s.BeneficiaryId.Trim(),
                    ItemType = s.ItemType.Trim().ToLowerInvariant(),
                    SourceId = s.SourceId,
                    Amount = Decimal.Round(s.Amount, 2, MidpointRounding.AwayFromZero)
                })
                .ToList();

            if (normalizedSelections.Count == 0)
            {
                throw new InvalidOperationException("Select at least one payout item.");
            }

            var outstandingByBeneficiary = new Dictionary<string, Dictionary<(string ItemType, int SourceId), decimal>>(StringComparer.OrdinalIgnoreCase);
            foreach (var beneficiaryId in normalizedSelections.Select(s => s.BeneficiaryId).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var outstandingItems = await GetOutstandingItemsAsync(beneficiaryId, cancellationToken);
                outstandingByBeneficiary[beneficiaryId] = outstandingItems.ToDictionary(
                    item => (item.ItemType.ToLowerInvariant(), item.SourceId),
                    item => item.OutstandingAmount);
            }

            var aggregatedSelections = normalizedSelections
                .GroupBy(s => new { s.BeneficiaryId, s.ItemType, s.SourceId })
                .Select(g => new PayoutSelectionRequest
                {
                    BeneficiaryId = g.Key.BeneficiaryId,
                    ItemType = g.Key.ItemType,
                    SourceId = g.Key.SourceId,
                    Amount = g.Sum(x => x.Amount)
                })
                .ToList();

            foreach (var selection in aggregatedSelections)
            {
                if (selection.ItemType != "ledger" && selection.ItemType != "adjustment")
                {
                    throw new InvalidOperationException("Unsupported payout item type.");
                }

                if (!outstandingByBeneficiary.TryGetValue(selection.BeneficiaryId, out var outstandingItems)
                    || !outstandingItems.TryGetValue((selection.ItemType, selection.SourceId), out var outstandingAmount))
                {
                    throw new InvalidOperationException("One or more payout items are no longer available.");
                }

                if (selection.Amount > outstandingAmount)
                {
                    throw new InvalidOperationException("One or more payout amounts exceed the remaining outstanding balance.");
                }
            }

            var batch = new PayoutBatch
            {
                Reference = reference,
                Notes = notes,
                CreatedById = createdById,
                CreatedAtUtc = DateTime.UtcNow,
                PaidAtUtc = DateTime.UtcNow
            };

            foreach (var selection in aggregatedSelections)
            {
                var entry = new PayoutEntry
                {
                    BeneficiaryId = selection.BeneficiaryId,
                    Amount = selection.Amount
                };

                if (string.Equals(selection.ItemType, "ledger", StringComparison.OrdinalIgnoreCase))
                {
                    entry.CommissionLedgerEntryId = selection.SourceId;
                }
                else
                {
                    entry.CommissionAdjustmentId = selection.SourceId;
                }

                batch.Entries.Add(entry);
            }

            _context.PayoutBatches.Add(batch);
            await _context.SaveChangesAsync(cancellationToken);
            return batch;
        }

        private void EvaluateRowInternal(ImportRow row, IReadOnlyDictionary<string, string>? mappings)
        {
            if (mappings != null && row.Status == ImportRowStatus.PendingMapping)
            {
                ApplyMappings(row, mappings);
            }

            MatchBusinessAccount(row);
            MatchAgreement(row);
        }

        private static ImportBatchStatus CalculateBatchStatus(IEnumerable<ImportRow> rows)
        {
            var materializedRows = rows.ToList();
            return materializedRows.All(r => r.Status == ImportRowStatus.Posted)
                ? ImportBatchStatus.Posted
                : materializedRows.Any(r => r.Status == ImportRowStatus.ReadyToPost)
                    ? ImportBatchStatus.ReadyToPost
                    : ImportBatchStatus.PendingReview;
        }

        private async Task<ImportBatchStatus> CalculateBatchStatusAsync(int batchId, CancellationToken cancellationToken)
        {
            var statuses = await _context.ImportRows
                .AsNoTracking()
                .Where(r => r.ImportBatchId == batchId)
                .Select(r => r.Status)
                .ToListAsync(cancellationToken);

            return CalculateBatchStatus(statuses.Select(status => new ImportRow { Status = status }));
        }

        private static Dictionary<string, string> DeserializeMappings(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
                    ?.Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private void ApplyMappings(ImportRow row, IReadOnlyDictionary<string, string> mappings)
        {
            Dictionary<string, string?> rawValues;
            try
            {
                rawValues = JsonSerializer.Deserialize<Dictionary<string, string?>>(row.RawPayloadJson, JsonOptions)
                    ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                rawValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            }

            row.ExternalRowId = GetMappedValue(rawValues, mappings, "ExternalRowId");
            row.BusinessAccountExternalKey = GetMappedValue(rawValues, mappings, "BusinessAccountExternalKey");
            row.BusinessAccountName = GetMappedValue(rawValues, mappings, "BusinessAccountName");
            row.ProductName = GetMappedValue(rawValues, mappings, "ProductName");
            row.CreditedRepId = GetMappedValue(rawValues, mappings, "CreditedRepId");

            if (Int32.TryParse(GetMappedValue(rawValues, mappings, "Quantity"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity))
            {
                row.Quantity = quantity;
            }

            if (Decimal.TryParse(GetMappedValue(rawValues, mappings, "GrossAmount"), NumberStyles.Number, CultureInfo.InvariantCulture, out var grossAmount))
            {
                row.GrossAmount = grossAmount;
            }

            if (Decimal.TryParse(GetMappedValue(rawValues, mappings, "CostAmount"), NumberStyles.Number, CultureInfo.InvariantCulture, out var costAmount))
            {
                row.CostAmount = costAmount;
            }

            if (DateTime.TryParse(GetMappedValue(rawValues, mappings, "SaleDate"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var saleDate))
            {
                row.SaleDate = saleDate;
            }

            row.MappedPayloadJson = JsonSerializer.Serialize(new
            {
                row.ExternalRowId,
                row.BusinessAccountExternalKey,
                row.BusinessAccountName,
                row.ProductName,
                row.Quantity,
                row.GrossAmount,
                row.CostAmount,
                row.SaleDate,
                row.CreditedRepId
            }, JsonOptions);
        }

        private void MatchBusinessAccount(ImportRow row)
        {
            if (row.BusinessAccountId.HasValue)
            {
                var selectedAccount = _context.BusinessAccounts
                    .AsNoTracking()
                    .SingleOrDefault(a => a.Id == row.BusinessAccountId.Value && a.IsActive);

                if (selectedAccount != null)
                {
                    row.BusinessAccountName ??= selectedAccount.Name;
                    row.BusinessAccountExternalKey ??= selectedAccount.ExternalKey;
                    row.ReviewNotes = null;
                    return;
                }
            }

            row.BusinessAccountId = null;

            if (!String.IsNullOrWhiteSpace(row.BusinessAccountExternalKey))
            {
                var matches = _context.BusinessAccounts
                    .Where(a => a.IsActive && a.ExternalKey == row.BusinessAccountExternalKey)
                    .Select(a => a.Id)
                    .ToList();

                if (matches.Count == 1)
                {
                    row.BusinessAccountId = matches[0];
                    row.ReviewNotes = null;
                    return;
                }
            }

            if (!String.IsNullOrWhiteSpace(row.BusinessAccountName))
            {
                var matches = _context.BusinessAccounts
                    .Where(a => a.IsActive && a.Name.ToLower() == row.BusinessAccountName!.ToLower())
                    .Select(a => a.Id)
                    .ToList();

                if (matches.Count == 1)
                {
                    row.BusinessAccountId = matches[0];
                    row.ReviewNotes = null;
                    return;
                }
            }

            row.Status = row.Status == ImportRowStatus.Rejected ? row.Status : ImportRowStatus.PendingReview;
            row.ReviewNotes = "Business account could not be resolved automatically.";
        }

        private void MatchAgreement(ImportRow row)
        {
            if (!row.BusinessAccountId.HasValue || !row.SaleDate.HasValue)
            {
                row.SelectedAgreementId = null;
                row.Status = row.Status == ImportRowStatus.Rejected ? row.Status : ImportRowStatus.PendingReview;
                return;
            }

            if (row.SelectedAgreementId.HasValue)
            {
                var selectedAgreement = _context.CommissionAgreements
                    .AsNoTracking()
                    .SingleOrDefault(a => a.Id == row.SelectedAgreementId.Value);

                if (selectedAgreement != null
                    && selectedAgreement.BusinessAccountId == row.BusinessAccountId.Value
                    && selectedAgreement.IsActive
                    && selectedAgreement.EffectiveStartDate <= row.SaleDate.Value
                    && selectedAgreement.EffectiveEndDate >= row.SaleDate.Value
                    && (string.IsNullOrWhiteSpace(selectedAgreement.ProductNameFilter) || selectedAgreement.ProductNameFilter == row.ProductName))
                {
                    row.Status = ImportRowStatus.ReadyToPost;
                    row.ReviewNotes = null;
                    return;
                }

                row.SelectedAgreementId = null;
            }

            var agreements = _context.CommissionAgreements
                .Where(a => a.BusinessAccountId == row.BusinessAccountId.Value
                    && a.IsActive
                    && a.EffectiveStartDate <= row.SaleDate.Value
                    && a.EffectiveEndDate >= row.SaleDate.Value)
                .Where(a => string.IsNullOrWhiteSpace(a.ProductNameFilter) || a.ProductNameFilter == row.ProductName)
                .Select(a => a.Id)
                .ToList();

            if (agreements.Count == 1)
            {
                row.SelectedAgreementId = agreements[0];
                row.Status = ImportRowStatus.ReadyToPost;
                row.ReviewNotes = null;
            }
            else if (agreements.Count == 0)
            {
                row.SelectedAgreementId = null;
                row.Status = ImportRowStatus.PendingReview;
                row.ReviewNotes = "No active agreement matched this row.";
            }
            else
            {
                row.SelectedAgreementId = null;
                row.Status = ImportRowStatus.PendingReview;
                row.ReviewNotes = "Multiple active agreements matched this row.";
            }
        }

        private static string? GetMappedValue(
            IReadOnlyDictionary<string, string?> rawValues,
            IReadOnlyDictionary<string, string> mappings,
            string targetField)
        {
            if (!mappings.TryGetValue(targetField, out var sourceColumn))
            {
                return null;
            }

            return rawValues.TryGetValue(sourceColumn, out var value) ? value : null;
        }

        private static decimal CalculateRecipientAmount(
            CommissionAgreementRecipient recipient,
            decimal grossAmount,
            decimal netAmount,
            int quantity,
            IReadOnlyDictionary<int, decimal> computedAmounts)
        {
            decimal amount = recipient.CalculationType switch
            {
                CommissionRecipientCalculationType.FlatAmountPerOrder => recipient.RateOrAmount,
                CommissionRecipientCalculationType.FlatAmountPerUnit => recipient.RateOrAmount * quantity,
                CommissionRecipientCalculationType.PercentOfGross => grossAmount * (recipient.RateOrAmount / 100m),
                CommissionRecipientCalculationType.PercentOfNet => netAmount * (recipient.RateOrAmount / 100m),
                CommissionRecipientCalculationType.PercentOfRecipientCommission when recipient.BasisRecipientId.HasValue
                    && computedAmounts.TryGetValue(recipient.BasisRecipientId.Value, out var upstreamCommission)
                        => upstreamCommission * (recipient.RateOrAmount / 100m),
                _ => 0m
            };

            return Decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        }

        private static object BuildCalculationDetails(
            CommissionAgreementRecipient recipient,
            SaleEvent saleEvent,
            decimal commissionAmount,
            decimal netAmount)
        {
            return new
            {
                calculationType = recipient.CalculationType.ToString(),
                rateOrAmount = recipient.RateOrAmount,
                basisRecipientId = recipient.BasisRecipientId,
                quantity = saleEvent.Quantity,
                grossAmount = saleEvent.GrossAmount,
                netAmount,
                commissionAmount
            };
        }

        public static async Task<IReadOnlyList<IDictionary<string, string?>>> ReadCsvRowsAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            using var reader = new StreamReader(stream, leaveOpen: true);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            await csv.ReadAsync();
            csv.ReadHeader();
            var header = csv.HeaderRecord ?? Array.Empty<string>();
            var rows = new List<IDictionary<string, string?>>();

            while (await csv.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (var column in header)
                {
                    row[column] = csv.GetField(column);
                }

                rows.Add(row);
            }

            stream.Position = 0;
            return rows;
        }
    }
}
