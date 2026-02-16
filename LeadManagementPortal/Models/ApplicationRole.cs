using Microsoft.AspNetCore.Identity;

namespace LeadManagementPortal.Models
{
    public class ApplicationRole : IdentityRole
    {
        public string? Description { get; set; }
    }
}
