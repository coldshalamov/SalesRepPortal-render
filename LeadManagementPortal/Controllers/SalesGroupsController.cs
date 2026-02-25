using LeadManagementPortal.Models;
using LeadManagementPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace LeadManagementPortal.Controllers
{
    [Authorize(Roles = "OrganizationAdmin," + LeadManagementPortal.Models.UserRoles.GroupAdmin)]
    public class SalesGroupsController : Controller
    {
        private readonly ISalesGroupService _salesGroupService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public SalesGroupsController(
            ISalesGroupService salesGroupService,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env)
        {
            _salesGroupService = salesGroupService;
            _userManager = userManager;
            _env = env;
        }

        public async Task<IActionResult> Index()
        {
            IEnumerable<SalesGroup> groups;

            if (User.IsInRole(UserRoles.GroupAdmin) && !User.IsInRole(UserRoles.OrganizationAdmin))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser?.SalesGroupId == null)
                {
                    TempData["ErrorMessage"] = "You are not assigned to a sales group.";
                    groups = Enumerable.Empty<SalesGroup>();
                }
                else
                {
                    var ownGroup = await _salesGroupService.GetByIdAsync(currentUser.SalesGroupId);
                    groups = ownGroup == null ? Enumerable.Empty<SalesGroup>() : new[] { ownGroup };
                }
            }
            else
            {
                groups = await _salesGroupService.GetAllAsync();
            }

            var allGroupAdmins = await _userManager.GetUsersInRoleAsync(UserRoles.GroupAdmin);
            var adminsByGroup = allGroupAdmins
                .Where(u => !string.IsNullOrEmpty(u.SalesGroupId))
                .GroupBy(u => u.SalesGroupId!)
                .ToDictionary(g => g.Key, g => g.ToList());

            ViewBag.GroupAdmins = adminsByGroup;

            return View(groups);
        }

        [Authorize(Roles = LeadManagementPortal.Models.UserRoles.GroupAdmin)]
        public async Task<IActionResult> MyGroup()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (string.IsNullOrEmpty(user.SalesGroupId))
            {
                TempData["ErrorMessage"] = "You are not assigned to a Sales Group.";
                return RedirectToAction("Index", "Home");
            }

            return RedirectToAction(nameof(Details), new { id = user.SalesGroupId });
        }

        public async Task<IActionResult> Details(string id)
        {
            var group = await _salesGroupService.GetByIdAsync(id);
            if (group == null)
            {
                return NotFound();
            }

            if (User.IsInRole(UserRoles.GroupAdmin) && !User.IsInRole(UserRoles.OrganizationAdmin))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser?.SalesGroupId != id)
                {
                    return Forbid();
                }
            }

            var members = await _salesGroupService.GetGroupMembersAsync(id);
            ViewBag.Members = members;

            var allGroupAdmins = await _userManager.GetUsersInRoleAsync(UserRoles.GroupAdmin);
            var admins = allGroupAdmins.Where(u => u.SalesGroupId == id).ToList();
            ViewBag.Admins = admins;

            return View(group);
        }

        [HttpGet]
        [Authorize(Roles = LeadManagementPortal.Models.UserRoles.OrganizationAdmin)]
        public async Task<IActionResult> Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = LeadManagementPortal.Models.UserRoles.OrganizationAdmin)]
        public async Task<IActionResult> Create(SalesGroup salesGroup)
        {
            if (ModelState.IsValid)
            {
                if (await _salesGroupService.CreateAsync(salesGroup))
                {
                    TempData["SuccessMessage"] = "Sales Group created successfully!";
                    return RedirectToAction(nameof(Index));
                }

                ModelState.AddModelError("", "Failed to create sales group.");
            }

            return View(salesGroup);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var group = await _salesGroupService.GetByIdAsync(id);
            if (group == null)
            {
                return NotFound();
            }

            if (User.IsInRole(UserRoles.GroupAdmin) && !User.IsInRole(UserRoles.OrganizationAdmin))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser?.SalesGroupId != id)
                {
                    return Forbid();
                }
            }

            return View(group);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, SalesGroup salesGroup)
        {
            if (id != salesGroup.Id)
            {
                return NotFound();
            }

            if (User.IsInRole(UserRoles.GroupAdmin) && !User.IsInRole(UserRoles.OrganizationAdmin))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser?.SalesGroupId != id)
                {
                    return Forbid();
                }
            }

            if (ModelState.IsValid)
            {
                if (await _salesGroupService.UpdateAsync(salesGroup))
                {
                    TempData["SuccessMessage"] = "Sales Group updated successfully!";
                    return RedirectToAction(nameof(Details), new { id = salesGroup.Id });
                }

                ModelState.AddModelError("", "Failed to update sales group.");
            }
            await PopulateGroupAdmins();
            return View(salesGroup);
        }

        private async Task PopulateGroupAdmins()
        {
            var groupAdmins = await _userManager.GetUsersInRoleAsync(UserRoles.GroupAdmin);
            ViewBag.GroupAdmins = new SelectList(groupAdmins, "Id", "Email");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = LeadManagementPortal.Models.UserRoles.OrganizationAdmin + "," + LeadManagementPortal.Models.UserRoles.GroupAdmin)]
        public async Task<IActionResult> UploadLogo(string id, IFormFile? logo)
        {
            if (string.IsNullOrWhiteSpace(id) || logo == null || logo.Length == 0)
            {
                TempData["ErrorMessage"] = "Please select a valid image file.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var group = await _salesGroupService.GetByIdAsync(id);
            if (group == null)
            {
                return NotFound();
            }

            // Security check: Group Admin can only upload to their own group
            if (User.IsInRole(UserRoles.GroupAdmin) && !User.IsInRole(UserRoles.OrganizationAdmin))
            {
                var user = await _userManager.GetUserAsync(User);
                if (user?.SalesGroupId != id)
                {
                    return Forbid();
                }
            }

            var ext = Path.GetExtension(logo.FileName).ToLowerInvariant();
            var allowed = new[] { ".png", ".jpg", ".jpeg", ".svg" };
            if (!allowed.Contains(ext))
            {
                TempData["ErrorMessage"] = "Only PNG, JPG, JPEG, or SVG files are allowed.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var uploadsPath = Path.Combine(_env.WebRootPath, "uploads", "groups", id);
            Directory.CreateDirectory(uploadsPath);
            var fileName = $"logo{ext}";
            var filePath = Path.Combine(uploadsPath, fileName);
            using (var stream = System.IO.File.Create(filePath))
            {
                await logo.CopyToAsync(stream);
            }

            group.LogoUrl = $"/uploads/groups/{id}/{fileName}";
            await _salesGroupService.UpdateAsync(group);
            TempData["SuccessMessage"] = "Logo uploaded successfully.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
