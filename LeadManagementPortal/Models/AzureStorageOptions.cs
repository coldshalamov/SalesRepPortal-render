namespace LeadManagementPortal.Models
{
    public class AzureStorageOptions
    {
        public string AccountName { get; set; } = string.Empty;
        public string AccountKey { get; set; } = string.Empty;
        public string ContainerName { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
    }
}
