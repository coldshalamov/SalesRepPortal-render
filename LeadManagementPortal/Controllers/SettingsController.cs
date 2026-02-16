using LeadManagementPortal.Models;
using LeadManagementPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeadManagementPortal.Controllers
{
    [Authorize(Roles = UserRoles.OrganizationAdmin)]
    public class SettingsController : Controller
    {
        private readonly ISettingsService _settingsService;

        public SettingsController(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var settings = await _settingsService.GetAsync();
            return View(settings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(SystemSettings model)
        {
            if (model.CoolingPeriodDays < 0) ModelState.AddModelError(nameof(model.CoolingPeriodDays), "Must be >= 0");
            if (model.LeadInitialExpiryDays <= 0) ModelState.AddModelError(nameof(model.LeadInitialExpiryDays), "Must be > 0");
            if (model.LeadExtensionDays <= 0) ModelState.AddModelError(nameof(model.LeadExtensionDays), "Must be > 0");

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            await _settingsService.UpdateAsync(model);
            TempData["SuccessMessage"] = "Settings updated successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}
