namespace LeadManagementPortal.Models.ViewModels
{
    public class CommissionDashboardViewModel
    {
        public decimal TotalCommissionEarned { get; set; }
        public decimal CurrentMonthCommission { get; set; }
        public int TotalLedgerRows { get; set; }
        public List<CommissionDealBreakdownViewModel> BreakdownByDealType { get; set; } = new List<CommissionDealBreakdownViewModel>();
        public List<CommissionLedgerRowViewModel> DetailRows { get; set; } = new List<CommissionLedgerRowViewModel>();
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
        public int SaleRecordId { get; set; }
        public string BeneficiaryId { get; set; } = string.Empty;
        public string BeneficiaryName { get; set; } = string.Empty;
        public DateTime SaleDate { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal GrossAmount { get; set; }
        public decimal NetAmount { get; set; }
        public decimal CommissionAmount { get; set; }
        public int ChainDepth { get; set; }
        public string DealType { get; set; } = string.Empty;
        public string CalculationBasis { get; set; } = string.Empty;
        public string CalculationNotes { get; set; } = string.Empty;
    }
}
