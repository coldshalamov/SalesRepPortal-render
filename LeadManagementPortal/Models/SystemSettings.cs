namespace LeadManagementPortal.Models
{
    public class SystemSettings
    {
        public int Id { get; set; } = 1;
        public int CoolingPeriodDays { get; set; } = 15;
        public int LeadInitialExpiryDays { get; set; } = 15;
        public int LeadExtensionDays { get; set; } = 5;
    }
}
