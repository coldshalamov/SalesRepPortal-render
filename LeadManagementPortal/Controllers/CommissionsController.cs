using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LeadManagementPortal.Models.ViewModels;

namespace LeadManagementPortal.Controllers
{
    [Authorize]
    public class CommissionsController : Controller
    {
        public IActionResult Index()
        {
            var viewModel = new CommissionDashboardViewModel
            {
                TotalCommissionEarned = 0m,
                CurrentMonthCommission = 0m,
                PendingPayouts = 0m,
                RecentDeals = new List<CommissionDealViewModel>()
            };

            return View(viewModel);
        }
    }
}
