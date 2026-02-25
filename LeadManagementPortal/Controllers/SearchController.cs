using LeadManagementPortal.Services;
using LeadManagementPortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LeadManagementPortal.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SearchController : ControllerBase
    {
        private readonly ILeadService _leadService;
        private readonly ICustomerService _customerService;

        public SearchController(ILeadService leadService, ICustomerService customerService)
        {
            _leadService = leadService;
            _customerService = customerService;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string? q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            {
                return Ok(new { leads = new List<object>(), customers = new List<object>() });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var userRole = ResolvePrimaryRole(User);
            if (string.IsNullOrWhiteSpace(userRole))
            {
                return Forbid();
            }

            // Use the Top variants to keep typeahead fast and avoid audit/log spam on keypress.
            const int maxPerType = 5;
            var term = q.Trim();
            var leads = await _leadService.SearchTopAsync(term, userId, userRole, maxPerType);
            var customers = await _customerService.SearchTopAsync(term, userId, userRole, maxPerType);

            return Ok(new
            {
                leads = leads.Select(l => new
                {
                    id = l.Id,
                    name = $"{l.FirstName} {l.LastName}",
                    company = l.Company,
                    urgency = l.UrgencyLevel,
                    status = l.Status.ToString(),
                    url = Url.Action("Details", "Leads", new { id = l.Id }) ?? $"/Leads/Details/{l.Id}"
                }),
                customers = customers.Select(c => new
                {
                    id = c.Id,
                    name = $"{c.FirstName} {c.LastName}",
                    company = c.Company,
                    url = Url.Action("Details", "Customers", new { id = c.Id }) ?? $"/Customers/Details/{c.Id}"
                })
            });
        }

        private static string? ResolvePrimaryRole(ClaimsPrincipal principal)
        {
            if (principal.IsInRole(UserRoles.OrganizationAdmin)) return UserRoles.OrganizationAdmin;
            if (principal.IsInRole(UserRoles.GroupAdmin)) return UserRoles.GroupAdmin;
            if (principal.IsInRole(UserRoles.SalesOrgAdmin)) return UserRoles.SalesOrgAdmin;
            if (principal.IsInRole(UserRoles.SalesRep)) return UserRoles.SalesRep;
            return null;
        }
    }
}
