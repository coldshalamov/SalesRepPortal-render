using System.ComponentModel.DataAnnotations;

namespace LeadManagementPortal.Models
{
    public enum CommissionRecipientCalculationType
    {
        FlatAmountPerOrder = 0,
        FlatAmountPerUnit = 1,
        PercentOfGross = 2,
        PercentOfNet = 3,
        PercentOfRecipientCommission = 4
    }

    public enum ImportBatchStatus
    {
        PendingReview = 0,
        ReadyToPost = 1,
        Posted = 2
    }

    public enum ImportRowStatus
    {
        PendingMapping = 0,
        PendingReview = 1,
        ReadyToPost = 2,
        Posted = 3,
        Rejected = 4
    }

    public class BusinessAccount
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? ExternalKey { get; set; }

        [MaxLength(4000)]
        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public ICollection<CommissionAgreement> Agreements { get; set; } = new List<CommissionAgreement>();
        public ICollection<SaleEvent> SaleEvents { get; set; } = new List<SaleEvent>();
    }

    public class CommissionAgreement
    {
        public int Id { get; set; }
        public int BusinessAccountId { get; set; }
        public BusinessAccount? BusinessAccount { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        public DateTime EffectiveStartDate { get; set; }
        public DateTime EffectiveEndDate { get; set; }
        public bool IsActive { get; set; } = true;

        [MaxLength(200)]
        public string? ProductNameFilter { get; set; }

        [MaxLength(4000)]
        public string? Notes { get; set; }

        public ICollection<CommissionAgreementRecipient> Recipients { get; set; } = new List<CommissionAgreementRecipient>();
        public ICollection<CommissionLedgerEntry> LedgerEntries { get; set; } = new List<CommissionLedgerEntry>();
    }

    public class CommissionAgreementRecipient
    {
        public int Id { get; set; }
        public int CommissionAgreementId { get; set; }
        public CommissionAgreement? CommissionAgreement { get; set; }

        [Required]
        public string BeneficiaryId { get; set; } = string.Empty;
        public ApplicationUser? Beneficiary { get; set; }

        public CommissionRecipientCalculationType CalculationType { get; set; }
        public decimal RateOrAmount { get; set; }
        public int? BasisRecipientId { get; set; }
        public CommissionAgreementRecipient? BasisRecipient { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public int SortOrder { get; set; }
        public ICollection<CommissionAgreementRecipient> DownstreamRecipients { get; set; } = new List<CommissionAgreementRecipient>();
        public ICollection<CommissionLedgerEntry> LedgerEntries { get; set; } = new List<CommissionLedgerEntry>();
    }

    public class ImportProfile
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? SourceSystem { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [Required]
        public string ColumnMappingsJson { get; set; } = "{}";

        public ICollection<ImportBatch> ImportBatches { get; set; } = new List<ImportBatch>();
    }

    public class ImportBatch
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string SourceSystem { get; set; } = string.Empty;

        public int? ImportProfileId { get; set; }
        public ImportProfile? ImportProfile { get; set; }

        public string? UploadedById { get; set; }
        public ApplicationUser? UploadedBy { get; set; }

        public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
        public ImportBatchStatus Status { get; set; } = ImportBatchStatus.PendingReview;

        [MaxLength(260)]
        public string? SourceFileName { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public ICollection<ImportRow> Rows { get; set; } = new List<ImportRow>();
    }

    public class ImportRow
    {
        public int Id { get; set; }
        public int ImportBatchId { get; set; }
        public ImportBatch? ImportBatch { get; set; }
        public int RowNumber { get; set; }
        public ImportRowStatus Status { get; set; } = ImportRowStatus.PendingMapping;

        [MaxLength(200)]
        public string? ExternalRowId { get; set; }

        public int? BusinessAccountId { get; set; }
        public BusinessAccount? BusinessAccount { get; set; }

        [MaxLength(100)]
        public string? BusinessAccountExternalKey { get; set; }

        [MaxLength(200)]
        public string? BusinessAccountName { get; set; }

        public int? SelectedAgreementId { get; set; }
        public CommissionAgreement? SelectedAgreement { get; set; }

        public int? SaleEventId { get; set; }
        public SaleEvent? SaleEvent { get; set; }

        [MaxLength(256)]
        public string? ProductName { get; set; }

        public int? Quantity { get; set; }
        public decimal? GrossAmount { get; set; }
        public decimal? CostAmount { get; set; }
        public DateTime? SaleDate { get; set; }

        [MaxLength(450)]
        public string? CreditedRepId { get; set; }
        public ApplicationUser? CreditedRep { get; set; }

        [Required]
        public string RawPayloadJson { get; set; } = "{}";

        [Required]
        public string MappedPayloadJson { get; set; } = "{}";

        [MaxLength(2000)]
        public string? ReviewNotes { get; set; }
    }

    public class SaleEvent
    {
        public int Id { get; set; }
        public int BusinessAccountId { get; set; }
        public BusinessAccount? BusinessAccount { get; set; }

        [MaxLength(200)]
        public string? ExternalRowId { get; set; }

        public DateTime SaleDate { get; set; }

        [Required]
        [MaxLength(256)]
        public string ProductName { get; set; } = string.Empty;

        public int Quantity { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal? CostAmount { get; set; }

        [MaxLength(450)]
        public string? CreditedRepId { get; set; }
        public ApplicationUser? CreditedRep { get; set; }

        [Required]
        [MaxLength(100)]
        public string SourceSystem { get; set; } = string.Empty;

        [Required]
        public string RawPayloadJson { get; set; } = "{}";

        [Required]
        [MaxLength(450)]
        public string PostedById { get; set; } = string.Empty;
        public ApplicationUser? PostedBy { get; set; }

        public DateTime PostedAtUtc { get; set; } = DateTime.UtcNow;

        public ICollection<CommissionLedgerEntry> LedgerEntries { get; set; } = new List<CommissionLedgerEntry>();
    }

    public class CommissionLedgerEntry
    {
        public int Id { get; set; }
        public int SaleEventId { get; set; }
        public SaleEvent? SaleEvent { get; set; }
        public int CommissionAgreementId { get; set; }
        public CommissionAgreement? CommissionAgreement { get; set; }
        public int? CommissionAgreementRecipientId { get; set; }
        public CommissionAgreementRecipient? CommissionAgreementRecipient { get; set; }

        [Required]
        [MaxLength(450)]
        public string BeneficiaryId { get; set; } = string.Empty;
        public ApplicationUser? Beneficiary { get; set; }

        public decimal GrossAmount { get; set; }
        public decimal NetAmount { get; set; }
        public decimal CommissionAmount { get; set; }
        public CommissionRecipientCalculationType CalculationType { get; set; }

        [Required]
        public string CalculationDetailsJson { get; set; } = "{}";

        public DateTime EarnedAtUtc { get; set; } = DateTime.UtcNow;
        public ICollection<PayoutEntry> PayoutEntries { get; set; } = new List<PayoutEntry>();
    }

    public class CommissionAdjustment
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(450)]
        public string BeneficiaryId { get; set; } = string.Empty;
        public ApplicationUser? Beneficiary { get; set; }

        public decimal Amount { get; set; }

        [Required]
        [MaxLength(200)]
        public string Reason { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Notes { get; set; }

        [Required]
        [MaxLength(450)]
        public string CreatedById { get; set; } = string.Empty;
        public ApplicationUser? CreatedBy { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public ICollection<PayoutEntry> PayoutEntries { get; set; } = new List<PayoutEntry>();
    }

    public class PayoutBatch
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Reference { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Notes { get; set; }

        [Required]
        [MaxLength(450)]
        public string CreatedById { get; set; } = string.Empty;
        public ApplicationUser? CreatedBy { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAtUtc { get; set; }

        public ICollection<PayoutEntry> Entries { get; set; } = new List<PayoutEntry>();
    }

    public class PayoutEntry
    {
        public int Id { get; set; }
        public int PayoutBatchId { get; set; }
        public PayoutBatch? PayoutBatch { get; set; }

        [Required]
        [MaxLength(450)]
        public string BeneficiaryId { get; set; } = string.Empty;
        public ApplicationUser? Beneficiary { get; set; }

        public int? CommissionLedgerEntryId { get; set; }
        public CommissionLedgerEntry? CommissionLedgerEntry { get; set; }

        public int? CommissionAdjustmentId { get; set; }
        public CommissionAdjustment? CommissionAdjustment { get; set; }

        public decimal Amount { get; set; }
    }
}
