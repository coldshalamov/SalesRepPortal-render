using LeadManagementPortal.Models;
using LeadManagementPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace LeadManagementPortal.Controllers
{
    [Authorize]
    public class LeadsController : Controller
    {
        private readonly ILeadService _leadService;
        private readonly ISalesGroupService _salesGroupService;
        private readonly IProductService _productService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAddressService _addressService;
        private readonly ILeadAuditService _leadAuditService;
        private readonly ISalesOrgService _salesOrgService;
        private readonly INotificationService _notificationService;

        public LeadsController(
            ILeadService leadService,
            ISalesGroupService salesGroupService,
            IProductService productService,
            UserManager<ApplicationUser> userManager,
            IAddressService addressService,
            ILeadAuditService leadAuditService,
            ISalesOrgService salesOrgService,
            INotificationService notificationService)
        {
            _leadService = leadService;
            _salesGroupService = salesGroupService;
            _productService = productService;
            _userManager = userManager;
            _addressService = addressService;
            _leadAuditService = leadAuditService;
            _salesOrgService = salesOrgService;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Index(string? searchTerm, string? status, List<LeadStatus>? statuses, string? salesGroupId, int? salesOrgId, string? salesRepId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "";

            IEnumerable<Lead> leads;

            if (!string.IsNullOrEmpty(searchTerm))
            {
                leads = await _leadService.SearchAsync(searchTerm, userId, userRole);
            }
            else
            {
                leads = await _leadService.GetByUserAsync(userId, userRole);
            }

            // Apply filters
            if (!string.IsNullOrEmpty(salesGroupId))
            {
                leads = leads.Where(l => l.SalesGroupId == salesGroupId);
            }
            if (salesOrgId.HasValue)
            {
                leads = leads.Where(l => l.AssignedTo != null && l.AssignedTo.SalesOrgId == salesOrgId);
            }
            if (!string.IsNullOrEmpty(salesRepId))
            {
                leads = leads.Where(l => l.AssignedToId == salesRepId);
            }

            // Determine if user explicitly requested filtering (single or multi)
            var userProvidedFilter = (statuses != null && statuses.Count > 0) || !string.IsNullOrEmpty(status) || !string.IsNullOrEmpty(salesGroupId) || salesOrgId.HasValue || !string.IsNullOrEmpty(salesRepId);

            // Multi-status filtering takes precedence if provided
            if (statuses != null && statuses.Count > 0)
            {
                leads = leads.Where(l => statuses.Contains(l.Status));
            }
            else if (!string.IsNullOrEmpty(status) && Enum.TryParse<LeadStatus>(status, out var leadStatus))
            {
                leads = leads.Where(l => l.Status == leadStatus);
            }
            else
            {
                // No explicit filter: hide Lost / Expired by default
                var hiddenStatuses = new[] { LeadStatus.Lost, LeadStatus.Expired };
                var hiddenCount = leads.Count(l => hiddenStatuses.Contains(l.Status));
                if (hiddenCount > 0)
                {
                    ViewBag.HiddenCount = hiddenCount;
                }
                leads = leads.Where(l => !hiddenStatuses.Contains(l.Status));
            }

            ViewBag.SearchTerm = searchTerm;
            ViewBag.Status = status;
            ViewBag.SelectedStatuses = statuses ?? new List<LeadStatus>();
            ViewBag.FilterProvided = userProvidedFilter;
            ViewBag.SelectedSalesGroupId = salesGroupId;
            ViewBag.SelectedSalesOrgId = salesOrgId;
            ViewBag.SelectedSalesRepId = salesRepId;

            // Populate filter dropdowns based on role
            await PopulateIndexFilters(userRole, userId);

            return View(leads);
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
                    var orgReps = await _userManager.Users.Where(u => u.SalesOrgId == user.SalesOrgId && u.IsActive).OrderBy(u => u.FirstName).ToListAsync();
                    ViewBag.SalesReps = new SelectList(orgReps, "Id", "FullName");
                }
            }
            else if (userRole == UserRoles.SalesRep)
            {
                // Sales Rep sees filters but only for themselves (or maybe they want to see the structure?)
                // Requirement: "for sales rep he should see SalesRep , sales org, Sales Group filters"
                // Even if they can only see their own leads, we populate the dropdowns with what they are associated with.
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
            var lead = await _leadService.GetByIdAsync(id);
            if (lead == null)
            {
                return NotFound();
            }

            // Check access rights
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "";

            if (!await CanAccessLead(lead, userId, userRole))
            {
                return Forbid();
            }

            return View(lead);
        }

        [HttpGet]
        [Authorize(Roles = LeadManagementPortal.Models.UserRoles.SalesOrgAdmin + "," + LeadManagementPortal.Models.UserRoles.OrganizationAdmin + "," + LeadManagementPortal.Models.UserRoles.GroupAdmin)]
        public async Task<IActionResult> Reassign(string id)
        {
            var lead = await _leadService.GetByIdAsync(id);
            if (lead == null) return NotFound();

            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "";
            var admin = await _userManager.GetUserAsync(User);

            if (userRole == UserRoles.GroupAdmin)
            {
                if (admin?.SalesGroupId == null || lead.SalesGroupId != admin.SalesGroupId) return Forbid();
            }
            else if (userRole == UserRoles.SalesOrgAdmin)
            {
                if (admin?.SalesOrgId == null || lead.AssignedTo == null || lead.AssignedTo.SalesOrgId != admin.SalesOrgId) return Forbid();
            }
            else if (userRole != UserRoles.OrganizationAdmin)
            {
                return Forbid();
            }

            var repsQuery = _userManager.Users
                .Where(u => u.IsActive && u.SalesGroupId == lead.SalesGroupId);

            if (userRole == UserRoles.SalesOrgAdmin)
            {
                 repsQuery = repsQuery.Where(u => u.SalesOrgId == admin.SalesOrgId);
            }

            var reps = await repsQuery
                .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                .ToListAsync();
            ViewBag.SalesReps = new SelectList(reps, "Id", "FullName", lead.AssignedToId);

            var vm = new LeadReassignViewModel { LeadId = lead.Id, CurrentAssigneeId = lead.AssignedToId };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = LeadManagementPortal.Models.UserRoles.SalesOrgAdmin + "," + LeadManagementPortal.Models.UserRoles.OrganizationAdmin + "," + LeadManagementPortal.Models.UserRoles.GroupAdmin)]
        public async Task<IActionResult> Reassign(LeadReassignViewModel model)
        {
            var lead = await _leadService.GetByIdAsync(model.LeadId);
            if (lead == null) return NotFound();

            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "";
            var adminUser = await _userManager.GetUserAsync(User);

            if (userRole == UserRoles.GroupAdmin)
            {
                if (adminUser?.SalesGroupId == null || lead.SalesGroupId != adminUser.SalesGroupId) return Forbid();
            }
            else if (userRole == UserRoles.SalesOrgAdmin)
            {
                if (adminUser?.SalesOrgId == null || lead.AssignedTo == null || lead.AssignedTo.SalesOrgId != adminUser.SalesOrgId) return Forbid();
            }
            else if (userRole != UserRoles.OrganizationAdmin)
            {
                return Forbid();
            }

            // Helper query for reps
            IQueryable<ApplicationUser> GetRepsQuery()
            {
                var q = _userManager.Users.Where(u => u.IsActive && u.SalesGroupId == lead.SalesGroupId);
                if (userRole == UserRoles.SalesOrgAdmin)
                {
                    q = q.Where(u => u.SalesOrgId == adminUser.SalesOrgId);
                }
                return q.OrderBy(u => u.FirstName).ThenBy(u => u.LastName);
            }

            if (!ModelState.IsValid)
            {
                ViewBag.SalesReps = new SelectList(await GetRepsQuery().ToListAsync(), "Id", "FullName", model.NewAssigneeId);
                return View(model);
            }

            var newAssignee = await _userManager.FindByIdAsync(model.NewAssigneeId);
            bool isValidAssignee = newAssignee != null && newAssignee.IsActive && newAssignee.SalesGroupId == lead.SalesGroupId;
            
            if (isValidAssignee && userRole == UserRoles.SalesOrgAdmin)
            {
                if (newAssignee.SalesOrgId != adminUser.SalesOrgId) isValidAssignee = false;
            }

            if (!isValidAssignee)
            {
                ModelState.AddModelError(string.Empty, "Select a valid active sales rep in the lead's group/org.");
                ViewBag.SalesReps = new SelectList(await GetRepsQuery().ToListAsync(), "Id", "FullName", model.NewAssigneeId);
                return View(model);
            }

            // Apply reassignment
            lead.AssignedToId = newAssignee.Id;
            // Keep SalesGroupId unchanged (same group enforced above)
            var ok = await _leadService.UpdateAsync(lead);
            if (ok)
            {
                await _leadAuditService.LogAsync(lead, adminUser.Id, "Reassign", $"from={model.CurrentAssigneeId} to={model.NewAssigneeId}");

                // Notify newly assigned rep
                if (!string.IsNullOrEmpty(model.NewAssigneeId) && model.NewAssigneeId != adminUser.Id)
                {
                    await _notificationService.NotifyUserAsync(
                        model.NewAssigneeId,
                        "lead_assigned",
                        "Lead Reassigned to You",
                        $"The lead for {lead.Company} has been reassigned to you.",
                        $"/Leads/Details/{lead.Id}"
                    );
                }

                TempData["SuccessMessage"] = "Lead reassigned successfully.";
                return RedirectToAction(nameof(Details), new { id = lead.Id });
            }

            ModelState.AddModelError(string.Empty, "Failed to reassign lead.");
            ViewBag.SalesReps = new SelectList(await GetRepsQuery().ToListAsync(), "Id", "FullName", model.NewAssigneeId);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await PopulateDropdowns();
            var products = await _productService.GetAllAsync();
            ViewBag.Products = new Microsoft.AspNetCore.Mvc.Rendering.MultiSelectList(products, "Id", "Name");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var user = await _userManager.FindByIdAsync(userId);

            await PopulateAssignmentDropdowns(user);

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Lead lead, List<string>? productIds, string? assignedToId, string? salesGroupId, int? salesOrgId)
        {
            // Validate address via SmartyStreets when provided
            if (!string.IsNullOrWhiteSpace(lead.Address))
            {
                var validated = await _addressService.ValidateAsync(lead.Address, lead.City, lead.State, lead.ZipCode);
                if (validated != null)
                {
                    lead.Address = string.Join(" ", new[] { validated.Street1, validated.Street2 }.Where(s => !string.IsNullOrWhiteSpace(s)));
                    lead.City = validated.City;
                    lead.State = validated.State;
                    lead.ZipCode = string.IsNullOrWhiteSpace(validated.Zip4) ? validated.Zip5 : $"{validated.Zip5}-{validated.Zip4}";
                }
            }
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var user = await _userManager.FindByIdAsync(userId);

            if (User.IsInRole(UserRoles.OrganizationAdmin) || User.IsInRole(UserRoles.GroupAdmin) || User.IsInRole(UserRoles.SalesOrgAdmin))
            {
                if (string.IsNullOrWhiteSpace(assignedToId))
                {
                    ModelState.AddModelError("assignedToId", "Sales Rep is required.");
                }
            }

            if (!ModelState.IsValid)
            {
                // Log validation errors
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    Console.WriteLine($"Validation Error: {error.ErrorMessage}");
                }
                await PopulateDropdowns();
                var allProducts = await _productService.GetAllAsync();
                ViewBag.Products = new Microsoft.AspNetCore.Mvc.Rendering.MultiSelectList(allProducts, "Id", "Name", productIds);
                
                await PopulateAssignmentDropdowns(user, salesGroupId, salesOrgId, assignedToId);
                return View(lead);
            }

            // Determine intended assignment/group for duplicate validation
            ApplicationUser? assigneeUser = null;
            if (!string.IsNullOrWhiteSpace(assignedToId))
            {
                var candidate = await _userManager.FindByIdAsync(assignedToId);
                if (candidate != null && candidate.IsActive)
                {
                    // Validate permission to assign
                    bool canAssign = false;
                    if (User.IsInRole(UserRoles.OrganizationAdmin)) canAssign = true;
                    else if (User.IsInRole(UserRoles.GroupAdmin) && candidate.SalesGroupId == user?.SalesGroupId) canAssign = true;
                    else if (User.IsInRole(UserRoles.SalesOrgAdmin) && candidate.SalesGroupId == user?.SalesGroupId && candidate.SalesOrgId == user?.SalesOrgId) canAssign = true;

                    if (canAssign) assigneeUser = candidate;
                }
            }
            string? targetGroupId = assigneeUser?.SalesGroupId ?? user?.SalesGroupId;

            // Group-aware duplicate check: allow other groups to create if existing Lost belongs to different group
            if (!await _leadService.CanRegisterLeadForGroupAsync(lead.Company, targetGroupId, lead.Address, lead.City, lead.State, lead.ZipCode))
            {
                ModelState.AddModelError("", "A similar lead (by company or address) already exists and is active or within the cooling period. If the existing lead is Lost, only a different sales group may create a new one.");
                await PopulateDropdowns();
                var allProducts = await _productService.GetAllAsync();
                ViewBag.Products = new Microsoft.AspNetCore.Mvc.Rendering.MultiSelectList(allProducts, "Id", "Name", productIds);
                await PopulateAssignmentDropdowns(user, salesGroupId, salesOrgId, assignedToId);
                return View(lead);
            }

            lead.CreatedById = userId;

            if (assigneeUser != null)
            {
                lead.AssignedToId = assigneeUser.Id;
                lead.SalesGroupId = assigneeUser.SalesGroupId;
            }
            else
            {
                // Default to self
                lead.AssignedToId = userId;
                lead.SalesGroupId = user?.SalesGroupId;
            }

            // Attach selected products
            var selectedProducts = await _productService.GetByIdsAsync(productIds ?? new List<string>());
            lead.Products = selectedProducts;

            try
            {
                if (await _leadService.CreateAsync(lead))
                {
                    await _leadAuditService.LogAsync(lead, userId, "Create", $"AssignedTo={lead.AssignedToId};Group={lead.SalesGroupId}");

                    // Notify the assigned rep if it's someone else
                    if (!string.IsNullOrEmpty(lead.AssignedToId) && lead.AssignedToId != userId)
                    {
                        await _notificationService.NotifyUserAsync(
                            lead.AssignedToId,
                            "lead_assigned",
                            "New Lead Assigned to You",
                            $"You have been assigned a new lead: {lead.Company} ({lead.FirstName} {lead.LastName})",
                            $"/Leads/Details/{lead.Id}"
                        );
                    }

                    // Notify org admins of any new lead
                    await _notificationService.NotifyRoleAsync(
                        UserRoles.OrganizationAdmin,
                        "lead_created",
                        "New Lead Created",
                        $"A new lead was created: {lead.Company} ({lead.FirstName} {lead.LastName})",
                        $"/Leads/Details/{lead.Id}"
                    );

                    TempData["SuccessMessage"] = "Lead created successfully!";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    ModelState.AddModelError("", "Failed to create lead. Please try again or contact support if the issue persists.");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Failed to create lead: {ex.Message}");
                if (ex.InnerException != null)
                {
                    ModelState.AddModelError("", $"Details: {ex.InnerException.Message}");
                }
            }
            await PopulateDropdowns();
            var products2 = await _productService.GetAllAsync();
            ViewBag.Products = new Microsoft.AspNetCore.Mvc.Rendering.MultiSelectList(products2, "Id", "Name", productIds);
            await PopulateAssignmentDropdowns(user, salesGroupId, salesOrgId, assignedToId);
            return View(lead);
        }

        [HttpGet]
        public async Task<IActionResult> AddressAutocomplete(string search, string? city, string? state)
        {
            var items = await _addressService.AutocompleteAsync(search ?? string.Empty, city, state);
            return Json(items);
        }

        [HttpGet]
        public async Task<IActionResult> ValidateAddressApi(string street, string? city, string? state, string? zip)
        {
            var v = await _addressService.ValidateAsync(street, city, state, zip);
            return Json(v);
        }

        [HttpGet]
        public async Task<IActionResult> CheckDuplicate(string? company, string? address, string? city, string? state, string? zip)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var user = await _userManager.FindByIdAsync(userId);
            var salesGroupId = user?.SalesGroupId;
            var allowed = await _leadService.CanRegisterLeadForGroupAsync(company, salesGroupId, address, city, state, zip);
            return Json(new { duplicate = !allowed });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var lead = await _leadService.GetByIdAsync(id);
            if (lead == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "";

            if (!await CanEditLead(lead, userId, userRole))
            {
                return Forbid();
            }

            if (lead.Status == LeadStatus.Converted)
            {
                TempData["ErrorMessage"] = "Converted leads cannot be edited.";
                return RedirectToAction(nameof(Details), new { id = lead.Id });
            }

            if (lead.Status == LeadStatus.Lost)
            {
                TempData["ErrorMessage"] = "Lost leads cannot be edited.";
                return RedirectToAction(nameof(Details), new { id = lead.Id });
            }

            if (lead.Status == LeadStatus.Expired)
            {
                TempData["ErrorMessage"] = "Expired leads cannot be edited. Please grant an extension first.";
                return RedirectToAction(nameof(Details), new { id = lead.Id });
            }

            await PopulateDropdowns();
            var allProducts = await _productService.GetAllAsync();
            var selectedIds = lead.Products?.Select(p => p.Id) ?? Enumerable.Empty<string>();
            ViewBag.Products = new Microsoft.AspNetCore.Mvc.Rendering.MultiSelectList(allProducts, "Id", "Name", selectedIds);

            // Populate Sales Group/Org/Rep dropdowns
            var currentUser = await _userManager.FindByIdAsync(userId);
            
            // Determine current assignment details
            string? currentGroupId = lead.SalesGroupId;
            int? currentOrgId = null;
            string? currentRepId = lead.AssignedToId;

            if (!string.IsNullOrEmpty(currentRepId))
            {
                var assignedUser = await _userManager.FindByIdAsync(currentRepId);
                if (assignedUser != null)
                {
                    currentGroupId = assignedUser.SalesGroupId;
                    currentOrgId = assignedUser.SalesOrgId;
                }
            }
            
            // Fallback if lead has no group (shouldn't happen usually)
            if (string.IsNullOrEmpty(currentGroupId)) currentGroupId = currentUser?.SalesGroupId;

            await PopulateAssignmentDropdowns(currentUser, currentGroupId, currentOrgId, currentRepId);

            return View(lead);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, Lead lead, List<string>? productIds)
        {
            if (id != lead.Id)
            {
                return NotFound();
            }

            var existingLead = await _leadService.GetByIdAsync(id);
            if (existingLead == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "";

            if (!await CanEditLead(existingLead, userId, userRole))
            {
                return Forbid();
            }

            if (existingLead.Status == LeadStatus.Converted)
            {
                TempData["ErrorMessage"] = "Converted leads cannot be edited.";
                return RedirectToAction(nameof(Details), new { id = existingLead.Id });
            }

            if (existingLead.Status == LeadStatus.Lost)
            {
                TempData["ErrorMessage"] = "Lost leads cannot be edited.";
                return RedirectToAction(nameof(Details), new { id = existingLead.Id });
            }

            if (existingLead.Status == LeadStatus.Expired)
            {
                TempData["ErrorMessage"] = "Expired leads cannot be edited. Please grant an extension first.";
                return RedirectToAction(nameof(Details), new { id = existingLead.Id });
            }

            // Business rule: Lost leads cannot be reassigned within same sales group
            if (existingLead.Status == LeadStatus.Lost && existingLead.SalesGroupId == lead.SalesGroupId && existingLead.AssignedToId != lead.AssignedToId)
            {
                ModelState.AddModelError("", "Lost leads cannot be reassigned within the same sales group.");
            }

            // Business rule: Only Organization Admin can change company name
            if (userRole != UserRoles.OrganizationAdmin &&
                !string.Equals(existingLead.Company ?? string.Empty, lead.Company ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("", "Only Organization Admin can change the company name.");
            }

            // Business rule: Sales Org Admin and Group Admin can only edit assignment details
            if (userRole == UserRoles.SalesOrgAdmin || userRole == UserRoles.GroupAdmin)
            {
                // Revert restricted fields
                lead.FirstName = existingLead.FirstName;
                lead.LastName = existingLead.LastName;
                lead.Email = existingLead.Email;
                lead.Phone = existingLead.Phone;
                lead.Company = existingLead.Company;
                lead.Address = existingLead.Address;
                lead.City = existingLead.City;
                lead.State = existingLead.State;
                lead.ZipCode = existingLead.ZipCode;
                lead.Notes = existingLead.Notes;
                
                // Keep existing products
                productIds = existingLead.Products?.Select(p => p.Id).ToList();

                // Group Admin and Sales Org Admin cannot change Sales Group (disabled in UI)
                lead.SalesGroupId = existingLead.SalesGroupId;

                // Sales Org Admin cannot change Sales Org
                if (userRole == UserRoles.SalesOrgAdmin)
                {
                    lead.SalesOrgId = existingLead.SalesOrgId;
                }
            }

            if (ModelState.IsValid)
            {
                // Update products selection
                var selectedProducts = await _productService.GetByIdsAsync(productIds ?? new List<string>());
                lead.Products = selectedProducts;

                // Check for duplicates by company or address when either changed
                var companyChanged = !string.Equals(existingLead.Company ?? string.Empty, lead.Company ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                var addressChanged = !string.Equals(existingLead.Address ?? string.Empty, lead.Address ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(existingLead.City ?? string.Empty, lead.City ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(existingLead.State ?? string.Empty, lead.State ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(existingLead.ZipCode ?? string.Empty, lead.ZipCode ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                if (companyChanged || addressChanged)
                {
                    // Only check the fields that have actually changed
                    var checkCompany = companyChanged ? lead.Company : null;
                    var checkAddress = addressChanged ? lead.Address : null;
                    var checkCity = addressChanged ? lead.City : null;
                    var checkState = addressChanged ? lead.State : null;
                    var checkZip = addressChanged ? lead.ZipCode : null;

                    var canRegister = await _leadService.CanRegisterLeadAsync(checkCompany, checkAddress, checkCity, checkState, checkZip, lead.Id);
                    if (!canRegister)
                    {
                        ModelState.AddModelError("", "A similar lead (by company or address) already exists and is active or within the cooling period.");
                        await PopulateDropdowns();
                        var allProducts = await _productService.GetAllAsync();
                        ViewBag.Products = new Microsoft.AspNetCore.Mvc.Rendering.MultiSelectList(allProducts, "Id", "Name", productIds);
                        return View(lead);
                    }
                }

                // Preserve original values
                lead.CreatedById = existingLead.CreatedById;
                lead.CreatedDate = existingLead.CreatedDate;
                lead.ExpiryDate = existingLead.ExpiryDate;
                lead.IsExtended = existingLead.IsExtended;
                lead.ExtensionGrantedDate = existingLead.ExtensionGrantedDate;
                lead.ExtensionGrantedBy = existingLead.ExtensionGrantedBy;

                // Only Organization Admin can change status
                if (userRole != UserRoles.OrganizationAdmin)
                {
                    lead.Status = existingLead.Status;
                }

                // If Org Admin sets status to Converted during edit, run full conversion flow
                var isOrgAdmin = userRole == UserRoles.OrganizationAdmin;
                var wantsConversion = isOrgAdmin && lead.Status == LeadStatus.Converted && existingLead.Status != LeadStatus.Converted;

                if (wantsConversion)
                {
                    // First persist other field changes without changing status
                    lead.Status = existingLead.Status;
                    var updated = await _leadService.UpdateAsync(lead);
                    if (!updated)
                    {
                        ModelState.AddModelError("", "Failed to update lead before conversion.");
                    }
                    else
                    {
                        var converted = await _leadService.ConvertToCustomerAsync(id, userId);
                        if (converted)
                        {
                            await _leadAuditService.LogAsync(existingLead, userId, "Convert", "Converted to customer");
                            TempData["SuccessMessage"] = "Lead converted to customer successfully!";
                            return RedirectToAction("Index", "Customers");
                        }
                        ModelState.AddModelError("", "Failed to convert lead. It may be Lost, Expired, or already Converted.");
                    }
                }

                if (await _leadService.UpdateAsync(lead))
                {
                    var changes = new List<string>();
                    void add(string name, string? oldVal, string? newVal)
                    {
                        if (!string.Equals(oldVal ?? string.Empty, newVal ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                            changes.Add($"{name}:{oldVal}=>{newVal}");
                    }
                    add("Company", existingLead.Company, lead.Company);
                    add("Address", existingLead.Address, lead.Address);
                    add("City", existingLead.City, lead.City);
                    add("State", existingLead.State, lead.State);
                    add("Zip", existingLead.ZipCode, lead.ZipCode);
                    add("Phone", existingLead.Phone, lead.Phone);
                    add("Email", existingLead.Email, lead.Email);
                    add("AssignedToId", existingLead.AssignedToId, lead.AssignedToId);
                    add("SalesGroupId", existingLead.SalesGroupId, lead.SalesGroupId);
                    add("Status", existingLead.Status.ToString(), lead.Status.ToString());
                    add("Notes", existingLead.Notes, lead.Notes);
                    var detail = changes.Count > 0 ? string.Join("; ", changes) : "No field changes";
                    await _leadAuditService.LogAsync(lead, userId, "Update", detail);
                    TempData["SuccessMessage"] = "Lead updated successfully!";
                    return RedirectToAction(nameof(Details), new { id = lead.Id });
                }

                ModelState.AddModelError("", "Failed to update lead.");
            }

            await PopulateDropdowns();
            var productsList = await _productService.GetAllAsync();
            ViewBag.Products = new Microsoft.AspNetCore.Mvc.Rendering.MultiSelectList(productsList, "Id", "Name", productIds);
            return View(lead);
        }

        [HttpPost]
        [Authorize(Roles = "OrganizationAdmin")]
        public async Task<IActionResult> GrantExtension(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var extLead = await _leadService.GetByIdAsync(id);

            if (await _leadService.GrantExtensionAsync(id, userId))
            {
                await _leadAuditService.LogAsync(id, userId, "GrantExtension", "Extension granted");

                // Notify the assigned rep
                if (extLead != null && !string.IsNullOrEmpty(extLead.AssignedToId) && extLead.AssignedToId != userId)
                {
                    await _notificationService.NotifyUserAsync(
                        extLead.AssignedToId,
                        "lead_extended",
                        "Lead Extension Granted",
                        $"An extension was granted for your lead: {extLead.Company}.",
                        $"/Leads/Details/{id}"
                    );
                }

                TempData["SuccessMessage"] = "Extension granted successfully!";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to grant extension. Lead may already have an extension or is Lost/Converted.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "OrganizationAdmin")]
        public async Task<IActionResult> Convert(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var convLead = await _leadService.GetByIdAsync(id);

            if (await _leadService.ConvertToCustomerAsync(id, userId))
            {
                await _leadAuditService.LogAsync(id, userId, "Convert", "Converted to customer");

                // Notify the assigned rep
                if (convLead != null && !string.IsNullOrEmpty(convLead.AssignedToId) && convLead.AssignedToId != userId)
                {
                    await _notificationService.NotifyUserAsync(
                        convLead.AssignedToId,
                        "lead_converted",
                        "Lead Converted to Customer!",
                        $"Your lead {convLead.Company} has been converted to a customer.",
                        "/Customers"
                    );
                }

                TempData["SuccessMessage"] = "Lead converted to customer successfully!";
                return RedirectToAction("Index", "Customers");
            }

            TempData["ErrorMessage"] = "Failed to convert lead. It may be Lost, Expired, or already Converted.";
            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task<bool> CanAccessLead(Lead lead, string userId, string userRole)
        {
            if (userRole == UserRoles.OrganizationAdmin)
                return true;

            if (userRole == UserRoles.SalesOrgAdmin)
            {
                var user = await _userManager.FindByIdAsync(userId);
                return user?.SalesOrgId != null && lead.AssignedTo != null && lead.AssignedTo.SalesOrgId == user.SalesOrgId;
            }

            if (userRole == UserRoles.GroupAdmin)
            {
                var user = await _userManager.FindByIdAsync(userId);
                return user?.SalesGroupId == lead.SalesGroupId;
            }

            if (userRole == UserRoles.SalesRep)
            {
                return lead.AssignedToId == userId;
            }

            return false;
        }

        private async Task<bool> CanEditLead(Lead lead, string userId, string userRole)
        {
            if (userRole == UserRoles.OrganizationAdmin)
                return true;

            if (userRole == UserRoles.SalesOrgAdmin)
            {
                var user = await _userManager.FindByIdAsync(userId);
                // Sales org admin can edit only within their org
                return user?.SalesOrgId != null && lead.AssignedTo != null && lead.AssignedTo.SalesOrgId == user.SalesOrgId;
            }

            if (userRole == UserRoles.GroupAdmin)
            {
                var user = await _userManager.FindByIdAsync(userId);
                // Group admin can edit leads within their group
                return user?.SalesGroupId == lead.SalesGroupId;
            }

            if (userRole == UserRoles.SalesRep)
            {
                return lead.AssignedToId == userId;
            }

            return false;
        }

        private async Task PopulateDropdowns()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var user = await _userManager.FindByIdAsync(userId);
            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "";

            if (userRole == UserRoles.OrganizationAdmin || userRole == UserRoles.GroupAdmin)
            {
                if (userRole == UserRoles.OrganizationAdmin)
                {
                    // Handled in Create GET; keep here if needed for other actions
                }
                else if (user?.SalesGroupId != null)
                {
                    var groupMembers = await _salesGroupService.GetGroupMembersAsync(user.SalesGroupId);
                    ViewBag.SalesReps = new SelectList(groupMembers, "Id", "FullName");
                }
            }

            var statuses = Enum.GetValues(typeof(LeadStatus)).Cast<LeadStatus>()
                .Where(s => s != LeadStatus.Converted);
            ViewBag.Statuses = new SelectList(statuses);
        }

        private async Task PopulateAssignmentDropdowns(ApplicationUser user, string? selectedGroupId = null, int? selectedOrgId = null, string? selectedRepId = null)
        {
            // Default to user's context if not provided
            if (string.IsNullOrEmpty(selectedGroupId)) selectedGroupId = user.SalesGroupId;
            if (!selectedOrgId.HasValue) selectedOrgId = user.SalesOrgId;

            if (User.IsInRole(UserRoles.OrganizationAdmin))
            {
                var groups = await _salesGroupService.GetAllAsync();
                ViewBag.SalesGroups = new SelectList(groups, "Id", "Name", selectedGroupId);
                
                if (!string.IsNullOrEmpty(selectedGroupId))
                {
                    var orgs = await _salesOrgService.GetByGroupIdAsync(selectedGroupId);
                    ViewBag.SalesOrgs = new SelectList(orgs, "Id", "Name", selectedOrgId);
                }
                else
                {
                    ViewBag.SalesOrgs = new SelectList(Enumerable.Empty<SalesOrg>(), "Id", "Name");
                }

                if (selectedOrgId.HasValue)
                {
                    var reps = await _userManager.Users
                        .Where(u => u.SalesOrgId == selectedOrgId && u.IsActive)
                        .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                        .ToListAsync();
                    ViewBag.SalesReps = new SelectList(reps, "Id", "FullName", selectedRepId);
                }
                else
                {
                    ViewBag.SalesReps = new SelectList(Enumerable.Empty<ApplicationUser>(), "Id", "FullName");
                }
            }
            else if (User.IsInRole(UserRoles.GroupAdmin))
            {
                if (user.SalesGroupId != null)
                {
                    var group = await _salesGroupService.GetByIdAsync(user.SalesGroupId);
                    ViewBag.SalesGroups = new SelectList(new[] { group }, "Id", "Name", user.SalesGroupId);
                    
                    var orgs = await _salesOrgService.GetByGroupIdAsync(user.SalesGroupId);
                    ViewBag.SalesOrgs = new SelectList(orgs, "Id", "Name", selectedOrgId);

                    if (selectedOrgId.HasValue)
                    {
                        var reps = await _userManager.Users
                            .Where(u => u.SalesOrgId == selectedOrgId && u.IsActive)
                            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                            .ToListAsync();
                        ViewBag.SalesReps = new SelectList(reps, "Id", "FullName", selectedRepId);
                    }
                    else
                    {
                        ViewBag.SalesReps = new SelectList(Enumerable.Empty<ApplicationUser>(), "Id", "FullName");
                    }
                }
            }
            else if (User.IsInRole(UserRoles.SalesOrgAdmin))
            {
                if (user.SalesGroupId != null && user.SalesOrgId != null)
                {
                    var group = await _salesGroupService.GetByIdAsync(user.SalesGroupId);
                    ViewBag.SalesGroups = new SelectList(new[] { group }, "Id", "Name", user.SalesGroupId);

                    var org = await _salesOrgService.GetByIdAsync(user.SalesOrgId.Value);
                    ViewBag.SalesOrgs = new SelectList(new[] { org }, "Id", "Name", user.SalesOrgId);

                    var reps = await _userManager.Users
                        .Where(u => u.SalesOrgId == user.SalesOrgId && u.IsActive)
                        .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                        .ToListAsync();
                    ViewBag.SalesReps = new SelectList(reps, "Id", "FullName", selectedRepId);
                }
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSalesOrgs(string groupId)
        {
            if (string.IsNullOrEmpty(groupId)) return Json(new List<object>());
            
            // Security: Group Admin can only see their own group's orgs
            if (User.IsInRole(UserRoles.GroupAdmin))
            {
                var user = await _userManager.GetUserAsync(User);
                if (user?.SalesGroupId != groupId) return Forbid();
            }

            var orgs = await _salesOrgService.GetByGroupIdAsync(groupId);
            return Json(orgs.Select(o => new { id = o.Id, name = o.Name }));
        }

        [HttpGet]
        public async Task<IActionResult> GetSalesReps(int orgId)
        {
            // Security: Org Admin can only see their own org's reps
            if (User.IsInRole(UserRoles.SalesOrgAdmin))
            {
                var user = await _userManager.GetUserAsync(User);
                if (user?.SalesOrgId != orgId) return Forbid();
            }
             // Security: Group Admin can only see reps in their group
            if (User.IsInRole(UserRoles.GroupAdmin))
            {
                var user = await _userManager.GetUserAsync(User);
                // We need to check if the requested orgId belongs to the user's group
                var org = await _salesOrgService.GetByIdAsync(orgId);
                if (org == null || org.SalesGroupId != user?.SalesGroupId) return Forbid();
            }

            var reps = await _userManager.Users
                .Where(u => u.SalesOrgId == orgId && u.IsActive)
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .ToListAsync();
            
            return Json(reps.Select(u => new { id = u.Id, fullName = u.FullName }));
        }
    }

    public class LeadReassignViewModel
    {
        [Required]
        public string LeadId { get; set; } = string.Empty;

        public string? CurrentAssigneeId { get; set; }

        [Required]
        [Display(Name = "Assign to")]
        public string NewAssigneeId { get; set; } = string.Empty;
    }
}
