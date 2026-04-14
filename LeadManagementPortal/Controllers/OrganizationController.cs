using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using LeadManagementPortal.Models.ViewModels;
using LeadManagementPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeadManagementPortal.Controllers
{
    [Authorize(Roles = UserRoles.OrganizationAdmin + "," + UserRoles.GroupAdmin + "," + UserRoles.SalesOrgAdmin)]
    public class OrganizationController : Controller
    {
        private readonly ISalesGroupService _groupService;
        private readonly ISalesOrgService _orgService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public OrganizationController(
            ISalesGroupService groupService,
            ISalesOrgService orgService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _groupService = groupService;
            _orgService = orgService;
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            bool isOrgAdmin = User.IsInRole(UserRoles.OrganizationAdmin);
            bool isGroupAdmin = User.IsInRole(UserRoles.GroupAdmin);

            IEnumerable<SalesGroup> groups;
            if (isOrgAdmin)
            {
                groups = await _groupService.GetAllAsync();
            }
            else if (currentUser?.SalesGroupId != null)
            {
                var g = await _groupService.GetByIdAsync(currentUser.SalesGroupId);
                groups = g != null ? new[] { g } : Enumerable.Empty<SalesGroup>();
            }
            else
            {
                groups = Enumerable.Empty<SalesGroup>();
            }

            var orgCounts = await _context.Users
                .Where(u => u.SalesOrgId != null)
                .GroupBy(u => u.SalesOrgId!.Value)
                .Select(g => new { OrgId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.OrgId, x => x.Count);

            var groupCounts = await _context.Users
                .Where(u => u.SalesGroupId != null)
                .GroupBy(u => u.SalesGroupId!)
                .Select(g => new { GroupId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.GroupId, x => x.Count);

            var vm = new OrganizationViewModel
            {
                CanManageGroups = isOrgAdmin,
                CanManageOrgs = isOrgAdmin || isGroupAdmin,
                Groups = groups.Select(g => new OrgChartGroupViewModel
                {
                    Id = g.Id,
                    Name = g.Name,
                    Description = g.Description,
                    LogoUrl = g.LogoUrl,
                    MemberCount = groupCounts.TryGetValue(g.Id, out var gc) ? gc : 0,
                    Orgs = g.SalesOrgs
                        .Select(o => new OrgChartOrgViewModel
                        {
                            Id = o.Id,
                            Name = o.Name,
                            SalesGroupId = o.SalesGroupId,
                            LogoUrl = o.LogoUrl,
                            MemberCount = orgCounts.TryGetValue(o.Id, out var oc) ? oc : 0
                        })
                        .OrderBy(o => o.Name)
                        .ToList()
                }).ToList()
            };

            return View(vm);
        }

        // ── Group API ──────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.OrganizationAdmin)]
        public async Task<IActionResult> CreateGroup([FromBody] GroupFormRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Json(new { ok = false, error = "Name is required." });

            var group = new SalesGroup { Name = req.Name.Trim(), Description = req.Description?.Trim() };
            var ok = await _groupService.CreateAsync(group);
            if (!ok)
                return Json(new { ok = false, error = "Failed to create group." });

            return Json(new { ok = true, id = group.Id, name = group.Name, description = group.Description });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.OrganizationAdmin + "," + UserRoles.GroupAdmin)]
        public async Task<IActionResult> EditGroup([FromBody] GroupFormRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Id))
                return Json(new { ok = false, error = "Invalid request." });

            var group = await _groupService.GetByIdAsync(req.Id);
            if (group == null)
                return Json(new { ok = false, error = "Group not found." });

            if (!User.IsInRole(UserRoles.OrganizationAdmin))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser?.SalesGroupId != req.Id)
                    return Json(new { ok = false, error = "Forbidden." });
            }

            group.Name = req.Name?.Trim() ?? group.Name;
            group.Description = req.Description?.Trim();
            await _groupService.UpdateAsync(group);

            return Json(new { ok = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.OrganizationAdmin)]
        public async Task<IActionResult> DeleteGroup([FromBody] IdRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Id))
                return Json(new { ok = false, error = "Invalid request." });

            var ok = await _groupService.DeleteAsync(req.Id);
            return Json(new { ok });
        }

        // ── Org API ────────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.OrganizationAdmin + "," + UserRoles.GroupAdmin)]
        public async Task<IActionResult> CreateOrg([FromBody] OrgFormRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.GroupId))
                return Json(new { ok = false, error = "Name and group are required." });

            if (!User.IsInRole(UserRoles.OrganizationAdmin))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser?.SalesGroupId != req.GroupId)
                    return Json(new { ok = false, error = "Forbidden." });
            }

            var org = new SalesOrg { Name = req.Name.Trim(), SalesGroupId = req.GroupId };
            var created = await _orgService.CreateAsync(org);
            return Json(new { ok = true, id = created.Id, name = created.Name });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.OrganizationAdmin + "," + UserRoles.GroupAdmin)]
        public async Task<IActionResult> EditOrg([FromBody] OrgFormRequest req)
        {
            var org = await _orgService.GetByIdAsync(req.Id);
            if (org == null)
                return Json(new { ok = false, error = "Org not found." });

            if (!User.IsInRole(UserRoles.OrganizationAdmin))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser?.SalesGroupId != org.SalesGroupId)
                    return Json(new { ok = false, error = "Forbidden." });
            }

            org.Name = req.Name?.Trim() ?? org.Name;
            await _orgService.UpdateAsync(org);
            return Json(new { ok = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.OrganizationAdmin + "," + UserRoles.GroupAdmin)]
        public async Task<IActionResult> DeleteOrg([FromBody] IdIntRequest req)
        {
            var org = await _orgService.GetByIdAsync(req.Id);
            if (org == null)
                return Json(new { ok = false, error = "Org not found." });

            if (!User.IsInRole(UserRoles.OrganizationAdmin))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser?.SalesGroupId != org.SalesGroupId)
                    return Json(new { ok = false, error = "Forbidden." });
            }

            var ok = await _orgService.DeleteAsync(req.Id);
            return Json(new { ok });
        }

        // ── Request models ─────────────────────────────────────────────────

        public record GroupFormRequest(string? Id, string Name, string? Description);
        public record OrgFormRequest(int Id, string Name, string GroupId);
        public record IdRequest(string Id);
        public record IdIntRequest(int Id);
    }
}
