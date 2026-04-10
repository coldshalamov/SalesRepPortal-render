namespace LeadManagementPortal.Models
{
    public class CommissionLedger
    {
        public int Id { get; set; }

        public int SaleRecordId { get; set; }
        public virtual SaleRecord? SaleRecord { get; set; }

        public string BeneficiaryId { get; set; } = string.Empty;
        public virtual ApplicationUser? Beneficiary { get; set; }

        public decimal GrossAmount { get; set; }
        public decimal NetAmount { get; set; }
        public decimal CommissionAmount { get; set; }
        public int ChainDepth { get; set; }
        public string DealSnapshot { get; set; } = "{}";
        public string CalculationNotes { get; set; } = string.Empty;
    }
}
