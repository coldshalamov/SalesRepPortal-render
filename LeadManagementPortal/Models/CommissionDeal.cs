namespace LeadManagementPortal.Models
{
    public enum CommissionDealType
    {
        GrossPercent = 0,
        NetPercent = 1,
        Markup = 2,
        ProfitSplit = 3
    }

    public enum CommissionCalculationBasis
    {
        DownlineGross = 0,
        DownlineNet = 1,
        DownlineCommission = 2
    }

    public class CommissionDeal
    {
        public string ApplicationUserId { get; set; } = string.Empty;
        public virtual ApplicationUser? ApplicationUser { get; set; }

        public CommissionDealType DealType { get; set; }
        public decimal Rate { get; set; }
        public decimal? BaseCost { get; set; }
        public CommissionCalculationBasis CalculationBasis { get; set; }
    }
}
