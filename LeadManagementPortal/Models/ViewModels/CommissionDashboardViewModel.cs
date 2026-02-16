namespace LeadManagementPortal.Models.ViewModels
{
    public class CommissionDashboardViewModel
    {
        public decimal TotalCommissionEarned { get; set; }
        public decimal CurrentMonthCommission { get; set; }
        public decimal PendingPayouts { get; set; }
        public List<CommissionDealViewModel> RecentDeals { get; set; } = new List<CommissionDealViewModel>();
    }

    public class CommissionDealViewModel
    {
        public DateTime Date { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Product { get; set; } = string.Empty;
        public decimal SaleAmount { get; set; }
        public decimal CommissionAmount { get; set; }
        public string Status { get; set; } = "Paid"; // Paid, Pending
    }
}
