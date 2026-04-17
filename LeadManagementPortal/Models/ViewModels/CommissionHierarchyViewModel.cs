namespace LeadManagementPortal.Models.ViewModels
{
    public class CommissionHierarchyViewModel
    {
        public List<CommissionHierarchyNodeViewModel> Nodes { get; set; } = new();
        public int TotalAccounts { get; set; }
        public int RootAccounts { get; set; }
        public int LinkedAccounts { get; set; }
        public int ConfiguredDeals { get; set; }
    }

    public class CommissionHierarchyNodeViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? SponsorId { get; set; }
        public string? SponsorName { get; set; }
        public string? DealType { get; set; }
        public string? CalculationBasis { get; set; }
        public decimal? Rate { get; set; }
        public decimal? BaseCost { get; set; }
        public bool IsOrphan { get; set; }
        public int DownlineCount { get; set; }
    }

    public class SaveCommissionHierarchyRequest
    {
        public string AccountId { get; set; } = string.Empty;
        public string? SponsorId { get; set; }
        public CommissionDealType? CommissionDealType { get; set; }
        public CommissionCalculationBasis? CommissionCalculationBasis { get; set; }
        public decimal? CommissionRate { get; set; }
        public decimal? CommissionBaseCost { get; set; }
    }
}
