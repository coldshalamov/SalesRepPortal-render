namespace LeadManagementPortal.Services
{
    public interface IDashboardService
    {
        Task<DashboardStats> GetDashboardStatsAsync(string userId, string userRole);
    }

    public class DashboardStats
    {
        public int TotalLeads { get; set; }
        public int PendingLeads { get; set; }
        public int ConvertedLeads { get; set; }
        public int ExpiredLeads { get; set; }
        public int CriticalLeads { get; set; }
        public int HighPriorityLeads { get; set; }
        public int LowPriorityLeads { get; set; }
        public int TotalCustomers { get; set; }
        public decimal ConversionRate { get; set; }
        public double AverageDaysToConvert { get; set; }
        public List<ConversionDataPoint> ConversionTrend { get; set; } = new();
    }

    public record ConversionDataPoint(string Label, int Count);
}
