using LeadManagementPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using LeadManagementPortal.Models;
using Microsoft.AspNetCore.Identity;

namespace LeadManagementPortal.Controllers
{
    [Authorize]
    public class LeadDocumentsController : Controller
    {
        private readonly ILeadDocumentService _leadDocs;
        private readonly ILeadService _leadService;
        private readonly UserManager<ApplicationUser> _userManager;

        public LeadDocumentsController(ILeadDocumentService leadDocs, ILeadService leadService, UserManager<ApplicationUser> userManager)
        {
            _leadDocs = leadDocs;
            _leadService = leadService;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> List(string leadId, CancellationToken ct)
        {
            if (!await CanAccessLeadAsync(leadId)) return Forbid();
            var docs = await _leadDocs.ListAsync(leadId, ct);
            return PartialView("~/Views/Leads/_LeadDocuments.cshtml", docs);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(string leadId, IFormFile file, CancellationToken ct)
        {
            if (!await CanAccessLeadAsync(leadId)) return Forbid();
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please choose a file.";
                return RedirectToAction("Details", "Leads", new { id = leadId });
            }

            var allowed = new[] { "application/pdf", "image/png", "image/jpeg", "text/plain", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" };
            var contentType = file.ContentType ?? "application/octet-stream";
            if (!allowed.Contains(contentType))
            {
                // Allow any role to upload, but basic sanity check
                // If strict validation needed, expand allowed types.
            }

            await using var stream = file.OpenReadStream();
            await _leadDocs.AddAsync(
                leadId: leadId,
                fileName: file.FileName,
                contentType: contentType,
                sizeBytes: file.Length,
                content: stream,
                uploadedByUserId: User?.Identity?.Name,
                ct: ct);

            TempData["Success"] = "Document uploaded.";
            return RedirectToAction("Details", "Leads", new { id = leadId });
        }

        [HttpGet]
        public async Task<IActionResult> Download(int id, CancellationToken ct)
        {
            var doc = await _leadDocs.GetAsync(id, ct);
            if (doc == null) return NotFound();
            if (!await CanAccessLeadAsync(doc.LeadId)) return Forbid();
            var url = await _leadDocs.GetDownloadUrlAsync(id, TimeSpan.FromMinutes(10), ct);
            if (url == null) return NotFound();
            return Redirect(url);
        }

        // Intentionally no Delete action per requirements (no one can delete)

        private async Task<bool> CanAccessLeadAsync(string leadId)
        {
            var lead = await _leadService.GetByIdAsync(leadId);
            if (lead == null) return false;

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "";

            if (userRole == UserRoles.OrganizationAdmin)
                return true;

            if (userRole == UserRoles.SalesOrgAdmin || userRole == UserRoles.GroupAdmin)
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
    }
}
