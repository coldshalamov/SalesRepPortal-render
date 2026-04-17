namespace LeadManagementPortal.Models.ViewModels
{
    public class CommissionDashboardViewModel
    {
        public bool IsAdminView { get; set; }
        public decimal TotalCommissionEarned { get; set; }
        public decimal CurrentMonthCommission { get; set; }
        public decimal TotalAdjustments { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal OutstandingBalance { get; set; }
        public int TotalLedgerRows { get; set; }
        public int BusinessAccountCount { get; set; }
        public int ActiveAgreementCount { get; set; }
        public int PendingReviewRows { get; set; }
        public int ReadyToPostRows { get; set; }
        public List<CommissionDealBreakdownViewModel> BreakdownByDealType { get; set; } = new();
        public List<CommissionLedgerRowViewModel> DetailRows { get; set; } = new();
        public List<CommissionImportBatchViewModel> RecentImportBatches { get; set; } = new();
        public List<OutstandingBeneficiaryBalanceViewModel> OutstandingBeneficiaryBalances { get; set; } = new();
    }

    public class CommissionDealBreakdownViewModel
    {
        public string DealType { get; set; } = string.Empty;
        public decimal TotalCommission { get; set; }
        public int RowCount { get; set; }
    }

    public class CommissionLedgerRowViewModel
    {
        public int Id { get; set; }
        public int SaleEventId { get; set; }
        public string BeneficiaryId { get; set; } = string.Empty;
        public string BeneficiaryName { get; set; } = string.Empty;
        public DateTime SaleDate { get; set; }
        public string SourceSystem { get; set; } = string.Empty;
        public string BusinessAccountName { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public decimal GrossAmount { get; set; }
        public decimal NetAmount { get; set; }
        public decimal CommissionAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal OutstandingAmount { get; set; }
        public string DealType { get; set; } = string.Empty;
        public string CalculationBasis { get; set; } = string.Empty;
        public string CalculationNotes { get; set; } = string.Empty;
    }

    public class CommissionImportBatchViewModel
    {
        public int Id { get; set; }
        public string SourceSystem { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int RowCount { get; set; }
        public DateTime ReceivedAtUtc { get; set; }
    }

    public class OutstandingBeneficiaryBalanceViewModel
    {
        public string BeneficiaryId { get; set; } = string.Empty;
        public string BeneficiaryName { get; set; } = string.Empty;
        public decimal OutstandingBalance { get; set; }
    }
}
