namespace LeadManagementPortal.Models
{
    public class SaleRecord
    {
        public int Id { get; set; }

        public string AccountId { get; set; } = string.Empty;
        public virtual ApplicationUser? Account { get; set; }

        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal? CostAmount { get; set; }
        public DateTime SaleDate { get; set; }
        public string ImportBatchId { get; set; } = string.Empty;
        public DateTime ImportedAt { get; set; }
        public string RawPayload { get; set; } = "{}";

        public virtual ICollection<CommissionLedger> CommissionLedgers { get; set; } = new List<CommissionLedger>();
    }
}
