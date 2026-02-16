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
            // MOCK DATA GENERATION
            // In a real scenario, this would come from a database query
            // linking Orders -> Commissions -> SalesRep

            var viewModel = new CommissionDashboardViewModel
            {
                TotalCommissionEarned = 12500.00m,
                CurrentMonthCommission = 1250.00m,
                PendingPayouts = 450.00m,
                RecentDeals = new List<CommissionDealViewModel>
                {
                    new CommissionDealViewModel
                    {
                        Date = DateTime.Today.AddDays(-2),
                        CustomerName = "Main Street Clinic",
                        Product = "GLP1",
                        SaleAmount = 1500.00m,
                        CommissionAmount = 150.00m,
                        Status = "Pending"
                    },
                    new CommissionDealViewModel
                    {
                        Date = DateTime.Today.AddDays(-5),
                        CustomerName = "Wellness Center North",
                        Product = "Peptides",
                        SaleAmount = 800.00m,
                        CommissionAmount = 80.00m,
                        Status = "Paid"
                    },
                     new CommissionDealViewModel
                    {
                        Date = DateTime.Today.AddDays(-12),
                        CustomerName = "Dr. Smith Practice",
                        Product = "Generics",
                        SaleAmount = 2500.00m,
                        CommissionAmount = 250.00m,
                        Status = "Paid"
                    },
                    new CommissionDealViewModel
                    {
                        Date = DateTime.Today.AddDays(-25),
                        CustomerName = "City Health",
                        Product = "TRT",
                        SaleAmount = 1200.00m,
                        CommissionAmount = 120.00m,
                        Status = "Paid"
                    }
                }
            };

            return View(viewModel);
        }
    }
}
