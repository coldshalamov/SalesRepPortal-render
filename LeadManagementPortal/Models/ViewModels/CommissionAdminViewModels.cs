using System.ComponentModel.DataAnnotations;

namespace LeadManagementPortal.Models.ViewModels
{
    public class BusinessAccountEditViewModel
    {
        public int? Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(100)]
        public string? ExternalKey { get; set; }

        [StringLength(4000)]
        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class CommissionAgreementEditViewModel
    {
        public int? Id { get; set; }

        [Required]
        public int BusinessAccountId { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        public DateTime EffectiveStartDate { get; set; } = DateTime.UtcNow.Date;

        [DataType(DataType.Date)]
        public DateTime EffectiveEndDate { get; set; } = DateTime.UtcNow.Date.AddYears(1);

        public bool IsActive { get; set; } = true;

        [StringLength(200)]
        public string? ProductNameFilter { get; set; }

        [StringLength(4000)]
        public string? Notes { get; set; }

        public List<CommissionAgreementRecipientEditViewModel> Recipients { get; set; } = new();
    }

    public class CommissionAgreementRecipientEditViewModel
    {
        public int? Id { get; set; }

        [Required]
        public string BeneficiaryId { get; set; } = string.Empty;

        [Required]
        public CommissionRecipientCalculationType CalculationType { get; set; }

        [Range(0, 99999999)]
        public decimal RateOrAmount { get; set; }

        public int? BasisRecipientKey { get; set; }
        public int SortOrder { get; set; }
        public string? Notes { get; set; }
    }

    public class ImportProfileEditViewModel
    {
        public int? Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(100)]
        public string? SourceSystem { get; set; }

        public bool IsActive { get; set; } = true;

        public string? ExternalRowIdColumn { get; set; }
        public string? BusinessAccountExternalKeyColumn { get; set; }
        public string? BusinessAccountNameColumn { get; set; }
        public string? ProductNameColumn { get; set; }
        public string? QuantityColumn { get; set; }
        public string? GrossAmountColumn { get; set; }
        public string? CostAmountColumn { get; set; }
        public string? SaleDateColumn { get; set; }
        public string? CreditedRepIdColumn { get; set; }
    }

    public class ImportBatchReviewViewModel
    {
        public ImportBatch Batch { get; set; } = null!;
        public List<ImportRow> Rows { get; set; } = new();
    }

    public class ImportRowEditViewModel
    {
        public int Id { get; set; }
        public int ImportBatchId { get; set; }
        public int RowNumber { get; set; }
        public ImportRowStatus Status { get; set; }
        public string? ExternalRowId { get; set; }
        public int? BusinessAccountId { get; set; }
        public string? BusinessAccountExternalKey { get; set; }
        public string? BusinessAccountName { get; set; }
        public int? SelectedAgreementId { get; set; }
        public string? ProductName { get; set; }
        public int? Quantity { get; set; }
        public decimal? GrossAmount { get; set; }
        public decimal? CostAmount { get; set; }
        public DateTime? SaleDate { get; set; }
        public string? CreditedRepId { get; set; }
        public string? ReviewNotes { get; set; }
        public string RawPayloadJson { get; set; } = "{}";
        public string MappedPayloadJson { get; set; } = "{}";
    }

    public class CommissionAdjustmentEditViewModel
    {
        [Required]
        public string BeneficiaryId { get; set; } = string.Empty;

        [Range(-99999999, 99999999)]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(200)]
        public string Reason { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Notes { get; set; }
    }

    public class PayoutBatchCreateViewModel
    {
        [Required]
        [StringLength(100)]
        public string Reference { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Notes { get; set; }

        public string BeneficiaryId { get; set; } = string.Empty;
        public string BeneficiaryName { get; set; } = string.Empty;
        public List<PayoutSelectionItemViewModel> Items { get; set; } = new();
    }

    public class PayoutSelectionItemViewModel
    {
        public bool IsSelected { get; set; }
        public string ItemType { get; set; } = string.Empty;
        public int SourceId { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal OutstandingAmount { get; set; }
        public decimal SelectedAmount { get; set; }
    }
}
