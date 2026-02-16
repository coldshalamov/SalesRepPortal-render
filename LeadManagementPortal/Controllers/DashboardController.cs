using LeadManagementPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LeadManagementPortal.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly IDashboardService _dashboardService;
        private readonly ILeadService _leadService;

        public DashboardController(IDashboardService dashboardService, ILeadService leadService)
        {
            _dashboardService = dashboardService;
            _leadService = leadService;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "";

            var stats = await _dashboardService.GetDashboardStatsAsync(userId, userRole);
            var recentLeads = await _leadService.GetByUserAsync(userId, userRole);

            ViewBag.Stats = stats;
            ViewBag.RecentLeads = recentLeads.Take(10);

            return View();
        }
    }
}
