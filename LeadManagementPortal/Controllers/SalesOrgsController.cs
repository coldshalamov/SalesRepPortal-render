using LeadManagementPortal.Models;
using LeadManagementPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.IO;
using System;

namespace LeadManagementPortal.Controllers
{
    [Authorize(Roles = LeadManagementPortal.Models.UserRoles.OrganizationAdmin + "," + LeadManagementPortal.Models.UserRoles.SalesOrgAdmin)]
    public class SalesOrgsController : Controller
    {
        private readonly ISalesOrgService _service;
        private readonly ISalesGroupService _groupService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public SalesOrgsController(ISalesOrgService service, ISalesGroupService groupService, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
        {
            _service = service;
            _groupService = groupService;
            _userManager = userManager;
            _env = env;
        }

        public async Task<IActionResult> Index()
        {
            var orgs = await _service.GetAllAsync();
            return View(orgs);
        }

        public async Task<IActionResult> Details(int id)
        {
            var org = await _service.GetByIdAsync(id);
            if (org == null) return NotFound();

            var allOrgAdmins = await _userManager.GetUsersInRoleAsync(UserRoles.SalesOrgAdmin);
            var admins = allOrgAdmins.Where(u => u.SalesOrgId == id).ToList();
            ViewBag.Admins = admins;

            return View(org);
        }

        public async Task<IActionResult> Create()
        {
            var groups = await _groupService.GetAllAsync();
            if (groups == null)
            {
                ModelState.AddModelError(string.Empty, "Unable to load sales groups.");
                ViewBag.SalesGroups = Enumerable.Empty<SalesGroup>();
                return View(new SalesOrg());
            }

            if (!groups.Any())
            {
                // Provide a friendly message and still render the form
                TempData["Warning"] = "No Sales Groups found. Please create a Sales Group first or use the seeded default.";
            }

            ViewBag.SalesGroups = groups;
            return View(new SalesOrg());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,SalesGroupId")] SalesOrg model)
        {
            // Server-side validation guardrails
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                ModelState.AddModelError(nameof(model.Name), "Name is required.");
            }
            if (string.IsNullOrWhiteSpace(model.SalesGroupId))
            {
                ModelState.AddModelError(nameof(model.SalesGroupId), "Sales Group selection is required.");
            }

            await _service.CreateAsync(model);
            TempData["Success"] = "Sales Organization created successfully.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var org = await _service.GetByIdAsync(id);
            if (org == null) return NotFound();
            ViewBag.SalesGroups = await _groupService.GetAllAsync();
            return View(org);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SalesOrg model)
        {
            if (id != model.Id) return BadRequest();
            if (!ModelState.IsValid)
            {
                ViewBag.SalesGroups = await _groupService.GetAllAsync();
                return View(model);
            }

            var existing = await _service.GetByIdAsync(id);
            if (existing == null) return NotFound();

            existing.Name = model.Name;
            existing.SalesGroupId = model.SalesGroupId;
            
            await _service.UpdateAsync(existing);
            return RedirectToAction(nameof(Index));
        }



        public async Task<IActionResult> Delete(int id)
        {
            var org = await _service.GetByIdAsync(id);
            if (org == null) return NotFound();
            return View(org);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                await _service.DeleteAsync(id);
                TempData["SuccessMessage"] = "Sales Organization deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                // Log exception
                TempData["ErrorMessage"] = "Failed to delete Sales Organization. It may have associated users or leads.";
                var org = await _service.GetByIdAsync(id);
                return View(org); 
            }
        }

        [Authorize(Roles = UserRoles.SalesOrgAdmin)]
        public async Task<IActionResult> MyOrgDetails()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.SalesOrgId == null)
            {
                return NotFound("Sales Org not found for this user.");
            }

            var org = await _service.GetByIdAsync(user.SalesOrgId.Value);
            if (org == null) return NotFound();

            return View(org);
        }

        [HttpPost]
        [Authorize(Roles = UserRoles.OrganizationAdmin + "," + UserRoles.SalesOrgAdmin)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadLogo(int id, IFormFile logo, string? source = null)
        {
            var user = await _userManager.GetUserAsync(User);
            
            // Check permissions
            bool isOrgAdmin = User.IsInRole(UserRoles.OrganizationAdmin);
            bool isSalesOrgAdmin = User.IsInRole(UserRoles.SalesOrgAdmin);

            if (!isOrgAdmin && isSalesOrgAdmin && user?.SalesOrgId != id)
            {
                return Forbid();
            }

            var org = await _service.GetByIdAsync(id);
            if (org == null) return NotFound();

            if (logo != null && logo.Length > 0)
            {
                var ext = Path.GetExtension(logo.FileName).ToLowerInvariant();
                var allowed = new[] { ".png", ".jpg", ".jpeg", ".svg" };
                if (allowed.Contains(ext))
                {
                    var uploadsPath = Path.Combine(_env.WebRootPath, "uploads", "orgs", id.ToString());
                    Directory.CreateDirectory(uploadsPath);
                    var fileName = $"logo{ext}";
                    var filePath = Path.Combine(uploadsPath, fileName);
                    
                    using (var stream = System.IO.File.Create(filePath))
                    {
                        await logo.CopyToAsync(stream);
                    }
                    
                    org.LogoUrl = $"/uploads/orgs/{id}/{fileName}";
                    await _service.UpdateAsync(org);
                    TempData["SuccessMessage"] = "Logo uploaded successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Only PNG, JPG, JPEG, or SVG files are allowed.";
                }
            }
            else 
            {
                 TempData["ErrorMessage"] = "Please select a valid image file.";
            }

            // Redirect based on source
            if (source == "MyOrgDetails")
            {
                return RedirectToAction(nameof(MyOrgDetails));
            }
            
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
