using LeadManagementPortal.Models;
using LeadManagementPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace LeadManagementPortal.Controllers
{
    [Authorize]
    public class CustomersController : Controller
    {
        private readonly ICustomerService _customerService;
        private readonly ISalesGroupService _salesGroupService;
        private readonly ISalesOrgService _salesOrgService;
        private readonly UserManager<ApplicationUser> _userManager;

        public CustomersController(
            ICustomerService customerService,
            ISalesGroupService salesGroupService,
            ISalesOrgService salesOrgService,
            UserManager<ApplicationUser> userManager)
        {
            _customerService = customerService;
            _salesGroupService = salesGroupService;
            _salesOrgService = salesOrgService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string? searchTerm, string? salesGroupId, int? salesOrgId, string? salesRepId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "";

            IEnumerable<Customer> customers;

            if (!string.IsNullOrEmpty(searchTerm))
            {
                customers = await _customerService.SearchAsync(searchTerm, userId, userRole);
            }
            else
            {
                customers = await _customerService.GetByUserAsync(userId, userRole);
            }

            // Apply filters
            if (!string.IsNullOrEmpty(salesGroupId))
            {
                customers = customers.Where(c => c.SalesGroupId == salesGroupId);
            }
            if (salesOrgId.HasValue)
            {
                customers = customers.Where(c => c.SalesRep != null && c.SalesRep.SalesOrgId == salesOrgId);
            }
            if (!string.IsNullOrEmpty(salesRepId))
            {
                customers = customers.Where(c => c.SalesRepId == salesRepId);
            }

            ViewBag.SearchTerm = searchTerm;
            ViewBag.SelectedSalesGroupId = salesGroupId;
            ViewBag.SelectedSalesOrgId = salesOrgId;
            ViewBag.SelectedSalesRepId = salesRepId;

            await PopulateIndexFilters(userRole, userId);

            return View(customers);
        }

        private async Task PopulateIndexFilters(string userRole, string userId)
        {
            if (userRole == UserRoles.OrganizationAdmin)
            {
                ViewBag.SalesGroups = new SelectList(await _salesGroupService.GetAllAsync(), "Id", "Name");
                ViewBag.SalesOrgs = new SelectList(await _salesOrgService.GetAllAsync(), "Id", "Name");
                ViewBag.SalesReps = new SelectList(await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FirstName).ToListAsync(), "Id", "FullName");
            }
            else if (userRole == UserRoles.GroupAdmin)
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user?.SalesGroupId != null)
                {
                    // Group Admin sees SalesOrgs and SalesReps in their group
                    ViewBag.SalesOrgs = new SelectList(await _salesOrgService.GetByGroupIdAsync(user.SalesGroupId), "Id", "Name");
                    var groupReps = await _userManager.Users.Where(u => u.SalesGroupId == user.SalesGroupId && u.IsActive).OrderBy(u => u.FirstName).ToListAsync();
                    ViewBag.SalesReps = new SelectList(groupReps, "Id", "FullName");
                }
            }
            else if (userRole == UserRoles.SalesOrgAdmin)
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user?.SalesOrgId != null)
                {
                    // Sales Org Admin sees SalesReps in their org
                    var orgReps = await _userManager.Users.Where(u => u.SalesOrgId == user.SalesOrgId && u.IsActive).OrderBy(u => u.FirstName).ToListAsync();
                    ViewBag.SalesReps = new SelectList(orgReps, "Id", "FullName");
                }
            }
            else if (userRole == UserRoles.SalesRep)
            {
                // Sales Rep sees filters for themselves, their org, and their group
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    if (user.SalesGroupId != null)
                    {
                        var group = await _salesGroupService.GetByIdAsync(user.SalesGroupId);
                        ViewBag.SalesGroups = new SelectList(new[] { group }, "Id", "Name");
                    }
                    if (user.SalesOrgId != null)
                    {
                        var org = await _salesOrgService.GetByIdAsync(user.SalesOrgId.Value);
                        ViewBag.SalesOrgs = new SelectList(new[] { org }, "Id", "Name");
                    }
                    ViewBag.SalesReps = new SelectList(new[] { user }, "Id", "FullName");
                }
            }
        }

        public async Task<IActionResult> Details(string id)
        {
            var customer = await _customerService.GetByIdAsync(id);
            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "OrganizationAdmin")]
        public async Task<IActionResult> Delete(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var success = await _customerService.SoftDeleteAsync(id, userId);
            if (success)
            {
                TempData["SuccessMessage"] = "Customer deleted. Corresponding lead marked as Lost.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to delete customer.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
