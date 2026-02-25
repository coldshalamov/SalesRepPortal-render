using LeadManagementPortal.Models;
using LeadManagementPortal.Models.ViewModels;
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
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "";

            var customer = await _customerService.GetAccessibleByIdAsync(id, userId, userRole);
            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "";

            var customer = await _customerService.GetAccessibleByIdAsync(id, userId, userRole);
            if (customer == null)
            {
                return NotFound();
            }

            bool canChangeOwner = User.IsInRole(UserRoles.OrganizationAdmin)
                || User.IsInRole(UserRoles.GroupAdmin)
                || User.IsInRole(UserRoles.SalesOrgAdmin)
                || (User.IsInRole(UserRoles.SalesRep) && customer.SalesRepId == userId);

            ViewBag.CanChangeOwner = canChangeOwner;
            ViewBag.SalesReps = new SelectList(Enumerable.Empty<ApplicationUser>(), "Id", "FullName");

            if (canChangeOwner)
            {
                int? orgId = customer.SalesRep?.SalesOrgId;
                if (!orgId.HasValue)
                {
                    var currentUser = await _userManager.FindByIdAsync(userId);
                    orgId = currentUser?.SalesOrgId;
                }

                if (orgId.HasValue)
                {
                    var reps = await _userManager.Users
                        .Where(u => u.IsActive && u.SalesOrgId == orgId.Value)
                        .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                        .ToListAsync();

                    ViewBag.SalesReps = new SelectList(reps, "Id", "FullName", customer.SalesRepId);
                }
            }

            var vm = new CustomerEditViewModel
            {
                Id = customer.Id,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                Email = customer.Email,
                Phone = customer.Phone,
                Company = customer.Company,
                Address = customer.Address,
                City = customer.City,
                State = customer.State,
                ZipCode = customer.ZipCode,
                Notes = customer.Notes,
                SalesRepId = customer.SalesRepId
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(CustomerEditViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "";

            var existing = await _customerService.GetAccessibleByIdAsync(model.Id, userId, userRole);
            if (existing == null)
            {
                return NotFound();
            }

            bool canChangeOwner = User.IsInRole(UserRoles.OrganizationAdmin)
                || User.IsInRole(UserRoles.GroupAdmin)
                || User.IsInRole(UserRoles.SalesOrgAdmin)
                || (User.IsInRole(UserRoles.SalesRep) && existing.SalesRepId == userId);

            ViewBag.CanChangeOwner = canChangeOwner;

            string? newSalesRepId = existing.SalesRepId;
            string? newSalesGroupId = existing.SalesGroupId;

            if (canChangeOwner && !string.IsNullOrWhiteSpace(model.SalesRepId) && model.SalesRepId != existing.SalesRepId)
            {
                int? existingOrgId = existing.SalesRep?.SalesOrgId;
                if (!existingOrgId.HasValue && !string.IsNullOrWhiteSpace(existing.SalesRepId))
                {
                    var existingOwner = await _userManager.FindByIdAsync(existing.SalesRepId);
                    existingOrgId = existingOwner?.SalesOrgId;
                }

                if (!existingOrgId.HasValue)
                {
                    ModelState.AddModelError(nameof(CustomerEditViewModel.SalesRepId), "Cannot change Sales Rep because the current sales organization is unknown.");
                }
                else
                {
                    var newOwner = await _userManager.FindByIdAsync(model.SalesRepId);
                    if (newOwner == null || !newOwner.IsActive)
                    {
                        ModelState.AddModelError(nameof(CustomerEditViewModel.SalesRepId), "Select a valid active sales rep.");
                    }
                    else if (newOwner.SalesOrgId != existingOrgId.Value)
                    {
                        ModelState.AddModelError(nameof(CustomerEditViewModel.SalesRepId), "Sales Rep must be in the same sales organization.");
                    }
                    else
                    {
                        newSalesRepId = newOwner.Id;
                        newSalesGroupId = newOwner.SalesGroupId;
                    }
                }
            }
            else if (!canChangeOwner)
            {
                // Ignore any posted value if user cannot change owner.
                ModelState.Remove(nameof(CustomerEditViewModel.SalesRepId));
            }

            // Repopulate dropdown on validation errors
            ViewBag.SalesReps = new SelectList(Enumerable.Empty<ApplicationUser>(), "Id", "FullName");
            if (canChangeOwner)
            {
                int? orgId = existing.SalesRep?.SalesOrgId;
                if (!orgId.HasValue)
                {
                    var currentUser = await _userManager.FindByIdAsync(userId);
                    orgId = currentUser?.SalesOrgId;
                }

                if (orgId.HasValue)
                {
                    var reps = await _userManager.Users
                        .Where(u => u.IsActive && u.SalesOrgId == orgId.Value)
                        .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                        .ToListAsync();

                    ViewBag.SalesReps = new SelectList(reps, "Id", "FullName", newSalesRepId);
                }
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var updated = new Customer
            {
                Id = model.Id,
                FirstName = model.FirstName,
                LastName = model.LastName,
                Email = model.Email,
                Phone = model.Phone,
                Company = model.Company,
                Address = model.Address,
                City = model.City,
                State = model.State,
                ZipCode = model.ZipCode,
                Notes = model.Notes,
                SalesRepId = newSalesRepId,
                SalesGroupId = newSalesGroupId
            };

            var ok = await _customerService.UpdateAsync(updated);
            if (ok)
            {
                TempData["SuccessMessage"] = "Customer updated successfully!";
                return RedirectToAction(nameof(Details), new { id = model.Id });
            }

            ModelState.AddModelError(string.Empty, "Failed to update customer.");
            return View(model);
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
