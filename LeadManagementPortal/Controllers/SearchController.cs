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
        public async Task<IActionResult> Get([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Ok(new { leads = new List<object>(), customers = new List<object>() });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "";

            // The SearchAsync methods in both services are already implemented 
            // and respect the role-based visibility rules!
            var leads = await _leadService.SearchAsync(q, userId, userRole);
            var customers = await _customerService.SearchAsync(q, userId, userRole);

            return Ok(new
            {
                leads = leads.Take(5).Select(l => new
                {
                    id = l.Id,
                    name = $"{l.FirstName} {l.LastName}",
                    company = l.Company,
                    urgency = l.UrgencyLevel,
                    status = l.Status.ToString()
                }),
                customers = customers.Take(5).Select(c => new
                {
                    id = c.Id,
                    name = $"{c.FirstName} {c.LastName}",
                    company = c.Company
                })
            });
        }
    }
}
