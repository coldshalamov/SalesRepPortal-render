namespace LeadManagementPortal.Models.ViewModels
{
    public class OrgChartGroupViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? LogoUrl { get; set; }
        public int MemberCount { get; set; }
        public List<OrgChartOrgViewModel> Orgs { get; set; } = new();
    }

    public class OrgChartOrgViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SalesGroupId { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public int MemberCount { get; set; }
    }

    public class OrganizationViewModel
    {
        public List<OrgChartGroupViewModel> Groups { get; set; } = new();
        public bool CanManageGroups { get; set; }
        public bool CanManageOrgs { get; set; }
    }
}
