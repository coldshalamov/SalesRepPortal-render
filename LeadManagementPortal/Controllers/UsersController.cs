using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace LeadManagementPortal.Controllers
{
    [Authorize]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public UsersController(
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        [Authorize(Roles = LeadManagementPortal.Models.UserRoles.OrganizationAdmin + "," + LeadManagementPortal.Models.UserRoles.SalesOrgAdmin)]
        public async Task<IActionResult> Index()
        {
            var query = _userManager.Users
                .Include(u => u.SalesGroup)
                .Include(u => u.SalesOrg)
                .AsQueryable();

            if (User.IsInRole(UserRoles.SalesOrgAdmin))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser?.SalesOrgId != null)
                {
                    query = query.Where(u => u.SalesOrgId == currentUser.SalesOrgId);
                }
            }

            var users = await query.ToListAsync();
            return View(users);
        }

        [HttpGet]
        [Authorize(Roles = LeadManagementPortal.Models.UserRoles.OrganizationAdmin + "," + LeadManagementPortal.Models.UserRoles.GroupAdmin + "," + LeadManagementPortal.Models.UserRoles.SalesOrgAdmin)]
        public async Task<IActionResult> Create()
        {
            if (User.IsInRole(UserRoles.GroupAdmin))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var group = currentUser?.SalesGroupId == null ? null : await _context.SalesGroups.FirstOrDefaultAsync(g => g.Id == currentUser.SalesGroupId);
                ViewBag.Roles = new SelectList(new[] { UserRoles.SalesRep });
                if (group != null)
                {
                    ViewBag.SalesGroups = new SelectList(new[] { group }, "Id", "Name", group.Id);
                    ViewBag.SalesOrgs = new SelectList(await _context.SalesOrgs.Where(o => o.SalesGroupId == group.Id).ToListAsync(), "Id", "Name");
                    ViewBag.LockToGroup = true;
                    ViewBag.LockToOrg = false;
                }
                else
                {
                    ViewBag.SalesGroups = new SelectList(Enumerable.Empty<SelectListItem>());
                    ViewBag.SalesOrgs = new SelectList(Enumerable.Empty<SelectListItem>());
                    ViewBag.LockToGroup = true;
                    ViewBag.LockToOrg = false;
                }
            }
            else if (User.IsInRole(UserRoles.SalesOrgAdmin))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                ViewBag.Roles = new SelectList(new[] { UserRoles.SalesRep });
                if (currentUser?.SalesOrgId != null)
                {
                    var org = await _context.SalesOrgs.FirstOrDefaultAsync(o => o.Id == currentUser.SalesOrgId.Value);
                    if (org != null)
                    {
                        var group = await _context.SalesGroups.FirstOrDefaultAsync(g => g.Id == org.SalesGroupId);
                        if (group != null)
                        {
                            ViewBag.SalesGroups = new SelectList(new[] { group }, "Id", "Name", group.Id);
                        }
                        else
                        {
                            ViewBag.SalesGroups = new SelectList(Enumerable.Empty<SelectListItem>());
                        }
                        ViewBag.SalesOrgs = new SelectList(new[] { org }, "Id", "Name", org.Id);
                        ViewBag.LockToGroup = true;
                        ViewBag.LockToOrg = true;
                    }
                    else
                    {
                        ViewBag.SalesGroups = new SelectList(Enumerable.Empty<SelectListItem>());
                        ViewBag.SalesOrgs = new SelectList(Enumerable.Empty<SelectListItem>());
                        ViewBag.LockToGroup = true;
                        ViewBag.LockToOrg = true;
                    }
                }
                else
                {
                    ViewBag.SalesGroups = new SelectList(Enumerable.Empty<SelectListItem>());
                    ViewBag.SalesOrgs = new SelectList(Enumerable.Empty<SelectListItem>());
                    ViewBag.LockToGroup = true;
                    ViewBag.LockToOrg = true;
                }
            }
            else
            {
                ViewBag.Roles = new SelectList(GetRolesAsync(), "Name", "Name");
                ViewBag.SalesGroups = new SelectList(await _context.SalesGroups.Where(g => g.IsActive).ToListAsync(), "Id", "Name");
                ViewBag.SalesOrgs = new SelectList(Enumerable.Empty<SelectListItem>());
                ViewBag.LockToGroup = false;
                ViewBag.LockToOrg = false;
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = LeadManagementPortal.Models.UserRoles.OrganizationAdmin + "," + LeadManagementPortal.Models.UserRoles.GroupAdmin + "," + LeadManagementPortal.Models.UserRoles.SalesOrgAdmin)]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                if (User.IsInRole(UserRoles.GroupAdmin))
                {
                    // Force role and group to current Group Admin's group and SalesRep role
                    model.Role = UserRoles.SalesRep;
                    var currentUser = await _userManager.GetUserAsync(User);
                    if (currentUser?.SalesGroupId == null)
                    {
                        ModelState.AddModelError(string.Empty, "You are not assigned to a sales group. Contact an Organization Admin.");
                    }
                    else
                    {
                        model.SalesGroupId = currentUser.SalesGroupId;
                    }
                }
                else if (User.IsInRole(UserRoles.SalesOrgAdmin))
                {
                    // Force role, group, and org to current Sales Org Admin's org
                    model.Role = UserRoles.SalesRep;
                    var currentUser = await _userManager.GetUserAsync(User);
                    if (currentUser?.SalesOrgId == null)
                    {
                        ModelState.AddModelError(string.Empty, "You are not assigned to a sales organization. Contact an Organization Admin.");
                    }
                    else
                    {
                        var org = await _context.SalesOrgs.FirstOrDefaultAsync(o => o.Id == currentUser.SalesOrgId.Value);
                        if (org == null)
                        {
                            ModelState.AddModelError(string.Empty, "Your assigned sales organization no longer exists.");
                        }
                        else
                        {
                            model.SalesGroupId = org.SalesGroupId;
                            model.SalesOrgId = org.Id;
                        }
                    }
                }

                // Enforce mandatory fields based on Role
                if (model.Role == UserRoles.GroupAdmin)
                {
                    if (string.IsNullOrEmpty(model.SalesGroupId))
                    {
                        ModelState.AddModelError(nameof(model.SalesGroupId), "Sales Group is required for Group Admins.");
                    }
                }
                else if (model.Role == UserRoles.SalesOrgAdmin || model.Role == UserRoles.SalesRep)
                {
                    if (string.IsNullOrEmpty(model.SalesGroupId))
                    {
                        ModelState.AddModelError(nameof(model.SalesGroupId), "Sales Group is required.");
                    }
                    if (!model.SalesOrgId.HasValue)
                    {
                        ModelState.AddModelError(nameof(model.SalesOrgId), "Sales Org is required.");
                    }
                }

                // Validate SalesOrg belongs to selected SalesGroup (if provided)
                if (model.SalesOrgId.HasValue)
                {
                    var org = await _context.SalesOrgs.FirstOrDefaultAsync(o => o.Id == model.SalesOrgId.Value);
                    if (org == null)
                    {
                        ModelState.AddModelError(nameof(model.SalesOrgId), "Selected Sales Org does not exist.");
                    }
                    else if (!string.IsNullOrEmpty(model.SalesGroupId) && org.SalesGroupId != model.SalesGroupId)
                    {
                        ModelState.AddModelError(nameof(model.SalesOrgId), "Selected Sales Org does not belong to the chosen Sales Group.");
                    }
                }

                // If validation errors were added above, stop and return the view
                if (!ModelState.IsValid)
                {
                    if (User.IsInRole(UserRoles.GroupAdmin))
                    {
                        ViewBag.Roles = new SelectList(new[] { UserRoles.SalesRep }, model.Role);
                        var currentUser = await _userManager.GetUserAsync(User);
                        var group = currentUser?.SalesGroupId == null ? null : await _context.SalesGroups.FirstOrDefaultAsync(g => g.Id == currentUser.SalesGroupId);
                        if (group != null)
                        {
                            ViewBag.SalesGroups = new SelectList(new[] { group }, "Id", "Name", group.Id);
                            ViewBag.SalesOrgs = new SelectList(await _context.SalesOrgs.Where(o => o.SalesGroupId == group.Id).ToListAsync(), "Id", "Name", model.SalesOrgId);
                        }
                        else
                        {
                            ViewBag.SalesGroups = new SelectList(Enumerable.Empty<SelectListItem>());
                            ViewBag.SalesOrgs = new SelectList(Enumerable.Empty<SelectListItem>());
                        }
                        ViewBag.LockToGroup = true;
                    }
                    else
                    {
                        ViewBag.Roles = new SelectList(GetRolesAsync(), "Name", "Name", model.Role);
                        ViewBag.SalesGroups = new SelectList(await _context.SalesGroups.Where(g => g.IsActive).ToListAsync(), "Id", "Name", model.SalesGroupId);
                        var orgs = string.IsNullOrEmpty(model.SalesGroupId) ? new List<SalesOrg>() : await _context.SalesOrgs.Where(o => o.SalesGroupId == model.SalesGroupId).ToListAsync();
                        ViewBag.SalesOrgs = new SelectList(orgs, "Id", "Name", model.SalesOrgId);
                        ViewBag.LockToGroup = false;
                    }
                    return View(model);
                }

                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    EmailConfirmed = true,
                    IsActive = true,
                    SalesGroupId = model.SalesGroupId,
                    SalesOrgId = model.SalesOrgId
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, model.Role);
                    TempData["SuccessMessage"] = "User created successfully!";
                    if (User.IsInRole(UserRoles.GroupAdmin))
                    {
                        return RedirectToAction(nameof(MyGroup));
                    }
                    return RedirectToAction(nameof(Index));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            if (User.IsInRole(UserRoles.GroupAdmin))
            {
                ViewBag.Roles = new SelectList(new[] { UserRoles.SalesRep });
                var currentUser = await _userManager.GetUserAsync(User);
                var group = currentUser?.SalesGroupId == null ? null : await _context.SalesGroups.FirstOrDefaultAsync(g => g.Id == currentUser.SalesGroupId);
                if (group != null)
                {
                    ViewBag.SalesGroups = new SelectList(new[] { group }, "Id", "Name", group.Id);
                    ViewBag.SalesOrgs = new SelectList(await _context.SalesOrgs.Where(o => o.SalesGroupId == group.Id).ToListAsync(), "Id", "Name", model.SalesOrgId);
                    ViewBag.LockToGroup = true;
                    ViewBag.LockToOrg = false;
                }
                else
                {
                    ViewBag.SalesGroups = new SelectList(Enumerable.Empty<SelectListItem>());
                    ViewBag.SalesOrgs = new SelectList(Enumerable.Empty<SelectListItem>());
                    ViewBag.LockToGroup = true;
                    ViewBag.LockToOrg = false;
                }
            }
            else if (User.IsInRole(UserRoles.SalesOrgAdmin))
            {
                ViewBag.Roles = new SelectList(new[] { UserRoles.SalesRep });
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser?.SalesOrgId != null)
                {
                    var org = await _context.SalesOrgs.FirstOrDefaultAsync(o => o.Id == currentUser.SalesOrgId.Value);
                    if (org != null)
                    {
                        var group = await _context.SalesGroups.FirstOrDefaultAsync(g => g.Id == org.SalesGroupId);
                        if (group != null)
                        {
                            ViewBag.SalesGroups = new SelectList(new[] { group }, "Id", "Name", group.Id);
                        }
                        else
                        {
                            ViewBag.SalesGroups = new SelectList(Enumerable.Empty<SelectListItem>());
                        }
                        ViewBag.SalesOrgs = new SelectList(new[] { org }, "Id", "Name", org.Id);
                        ViewBag.LockToGroup = true;
                        ViewBag.LockToOrg = true;
                    }
                    else
                    {
                        ViewBag.SalesGroups = new SelectList(Enumerable.Empty<SelectListItem>());
                        ViewBag.SalesOrgs = new SelectList(Enumerable.Empty<SelectListItem>());
                        ViewBag.LockToGroup = true;
                        ViewBag.LockToOrg = true;
                    }
                }
                else
                {
                    ViewBag.SalesGroups = new SelectList(Enumerable.Empty<SelectListItem>());
                    ViewBag.SalesOrgs = new SelectList(Enumerable.Empty<SelectListItem>());
                    ViewBag.LockToGroup = true;
                    ViewBag.LockToOrg = true;
                }
            }
            else
            {
                ViewBag.Roles = new SelectList(GetRolesAsync(), "Name", "Name");
                ViewBag.SalesGroups = new SelectList(await _context.SalesGroups.Where(g => g.IsActive).ToListAsync(), "Id", "Name");
                var orgs = string.IsNullOrEmpty(model.SalesGroupId) ? new List<SalesOrg>() : await _context.SalesOrgs.Where(o => o.SalesGroupId == model.SalesGroupId).ToListAsync();
                ViewBag.SalesOrgs = new SelectList(orgs, "Id", "Name", model.SalesOrgId);
                ViewBag.LockToGroup = false;
                ViewBag.LockToOrg = false;
            }
            return View(model);
        }

        [HttpGet]
        [Authorize(Roles = LeadManagementPortal.Models.UserRoles.OrganizationAdmin + "," + LeadManagementPortal.Models.UserRoles.GroupAdmin + "," + LeadManagementPortal.Models.UserRoles.SalesOrgAdmin)]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userManager.Users
                .Include(u => u.SalesGroup)
                .Include(u => u.SalesOrg)
                .Include(u => u.CommissionDeal)
                .Include(u => u.SponsorLink)
                .FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return NotFound();
            }

            // If Group Admin, ensure they can only edit users in their own group and lock role/group
            bool lockToGroup = false;
            bool lockToOrg = false;
            if (User.IsInRole(UserRoles.GroupAdmin))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser?.SalesGroupId == null)
                {
                    return Forbid();
                }

                // Group admins can only edit users within their own group
                if (user.SalesGroupId != currentUser.SalesGroupId)
                {
                    return Forbid();
                }

                lockToGroup = true;
            }
            else if (User.IsInRole(UserRoles.SalesOrgAdmin))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser?.SalesOrgId == null)
                {
                    return Forbid();
                }

                // Sales org admins can only edit users within their own org
                if (user.SalesOrgId != currentUser.SalesOrgId)
                {
                    return Forbid();
                }

                lockToGroup = true;
                lockToOrg = true;
            }

            var roles = await _userManager.GetRolesAsync(user);
            var model = new EditUserViewModel
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email ?? string.Empty,
                Role = roles.FirstOrDefault() ?? string.Empty,
                SalesGroupId = user.SalesGroupId,
                SalesOrgId = user.SalesOrgId,
                IsActive = user.IsActive,
                SponsorId = user.SponsorLink?.SponsorId,
                CommissionDealType = user.CommissionDeal?.DealType,
                CommissionRate = user.CommissionDeal?.Rate,
                CommissionBaseCost = user.CommissionDeal?.BaseCost,
                CommissionCalculationBasis = user.CommissionDeal?.CalculationBasis
            };

            if (User.IsInRole(UserRoles.GroupAdmin))
            {
                // Lock role to SalesRep and group to the admin's group
                ViewBag.Roles = new SelectList(new[] { UserRoles.SalesRep }, model.Role);
                var currentUser = await _userManager.GetUserAsync(User);
                var group = currentUser?.SalesGroupId == null ? null : await _context.SalesGroups.FirstOrDefaultAsync(g => g.Id == currentUser.SalesGroupId);
                if (group != null)
                {
                    ViewBag.SalesGroups = new SelectList(new[] { group }, "Id", "Name", group.Id);
                    ViewBag.SalesOrgs = new SelectList(await _context.SalesOrgs.Where(o => o.SalesGroupId == group.Id).ToListAsync(), "Id", "Name", model.SalesOrgId);
                }
                else
                {
                    ViewBag.SalesGroups = new SelectList(Enumerable.Empty<SelectListItem>());
                    ViewBag.SalesOrgs = new SelectList(Enumerable.Empty<SelectListItem>());
                }
                ViewBag.LockToGroup = lockToGroup;
                ViewBag.LockToOrg = false;
            }
            else if (User.IsInRole(UserRoles.SalesOrgAdmin))
            {
                // Lock role to SalesRep and both group and org to the admin's org
                ViewBag.Roles = new SelectList(new[] { UserRoles.SalesRep }, model.Role);
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser?.SalesOrgId != null)
                {
                    var org = await _context.SalesOrgs.FirstOrDefaultAsync(o => o.Id == currentUser.SalesOrgId.Value);
                    if (org != null)
                    {
                        var group = await _context.SalesGroups.FirstOrDefaultAsync(g => g.Id == org.SalesGroupId);
                        if (group != null)
                        {
                            ViewBag.SalesGroups = new SelectList(new[] { group }, "Id", "Name", group.Id);
                        }
                        else
                        {
                            ViewBag.SalesGroups = new SelectList(Enumerable.Empty<SelectListItem>());
                        }
                        ViewBag.SalesOrgs = new SelectList(new[] { org }, "Id", "Name", model.SalesOrgId);
                    }
                    else
                    {
                        ViewBag.SalesGroups = new SelectList(Enumerable.Empty<SelectListItem>());
                        ViewBag.SalesOrgs = new SelectList(Enumerable.Empty<SelectListItem>());
                    }
                }
                else
                {
                    ViewBag.SalesGroups = new SelectList(Enumerable.Empty<SelectListItem>());
                    ViewBag.SalesOrgs = new SelectList(Enumerable.Empty<SelectListItem>());
                }
                ViewBag.LockToGroup = lockToGroup;
                ViewBag.LockToOrg = lockToOrg;
            }
            else
            {
                ViewBag.Roles = new SelectList(GetRolesAsync(), "Name", "Name", model.Role);
                ViewBag.SalesGroups = new SelectList(await _context.SalesGroups.Where(g => g.IsActive).ToListAsync(), "Id", "Name", model.SalesGroupId);
                var orgs = string.IsNullOrEmpty(model.SalesGroupId) ? new List<SalesOrg>() : await _context.SalesOrgs.Where(o => o.SalesGroupId == model.SalesGroupId).ToListAsync();
                ViewBag.SalesOrgs = new SelectList(orgs, "Id", "Name", model.SalesOrgId);
                ViewBag.LockToGroup = false;
                ViewBag.LockToOrg = false;
            }

            await PopulateCommissionOptionsAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = LeadManagementPortal.Models.UserRoles.OrganizationAdmin + "," + LeadManagementPortal.Models.UserRoles.GroupAdmin + "," + LeadManagementPortal.Models.UserRoles.SalesOrgAdmin)]
        public async Task<IActionResult> Edit(EditUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.Users
                    .Include(u => u.CommissionDeal)
                    .Include(u => u.SponsorLink)
                    .FirstOrDefaultAsync(u => u.Id == model.Id);
                if (user == null)
                {
                    return NotFound();
                }

                var currentUser = await _userManager.GetUserAsync(User);

                if (User.IsInRole(UserRoles.GroupAdmin))
                {
                    // Enforce constraints for Group Admin edits
                    if (currentUser?.SalesGroupId == null)
                    {
                        return Forbid();
                    }

                    if (user.SalesGroupId != currentUser.SalesGroupId)
                    {
                        return Forbid();
                    }

                    var lockedRole = await GetLockedScopedRoleAsync(user);
                    if (lockedRole == null)
                    {
                        return Forbid();
                    }

                    model.Role = lockedRole;
                    model.SalesGroupId = currentUser.SalesGroupId;
                }
                else if (User.IsInRole(UserRoles.SalesOrgAdmin))
                {
                    // Enforce constraints for Sales Org Admin edits
                    if (currentUser?.SalesOrgId == null)
                    {
                        return Forbid();
                    }

                    if (user.SalesOrgId != currentUser.SalesOrgId)
                    {
                        return Forbid();
                    }

                    var lockedRole = await GetLockedScopedRoleAsync(user);
                    if (lockedRole == null)
                    {
                        return Forbid();
                    }

                    model.Role = lockedRole;
                    var org = await _context.SalesOrgs.FirstOrDefaultAsync(o => o.Id == currentUser.SalesOrgId.Value);
                    if (org != null)
                    {
                        model.SalesOrgId = org.Id;
                        model.SalesGroupId = org.SalesGroupId;
                    }
                }

                // Enforce mandatory fields based on Role
                if (model.Role == UserRoles.GroupAdmin)
                {
                    if (string.IsNullOrEmpty(model.SalesGroupId))
                    {
                        ModelState.AddModelError(nameof(model.SalesGroupId), "Sales Group is required for Group Admins.");
                    }
                }
                else if (model.Role == UserRoles.SalesOrgAdmin || model.Role == UserRoles.SalesRep)
                {
                    if (string.IsNullOrEmpty(model.SalesGroupId))
                    {
                        ModelState.AddModelError(nameof(model.SalesGroupId), "Sales Group is required.");
                    }
                    if (!model.SalesOrgId.HasValue)
                    {
                        ModelState.AddModelError(nameof(model.SalesOrgId), "Sales Org is required.");
                    }
                }

                // Validate SalesOrg belongs to selected SalesGroup (if provided)
                if (model.SalesOrgId.HasValue)
                {
                    var org = await _context.SalesOrgs.FirstOrDefaultAsync(o => o.Id == model.SalesOrgId.Value);
                    if (org == null)
                    {
                        ModelState.AddModelError(nameof(model.SalesOrgId), "Selected Sales Org does not exist.");
                    }
                    else if (!string.IsNullOrEmpty(model.SalesGroupId) && org.SalesGroupId != model.SalesGroupId)
                    {
                        ModelState.AddModelError(nameof(model.SalesOrgId), "Selected Sales Org does not belong to the chosen Sales Group.");
                    }
                }

                ValidateCommissionFields(model);

                if (!string.IsNullOrWhiteSpace(model.SponsorId))
                {
                    if (model.SponsorId == model.Id)
                    {
                        ModelState.AddModelError(nameof(model.SponsorId), "A user cannot sponsor themselves.");
                    }
                    else
                    {
                        var sponsor = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == model.SponsorId);
                        if (sponsor == null)
                        {
                            ModelState.AddModelError(nameof(model.SponsorId), "Selected sponsor does not exist.");
                        }
                        else if (!await CanAssignSponsorAsync(sponsor, currentUser))
                        {
                            ModelState.AddModelError(nameof(model.SponsorId), "Selected sponsor is outside your admin scope.");
                        }
                        else if (await WouldCreateCommissionCycleAsync(model.Id, model.SponsorId))
                        {
                            ModelState.AddModelError(nameof(model.SponsorId), "This sponsor selection would create a commission cycle.");
                        }
                    }
                }

                if (ModelState.IsValid)
                {
                    user.FirstName = model.FirstName;
                    user.LastName = model.LastName;
                    user.Email = model.Email;
                    user.UserName = model.Email;
                    user.SalesGroupId = model.SalesGroupId;
                    user.SalesOrgId = model.SalesOrgId;
                    user.IsActive = model.IsActive;

                    var result = await _userManager.UpdateAsync(user);
                    if (result.Succeeded)
                    {
                        // Update role
                        var currentRoles = await _userManager.GetRolesAsync(user);
                        await _userManager.RemoveFromRolesAsync(user, currentRoles);
                        await _userManager.AddToRoleAsync(user, model.Role);

                        await UpsertCommissionConfigurationAsync(user, model);

                        TempData["SuccessMessage"] = "User updated successfully!";
                        if (User.IsInRole(UserRoles.GroupAdmin))
                        {
                            return RedirectToAction(nameof(MyGroup));
                        }
                        else
                        {
                            return RedirectToAction(nameof(Index));
                        }
                    }

                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
            }

            if (User.IsInRole(UserRoles.GroupAdmin))
            {
                ViewBag.Roles = new SelectList(new[] { UserRoles.SalesRep }, model.Role);
                var currentUser = await _userManager.GetUserAsync(User);
                var group = currentUser?.SalesGroupId == null ? null : await _context.SalesGroups.FirstOrDefaultAsync(g => g.Id == currentUser.SalesGroupId);
                if (group != null)
                {
                    ViewBag.SalesGroups = new SelectList(new[] { group }, "Id", "Name", group.Id);
                    ViewBag.SalesOrgs = new SelectList(await _context.SalesOrgs.Where(o => o.SalesGroupId == group.Id).ToListAsync(), "Id", "Name", model.SalesOrgId);
                }
                else
                {
                    ViewBag.SalesGroups = new SelectList(Enumerable.Empty<SelectListItem>());
                    ViewBag.SalesOrgs = new SelectList(Enumerable.Empty<SelectListItem>());
                }
                ViewBag.LockToGroup = true;
                ViewBag.LockToOrg = false;
            }
            else if (User.IsInRole(UserRoles.SalesOrgAdmin))
            {
                ViewBag.Roles = new SelectList(new[] { UserRoles.SalesRep }, model.Role);
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser?.SalesOrgId != null)
                {
                    var org = await _context.SalesOrgs.FirstOrDefaultAsync(o => o.Id == currentUser.SalesOrgId.Value);
                    if (org != null)
                    {
                        var group = await _context.SalesGroups.FirstOrDefaultAsync(g => g.Id == org.SalesGroupId);
                        if (group != null)
                        {
                            ViewBag.SalesGroups = new SelectList(new[] { group }, "Id", "Name", group.Id);
                        }
                        else
                        {
                            ViewBag.SalesGroups = new SelectList(Enumerable.Empty<SelectListItem>());
                        }
                        ViewBag.SalesOrgs = new SelectList(new[] { org }, "Id", "Name", model.SalesOrgId);
                    }
                    else
                    {
                        ViewBag.SalesGroups = new SelectList(Enumerable.Empty<SelectListItem>());
                        ViewBag.SalesOrgs = new SelectList(Enumerable.Empty<SelectListItem>());
                    }
                }
                else
                {
                    ViewBag.SalesGroups = new SelectList(Enumerable.Empty<SelectListItem>());
                    ViewBag.SalesOrgs = new SelectList(Enumerable.Empty<SelectListItem>());
                }
                ViewBag.LockToGroup = true;
                ViewBag.LockToOrg = true;
            }
            else
            {
                ViewBag.Roles = new SelectList(GetRolesAsync(), "Name", "Name", model.Role);
                ViewBag.SalesGroups = new SelectList(await _context.SalesGroups.Where(g => g.IsActive).ToListAsync(), "Id", "Name", model.SalesGroupId);
                var orgs = string.IsNullOrEmpty(model.SalesGroupId) ? new List<SalesOrg>() : await _context.SalesOrgs.Where(o => o.SalesGroupId == model.SalesGroupId).ToListAsync();
                ViewBag.SalesOrgs = new SelectList(orgs, "Id", "Name", model.SalesOrgId);
                ViewBag.LockToGroup = false;
                ViewBag.LockToOrg = false;
            }

            await PopulateCommissionOptionsAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = LeadManagementPortal.Models.UserRoles.OrganizationAdmin)]
        public async Task<IActionResult> AssignToGroup(string userId, string salesGroupId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            user.SalesGroupId = string.IsNullOrEmpty(salesGroupId) ? null : salesGroupId;
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "User assigned to group successfully!";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to assign user to group.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Roles = LeadManagementPortal.Models.UserRoles.GroupAdmin)]
        public async Task<IActionResult> MyGroup()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.SalesGroupId == null)
            {
                TempData["ErrorMessage"] = "You are not assigned to a sales group.";
                return RedirectToAction("Index", "Dashboard");
            }

            var orgAdminRole = await _roleManager.FindByNameAsync(UserRoles.OrganizationAdmin);
            var orgAdminRoleId = orgAdminRole?.Id;

            var query = _userManager.Users
                .Include(u => u.SalesGroup)
                .Include(u => u.SalesOrg)
                .Where(u => u.SalesGroupId == currentUser.SalesGroupId);

            if (orgAdminRoleId != null)
            {
                query = query.Where(u => !_context.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == orgAdminRoleId));
            }

            var users = await query.ToListAsync();

            return View(users);
        }

        [HttpGet]
        [Authorize(Roles = LeadManagementPortal.Models.UserRoles.SalesOrgAdmin)]
        public async Task<IActionResult> MyOrg()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.SalesOrgId == null)
            {
                TempData["ErrorMessage"] = "You are not assigned to a sales organization.";
                return RedirectToAction("Index", "Dashboard");
            }

            var orgAdminRole = await _roleManager.FindByNameAsync(UserRoles.OrganizationAdmin);
            var groupAdminRole = await _roleManager.FindByNameAsync(UserRoles.GroupAdmin);
            
            var excludedRoleIds = new List<string>();
            if (orgAdminRole != null) excludedRoleIds.Add(orgAdminRole.Id);
            if (groupAdminRole != null) excludedRoleIds.Add(groupAdminRole.Id);

            var query = _userManager.Users
                .Include(u => u.SalesGroup)
                .Include(u => u.SalesOrg)
                .Where(u => u.SalesOrgId == currentUser.SalesOrgId);

            if (excludedRoleIds.Any())
            {
                query = query.Where(u => !_context.UserRoles.Any(ur => ur.UserId == u.Id && excludedRoleIds.Contains(ur.RoleId)));
            }

            var users = await query.ToListAsync();

            return View(users);
        }

        private IActionResult RedirectToUserList()
        {
            if (User.IsInRole(UserRoles.SalesOrgAdmin) && !User.IsInRole(UserRoles.OrganizationAdmin))
            {
                return RedirectToAction(nameof(MyOrg));
            }
            if (User.IsInRole(UserRoles.GroupAdmin) && !User.IsInRole(UserRoles.OrganizationAdmin))
            {
                return RedirectToAction(nameof(MyGroup));
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = LeadManagementPortal.Models.UserRoles.OrganizationAdmin + "," + LeadManagementPortal.Models.UserRoles.GroupAdmin + "," + LeadManagementPortal.Models.UserRoles.SalesOrgAdmin)]
        public async Task<IActionResult> PromoteToSalesOrgAdmin(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return NotFound();

            var actor = await _userManager.GetUserAsync(User);
            if (actor == null) return Challenge();

            var target = await _userManager.FindByIdAsync(userId);
            if (target == null) return NotFound();

            // Scope checks
            if (User.IsInRole(UserRoles.GroupAdmin) && !User.IsInRole(UserRoles.OrganizationAdmin))
            {
                if (actor.SalesGroupId == null || target.SalesGroupId != actor.SalesGroupId)
                {
                    return Forbid();
                }
            }
            if (User.IsInRole(UserRoles.SalesOrgAdmin) && !User.IsInRole(UserRoles.OrganizationAdmin))
            {
                if (actor.SalesOrgId == null || target.SalesOrgId != actor.SalesOrgId)
                {
                    return Forbid();
                }
            }

            var targetRoles = await _userManager.GetRolesAsync(target);
            if (targetRoles.Contains(UserRoles.OrganizationAdmin) || targetRoles.Contains(UserRoles.GroupAdmin))
            {
                TempData["ErrorMessage"] = "You cannot change roles for this user.";
                return RedirectToUserList();
            }

            if (targetRoles.Contains(UserRoles.SalesOrgAdmin))
            {
                TempData["SuccessMessage"] = "User is already a Sales Org Admin.";
                return RedirectToUserList();
            }

            if (!targetRoles.Contains(UserRoles.SalesRep))
            {
                TempData["ErrorMessage"] = "Only Sales Reps can be promoted to Sales Org Admin.";
                return RedirectToUserList();
            }

            if (target.SalesOrgId == null)
            {
                TempData["ErrorMessage"] = "User must be assigned to a Sales Org before promotion.";
                return RedirectToUserList();
            }

            if (targetRoles.Any())
            {
                await _userManager.RemoveFromRolesAsync(target, targetRoles);
            }

            var addResult = await _userManager.AddToRoleAsync(target, UserRoles.SalesOrgAdmin);
            if (!addResult.Succeeded)
            {
                TempData["ErrorMessage"] = "Failed to promote user.";
                return RedirectToUserList();
            }

            TempData["SuccessMessage"] = "User promoted to Sales Org Admin.";
            return RedirectToUserList();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = LeadManagementPortal.Models.UserRoles.OrganizationAdmin + "," + LeadManagementPortal.Models.UserRoles.GroupAdmin + "," + LeadManagementPortal.Models.UserRoles.SalesOrgAdmin)]
        public async Task<IActionResult> DemoteToSalesRep(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return NotFound();

            var actor = await _userManager.GetUserAsync(User);
            if (actor == null) return Challenge();

            var target = await _userManager.FindByIdAsync(userId);
            if (target == null) return NotFound();

            if (!User.IsInRole(UserRoles.OrganizationAdmin) && actor.Id == target.Id)
            {
                TempData["ErrorMessage"] = "You cannot demote yourself.";
                return RedirectToUserList();
            }

            // Scope checks
            if (User.IsInRole(UserRoles.GroupAdmin) && !User.IsInRole(UserRoles.OrganizationAdmin))
            {
                if (actor.SalesGroupId == null || target.SalesGroupId != actor.SalesGroupId)
                {
                    return Forbid();
                }
            }
            if (User.IsInRole(UserRoles.SalesOrgAdmin) && !User.IsInRole(UserRoles.OrganizationAdmin))
            {
                if (actor.SalesOrgId == null || target.SalesOrgId != actor.SalesOrgId)
                {
                    return Forbid();
                }
            }

            var targetRoles = await _userManager.GetRolesAsync(target);
            if (targetRoles.Contains(UserRoles.OrganizationAdmin) || targetRoles.Contains(UserRoles.GroupAdmin))
            {
                TempData["ErrorMessage"] = "You cannot change roles for this user.";
                return RedirectToUserList();
            }

            if (!targetRoles.Contains(UserRoles.SalesOrgAdmin))
            {
                TempData["SuccessMessage"] = "User is already a Sales Rep.";
                return RedirectToUserList();
            }

            if (targetRoles.Any())
            {
                await _userManager.RemoveFromRolesAsync(target, targetRoles);
            }

            var addResult = await _userManager.AddToRoleAsync(target, UserRoles.SalesRep);
            if (!addResult.Succeeded)
            {
                TempData["ErrorMessage"] = "Failed to demote user.";
                return RedirectToUserList();
            }

            TempData["SuccessMessage"] = "User demoted to Sales Rep.";
            return RedirectToUserList();
        }

        [HttpGet]
        [Authorize(Roles = LeadManagementPortal.Models.UserRoles.SalesOrgAdmin)]
        public async Task<IActionResult> Deactivate(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var currentAdmin = await _userManager.GetUserAsync(User);
            if (currentAdmin?.SalesOrgId == null) return Forbid();

            var user = await _userManager.Users.Include(u => u.SalesOrg).FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();
            if (user.SalesOrgId != currentAdmin.SalesOrgId) return Forbid();

            var candidates = await _userManager.Users
                .Where(u => u.IsActive
                            && u.Id != user.Id
                            && u.SalesOrgId == currentAdmin.SalesOrgId)
                .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                .ToListAsync();

            ViewBag.TransferTargets = new SelectList(candidates, "Id", "FullName");

            var model = new DeactivateUserViewModel
            {
                UserId = user.Id,
                UserDisplay = string.IsNullOrWhiteSpace(user.FirstName + user.LastName) ? user.Email ?? user.UserName ?? user.Id : user.FullName,
                IsCurrentlyActive = user.IsActive
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = LeadManagementPortal.Models.UserRoles.SalesOrgAdmin)]
        public async Task<IActionResult> Deactivate(DeactivateUserViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var currentAdmin = await _userManager.GetUserAsync(User);
            if (currentAdmin?.SalesOrgId == null) return Forbid();

            var fromUser = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == model.UserId);
            if (fromUser == null) return NotFound();
            if (fromUser.SalesOrgId != currentAdmin.SalesOrgId) return Forbid();

            var toUser = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == model.TransferToUserId);
            if (toUser == null || !toUser.IsActive || toUser.SalesOrgId != currentAdmin.SalesOrgId)
            {
                ModelState.AddModelError(string.Empty, "Please select a valid active user in your Sales Org.");
                var candidates = await _userManager.Users
                    .Where(u => u.IsActive && u.Id != model.UserId && u.SalesOrgId == currentAdmin.SalesOrgId)
                    .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                    .ToListAsync();
                ViewBag.TransferTargets = new SelectList(candidates, "Id", "FullName", model.TransferToUserId);
                return View(model);
            }

            // Reassign data: Leads (AssignedToId) and Customers (SalesRepId)
            var leads = await _context.Leads.Where(l => l.AssignedToId == fromUser.Id).ToListAsync();
            foreach (var lead in leads)
            {
                lead.AssignedToId = toUser.Id;
                // Keep existing SalesGroupId as-is; assumes same org/team
            }

            var customers = await _context.Customers.Where(c => c.SalesRepId == fromUser.Id && !c.IsDeleted).ToListAsync();
            foreach (var c in customers)
            {
                c.SalesRepId = toUser.Id;
            }

            // Deactivate the user
            fromUser.IsActive = false;

            await _context.SaveChangesAsync();
            await _userManager.UpdateAsync(fromUser);

            TempData["SuccessMessage"] = $"{fromUser.FullName} has been deactivated and data transferred to {toUser.FullName}.";
            return RedirectToAction(nameof(MyOrg));
        }

        private void ValidateCommissionFields(EditUserViewModel model)
        {
            var hasCommissionInput = model.CommissionDealType.HasValue
                || model.CommissionRate.HasValue
                || model.CommissionBaseCost.HasValue
                || model.CommissionCalculationBasis.HasValue;

            if (!hasCommissionInput)
            {
                return;
            }

            if (!model.CommissionDealType.HasValue)
            {
                ModelState.AddModelError(nameof(model.CommissionDealType), "Select a deal type or clear the commission fields.");
            }

            if (!model.CommissionRate.HasValue)
            {
                ModelState.AddModelError(nameof(model.CommissionRate), "Rate / amount is required when a commission deal is configured.");
            }

            if (!model.CommissionCalculationBasis.HasValue)
            {
                ModelState.AddModelError(nameof(model.CommissionCalculationBasis), "Calculation basis is required when a commission deal is configured.");
            }
        }

        private async Task PopulateCommissionOptionsAsync(EditUserViewModel model)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var sponsorCandidates = await GetSponsorCandidatesAsync(currentUser, model.Id);

            ViewBag.Sponsors = new SelectList(
                sponsorCandidates.Select(candidate => new
                {
                    candidate.Id,
                    Name = $"{candidate.FullName} ({candidate.Email})"
                }),
                "Id",
                "Name",
                model.SponsorId);
        }

        private async Task<List<ApplicationUser>> GetSponsorCandidatesAsync(ApplicationUser? currentUser, string editedUserId)
        {
            var query = _userManager.Users
                .AsNoTracking()
                .Where(u => u.IsActive && u.Id != editedUserId);

            if (User.IsInRole(UserRoles.GroupAdmin) && currentUser?.SalesGroupId != null)
            {
                query = query.Where(u => u.SalesGroupId == currentUser.SalesGroupId);
            }
            else if (User.IsInRole(UserRoles.SalesOrgAdmin) && currentUser?.SalesOrgId != null)
            {
                query = query.Where(u => u.SalesOrgId == currentUser.SalesOrgId);
            }

            return await query
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .ToListAsync();
        }

        private Task<bool> CanAssignSponsorAsync(ApplicationUser sponsor, ApplicationUser? currentUser)
        {
            if (User.IsInRole(UserRoles.GroupAdmin))
            {
                return Task.FromResult(currentUser?.SalesGroupId != null && sponsor.SalesGroupId == currentUser.SalesGroupId);
            }

            if (User.IsInRole(UserRoles.SalesOrgAdmin))
            {
                return Task.FromResult(currentUser?.SalesOrgId != null && sponsor.SalesOrgId == currentUser.SalesOrgId);
            }

            return Task.FromResult(true);
        }

        private async Task<bool> WouldCreateCommissionCycleAsync(string downlineId, string sponsorId)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { downlineId };
            var currentSponsorId = sponsorId;

            while (!string.IsNullOrWhiteSpace(currentSponsorId))
            {
                if (!visited.Add(currentSponsorId))
                {
                    return true;
                }

                currentSponsorId = await _context.CommissionLinks
                    .AsNoTracking()
                    .Where(link => link.DownlineId == currentSponsorId)
                    .Select(link => link.SponsorId)
                    .SingleOrDefaultAsync() ?? string.Empty;
            }

            return false;
        }

        private async Task UpsertCommissionConfigurationAsync(ApplicationUser user, EditUserViewModel model)
        {
            if (model.CommissionDealType.HasValue)
            {
                if (user.CommissionDeal == null)
                {
                    user.CommissionDeal = new CommissionDeal
                    {
                        ApplicationUserId = user.Id
                    };
                    _context.CommissionDeals.Add(user.CommissionDeal);
                }

                user.CommissionDeal.DealType = model.CommissionDealType.Value;
                user.CommissionDeal.Rate = model.CommissionRate ?? 0m;
                user.CommissionDeal.BaseCost = model.CommissionBaseCost;
                user.CommissionDeal.CalculationBasis = model.CommissionCalculationBasis ?? CommissionCalculationBasis.DownlineGross;
            }
            else if (user.CommissionDeal != null)
            {
                _context.CommissionDeals.Remove(user.CommissionDeal);
            }

            if (string.IsNullOrWhiteSpace(model.SponsorId))
            {
                if (user.SponsorLink != null)
                {
                    _context.CommissionLinks.Remove(user.SponsorLink);
                }
            }
            else if (user.SponsorLink == null)
            {
                _context.CommissionLinks.Add(new CommissionLink
                {
                    DownlineId = user.Id,
                    SponsorId = model.SponsorId
                });
            }
            else
            {
                user.SponsorLink.SponsorId = model.SponsorId;
            }

            await _context.SaveChangesAsync();
        }

        private async Task<string?> GetLockedScopedRoleAsync(ApplicationUser user)
        {
            var currentRoles = await _userManager.GetRolesAsync(user);
            var currentRole = currentRoles.FirstOrDefault();

            if (string.Equals(currentRole, UserRoles.SalesRep, StringComparison.Ordinal)
                || string.Equals(currentRole, UserRoles.Affiliate, StringComparison.Ordinal))
            {
                return currentRole;
            }

            return null;
        }

        private List<ApplicationRole> GetRolesAsync()
        {
            return _roleManager.Roles.ToList();
        }

        [HttpGet]
        public async Task<IActionResult> OrgsByGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
            {
                return Json(Enumerable.Empty<object>());
            }
            var orgs = await _context.SalesOrgs.Where(o => o.SalesGroupId == groupId).Select(o => new { o.Id, o.Name }).ToListAsync();
            return Json(orgs);
        }
    }

    public class CreateUserViewModel
    {
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = string.Empty;

        public string? SalesGroupId { get; set; }
        public int? SalesOrgId { get; set; }
    }

    public class EditUserViewModel
    {
        [Required]
        public string Id { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = string.Empty;

        public string? SalesGroupId { get; set; }
        public int? SalesOrgId { get; set; }

        public bool IsActive { get; set; }

        [Display(Name = "Sponsor")]
        public string? SponsorId { get; set; }

        [Display(Name = "Deal type")]
        public CommissionDealType? CommissionDealType { get; set; }

        [Display(Name = "Rate / amount")]
        [Range(typeof(decimal), "0", "999999999")]
        public decimal? CommissionRate { get; set; }

        [Display(Name = "Base cost")]
        [Range(typeof(decimal), "0", "999999999")]
        public decimal? CommissionBaseCost { get; set; }

        [Display(Name = "Calculation basis")]
        public CommissionCalculationBasis? CommissionCalculationBasis { get; set; }
    }

    public class DeactivateUserViewModel
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Display(Name = "User")]
        public string UserDisplay { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Transfer data to")]
        public string TransferToUserId { get; set; } = string.Empty;

        public bool IsCurrentlyActive { get; set; }
    }
}
