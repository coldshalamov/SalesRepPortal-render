using LeadManagementPortal.Controllers;
using LeadManagementPortal.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace LeadManagementPortal.Tests
{
    public class CommissionsControllerTests
    {
        [Fact]
        public void Index_ReturnsNonMisleadingDefaults()
        {
            var controller = new CommissionsController();

            var result = controller.Index();
            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<CommissionDashboardViewModel>(view.Model);

            Assert.Equal(0m, model.TotalCommissionEarned);
            Assert.Equal(0m, model.CurrentMonthCommission);
            Assert.Equal(0m, model.PendingPayouts);
            Assert.Empty(model.RecentDeals);
        }
    }
}

