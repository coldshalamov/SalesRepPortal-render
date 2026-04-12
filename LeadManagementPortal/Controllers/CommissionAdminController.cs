using System.Text.Json;
using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using LeadManagementPortal.Models.ViewModels;
using LeadManagementPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LeadManagementPortal.Controllers
{
    [Authorize(Roles = UserRoles.OrganizationAdmin)]
    public class CommissionAdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICommissionControlPlaneService _commissionControlPlaneService;
        private readonly UserManager<ApplicationUser> _userManager;

        public CommissionAdminController(
            ApplicationDbContext context,
            ICommissionControlPlaneService commissionControlPlaneService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _commissionControlPlaneService = commissionControlPlaneService;
            _userManager = userManager;
        }

        public IActionResult Index() => RedirectToAction(nameof(Imports));

        public async Task<IActionResult> Accounts()
        {
            return View(await _context.BusinessAccounts.AsNoTracking().OrderBy(a => a.Name).ToListAsync());
        }

        [HttpGet]
        public IActionResult CreateAccount()
        {
            return View("EditAccount", new BusinessAccountEditViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAccount(BusinessAccountEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("EditAccount", model);
            }

            _context.BusinessAccounts.Add(new BusinessAccount
            {
                Name = model.Name.Trim(),
                ExternalKey = model.ExternalKey?.Trim(),
                Notes = model.Notes?.Trim(),
                IsActive = model.IsActive,
                CreatedAtUtc = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Business account created.";
            return RedirectToAction(nameof(Accounts));
        }

        [HttpGet]
        public async Task<IActionResult> EditAccount(int id)
        {
            var account = await _context.BusinessAccounts.FindAsync(id);
            if (account == null)
            {
                return NotFound();
            }

            return View(new BusinessAccountEditViewModel
            {
                Id = account.Id,
                Name = account.Name,
                ExternalKey = account.ExternalKey,
                Notes = account.Notes,
                IsActive = account.IsActive
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAccount(BusinessAccountEditViewModel model)
        {
            if (!ModelState.IsValid || !model.Id.HasValue)
            {
                return View(model);
            }

            var account = await _context.BusinessAccounts.FindAsync(model.Id.Value);
            if (account == null)
            {
                return NotFound();
            }

            account.Name = model.Name.Trim();
            account.ExternalKey = model.ExternalKey?.Trim();
            account.Notes = model.Notes?.Trim();
            account.IsActive = model.IsActive;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Business account updated.";
            return RedirectToAction(nameof(Accounts));
        }

        public async Task<IActionResult> Agreements()
        {
            var agreements = await _context.CommissionAgreements
                .AsNoTracking()
                .Include(a => a.BusinessAccount)
                .Include(a => a.Recipients)
                    .ThenInclude(r => r.Beneficiary)
                .OrderByDescending(a => a.IsActive)
                .ThenBy(a => a.BusinessAccount!.Name)
                .ThenBy(a => a.Name)
                .ToListAsync();

            return View(agreements);
        }

        [HttpGet]
        public async Task<IActionResult> CreateAgreement()
        {
            var model = new CommissionAgreementEditViewModel
            {
                Recipients = new List<CommissionAgreementRecipientEditViewModel>
                {
                    new() { SortOrder = 1 }
                }
            };

            await PopulateAgreementOptionsAsync();
            return View("EditAgreement", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAgreement(CommissionAgreementEditViewModel model)
        {
            return await SaveAgreementAsync(model, isCreate: true);
        }

        [HttpGet]
        public async Task<IActionResult> EditAgreement(int id)
        {
            var agreement = await _context.CommissionAgreements
                .Include(a => a.Recipients)
                .SingleOrDefaultAsync(a => a.Id == id);
            if (agreement == null)
            {
                return NotFound();
            }

            var orderedRecipients = agreement.Recipients.OrderBy(r => r.SortOrder).ThenBy(r => r.Id).ToList();
            var sortLookup = orderedRecipients.ToDictionary(r => r.Id, r => r.SortOrder);
            var model = new CommissionAgreementEditViewModel
            {
                Id = agreement.Id,
                BusinessAccountId = agreement.BusinessAccountId,
                Name = agreement.Name,
                EffectiveStartDate = agreement.EffectiveStartDate,
                EffectiveEndDate = agreement.EffectiveEndDate,
                IsActive = agreement.IsActive,
                ProductNameFilter = agreement.ProductNameFilter,
                Notes = agreement.Notes,
                Recipients = orderedRecipients.Select(r => new CommissionAgreementRecipientEditViewModel
                {
                    Id = r.Id,
                    BeneficiaryId = r.BeneficiaryId,
                    CalculationType = r.CalculationType,
                    RateOrAmount = r.RateOrAmount,
                    BasisRecipientKey = r.BasisRecipientId.HasValue && sortLookup.TryGetValue(r.BasisRecipientId.Value, out var sortOrder) ? sortOrder : null,
                    SortOrder = r.SortOrder,
                    Notes = r.Notes
                }).ToList()
            };

            await PopulateAgreementOptionsAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAgreement(CommissionAgreementEditViewModel model)
        {
            return await SaveAgreementAsync(model, isCreate: false);
        }

        public async Task<IActionResult> ImportProfiles()
        {
            return View(await _context.ImportProfiles.AsNoTracking().OrderBy(p => p.Name).ToListAsync());
        }

        [HttpGet]
        public IActionResult CreateProfile()
        {
            return View("EditProfile", new ImportProfileEditViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProfile(ImportProfileEditViewModel model)
        {
            return await SaveImportProfileAsync(model, isCreate: true);
        }

        [HttpGet]
        public async Task<IActionResult> EditProfile(int id)
        {
            var profile = await _context.ImportProfiles.FindAsync(id);
            if (profile == null)
            {
                return NotFound();
            }

            return View(new ImportProfileEditViewModel
            {
                Id = profile.Id,
                Name = profile.Name,
                SourceSystem = profile.SourceSystem,
                IsActive = profile.IsActive,
                ExternalRowIdColumn = TryGetMapping(profile.ColumnMappingsJson, "ExternalRowId"),
                BusinessAccountExternalKeyColumn = TryGetMapping(profile.ColumnMappingsJson, "BusinessAccountExternalKey"),
                BusinessAccountNameColumn = TryGetMapping(profile.ColumnMappingsJson, "BusinessAccountName"),
                ProductNameColumn = TryGetMapping(profile.ColumnMappingsJson, "ProductName"),
                QuantityColumn = TryGetMapping(profile.ColumnMappingsJson, "Quantity"),
                GrossAmountColumn = TryGetMapping(profile.ColumnMappingsJson, "GrossAmount"),
                CostAmountColumn = TryGetMapping(profile.ColumnMappingsJson, "CostAmount"),
                SaleDateColumn = TryGetMapping(profile.ColumnMappingsJson, "SaleDate"),
                CreditedRepIdColumn = TryGetMapping(profile.ColumnMappingsJson, "CreditedRepId")
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(ImportProfileEditViewModel model)
        {
            return await SaveImportProfileAsync(model, isCreate: false);
        }

        public async Task<IActionResult> Imports()
        {
            var batches = await _context.ImportBatches
                .AsNoTracking()
                .Include(b => b.ImportProfile)
                .Include(b => b.UploadedBy)
                .OrderByDescending(b => b.ReceivedAtUtc)
                .ToListAsync();

            ViewBag.ImportProfiles = new SelectList(
                await _context.ImportProfiles.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
                "Id",
                "Name");

            return View(batches);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadCsv(IFormFile? file, int? importProfileId)
        {
            if (file == null || file.Length == 0)
            {
                TempData["ErrorMessage"] = "Select a CSV file to upload.";
                return RedirectToAction(nameof(Imports));
            }

            await using var stream = file.OpenReadStream();
            var rows = await CommissionControlPlaneService.ReadCsvRowsAsync(stream);
            var currentUser = await _userManager.GetUserAsync(User);

            var batch = await _commissionControlPlaneService.CreateBatchFromRawRowsAsync(
                "csv-upload",
                rows,
                importProfileId,
                currentUser?.Id,
                file.FileName);

            TempData["SuccessMessage"] = $"Imported {batch.Rows.Count} rows into batch #{batch.Id}.";
            return RedirectToAction(nameof(ReviewBatch), new { id = batch.Id });
        }

        public async Task<IActionResult> ReviewBatch(int id)
        {
            var batch = await _context.ImportBatches
                .AsNoTracking()
                .Include(b => b.ImportProfile)
                .Include(b => b.Rows.OrderBy(r => r.RowNumber))
                .SingleOrDefaultAsync(b => b.Id == id);
            if (batch == null)
            {
                return NotFound();
            }

            return View(new ImportBatchReviewViewModel
            {
                Batch = batch,
                Rows = batch.Rows.OrderBy(r => r.RowNumber).ToList()
            });
        }

        [HttpGet]
        public async Task<IActionResult> EditRow(int id)
        {
            var row = await _context.ImportRows
                .AsNoTracking()
                .SingleOrDefaultAsync(r => r.Id == id);
            if (row == null)
            {
                return NotFound();
            }

            var model = new ImportRowEditViewModel
            {
                Id = row.Id,
                ImportBatchId = row.ImportBatchId,
                RowNumber = row.RowNumber,
                Status = row.Status,
                ExternalRowId = row.ExternalRowId,
                BusinessAccountId = row.BusinessAccountId,
                BusinessAccountExternalKey = row.BusinessAccountExternalKey,
                BusinessAccountName = row.BusinessAccountName,
                SelectedAgreementId = row.SelectedAgreementId,
                ProductName = row.ProductName,
                Quantity = row.Quantity,
                GrossAmount = row.GrossAmount,
                CostAmount = row.CostAmount,
                SaleDate = row.SaleDate,
                CreditedRepId = row.CreditedRepId,
                ReviewNotes = row.ReviewNotes,
                RawPayloadJson = row.RawPayloadJson,
                MappedPayloadJson = row.MappedPayloadJson
            };

            await PopulateImportRowOptionsAsync(model.BusinessAccountId, model.SelectedAgreementId, model.CreditedRepId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRow(ImportRowEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateImportRowOptionsAsync(model.BusinessAccountId, model.SelectedAgreementId, model.CreditedRepId);
                return View(model);
            }

            var row = await _context.ImportRows.SingleOrDefaultAsync(r => r.Id == model.Id);
            if (row == null)
            {
                return NotFound();
            }

            row.ExternalRowId = model.ExternalRowId?.Trim();
            row.BusinessAccountId = model.BusinessAccountId;
            row.BusinessAccountExternalKey = model.BusinessAccountExternalKey?.Trim();
            row.BusinessAccountName = model.BusinessAccountName?.Trim();
            row.SelectedAgreementId = model.SelectedAgreementId;
            row.ProductName = model.ProductName?.Trim();
            row.Quantity = model.Quantity;
            row.GrossAmount = model.GrossAmount;
            row.CostAmount = model.CostAmount;
            row.SaleDate = model.SaleDate;
            row.CreditedRepId = model.CreditedRepId;
            row.ReviewNotes = model.ReviewNotes?.Trim();
            row.MappedPayloadJson = JsonSerializer.Serialize(new
            {
                row.ExternalRowId,
                row.BusinessAccountExternalKey,
                row.BusinessAccountName,
                row.ProductName,
                row.Quantity,
                row.GrossAmount,
                row.CostAmount,
                row.SaleDate,
                row.CreditedRepId
            });
            row.Status = ImportRowStatus.PendingReview;

            await _context.SaveChangesAsync();
            await _commissionControlPlaneService.EvaluateRowAsync(row.Id);

            TempData["SuccessMessage"] = $"Row #{row.RowNumber} updated.";
            return RedirectToAction(nameof(ReviewBatch), new { id = row.ImportBatchId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EvaluateBatch(int id)
        {
            await _commissionControlPlaneService.EvaluateBatchAsync(id);
            TempData["SuccessMessage"] = $"Batch #{id} evaluated.";
            return RedirectToAction(nameof(ReviewBatch), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostBatch(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            await _commissionControlPlaneService.PostReadyRowsAsync(id, currentUser.Id);
            TempData["SuccessMessage"] = $"Batch #{id} posted.";
            return RedirectToAction(nameof(ReviewBatch), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRowRejected(int id)
        {
            var row = await _context.ImportRows.FindAsync(id);
            if (row == null)
            {
                return NotFound();
            }

            row.Status = ImportRowStatus.Rejected;
            row.ReviewNotes = "Rejected by Organization Admin.";
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Row #{row.RowNumber} rejected.";
            return RedirectToAction(nameof(ReviewBatch), new { id = row.ImportBatchId });
        }

        public async Task<IActionResult> Adjustments()
        {
            ViewBag.Beneficiaries = await BuildBeneficiarySelectListAsync();
            var adjustments = await _context.CommissionAdjustments
                .AsNoTracking()
                .Include(a => a.Beneficiary)
                .Include(a => a.CreatedBy)
                .OrderByDescending(a => a.CreatedAtUtc)
                .ToListAsync();
            return View(adjustments);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAdjustment(CommissionAdjustmentEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Beneficiaries = await BuildBeneficiarySelectListAsync();
                var adjustments = await _context.CommissionAdjustments
                    .AsNoTracking()
                    .Include(a => a.Beneficiary)
                    .Include(a => a.CreatedBy)
                    .OrderByDescending(a => a.CreatedAtUtc)
                    .ToListAsync();
                return View("Adjustments", adjustments);
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            _context.CommissionAdjustments.Add(new CommissionAdjustment
            {
                BeneficiaryId = model.BeneficiaryId,
                Amount = model.Amount,
                Reason = model.Reason.Trim(),
                Notes = model.Notes?.Trim(),
                CreatedById = currentUser.Id,
                CreatedAtUtc = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Adjustment created.";
            return RedirectToAction(nameof(Adjustments));
        }

        public async Task<IActionResult> Payouts(string? beneficiaryId)
        {
            var batches = await _context.PayoutBatches
                .AsNoTracking()
                .Include(p => p.Entries)
                .OrderByDescending(p => p.PaidAtUtc)
                .ThenByDescending(p => p.CreatedAtUtc)
                .ToListAsync();

            ViewBag.PayoutForm = await BuildPayoutViewModelAsync(beneficiaryId);
            ViewBag.Beneficiaries = await BuildBeneficiarySelectListAsync(beneficiaryId);
            return View(batches);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePayout(PayoutBatchCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Fix the payout form and try again.";
                return RedirectToAction(nameof(Payouts), new { beneficiaryId = model.BeneficiaryId });
            }

            var selected = model.Items
                .Where(i => i.IsSelected && i.SelectedAmount > 0m)
                .Select(i => new PayoutSelectionRequest
                {
                    BeneficiaryId = model.BeneficiaryId,
                    ItemType = i.ItemType,
                    SourceId = i.SourceId,
                    Amount = i.SelectedAmount
                })
                .ToList();

            if (!selected.Any())
            {
                TempData["ErrorMessage"] = "Select at least one payout item.";
                return RedirectToAction(nameof(Payouts), new { beneficiaryId = model.BeneficiaryId });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            try
            {
                await _commissionControlPlaneService.CreatePayoutBatchAsync(currentUser.Id, model.Reference.Trim(), model.Notes?.Trim(), selected);
                TempData["SuccessMessage"] = "Payout batch created.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Payouts), new { beneficiaryId = model.BeneficiaryId });
        }

        private async Task<IActionResult> SaveAgreementAsync(CommissionAgreementEditViewModel model, bool isCreate)
        {
            model.Recipients ??= new List<CommissionAgreementRecipientEditViewModel>();
            model.Recipients = model.Recipients
                .Where(r => !string.IsNullOrWhiteSpace(r.BeneficiaryId))
                .OrderBy(r => r.SortOrder)
                .ThenBy(r => r.Id)
                .ToList();

            if (model.Recipients.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Add at least one recipient.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateAgreementOptionsAsync();
                return View("EditAgreement", model);
            }

            CommissionAgreement agreement;
            if (isCreate)
            {
                agreement = new CommissionAgreement();
                _context.CommissionAgreements.Add(agreement);
            }
            else
            {
                var existingAgreement = await _context.CommissionAgreements
                    .Include(a => a.Recipients)
                    .SingleOrDefaultAsync(a => a.Id == model.Id);
                if (existingAgreement == null)
                {
                    return NotFound();
                }

                agreement = existingAgreement;
                _context.CommissionAgreementRecipients.RemoveRange(agreement.Recipients);
            }

            agreement.BusinessAccountId = model.BusinessAccountId;
            agreement.Name = model.Name.Trim();
            agreement.EffectiveStartDate = model.EffectiveStartDate.Date;
            agreement.EffectiveEndDate = model.EffectiveEndDate.Date;
            agreement.IsActive = model.IsActive;
            agreement.ProductNameFilter = model.ProductNameFilter?.Trim();
            agreement.Notes = model.Notes?.Trim();

            await _context.SaveChangesAsync();

            var recipients = model.Recipients
                .Select((recipient, index) => new CommissionAgreementRecipient
                {
                    CommissionAgreementId = agreement.Id,
                    BeneficiaryId = recipient.BeneficiaryId,
                    CalculationType = recipient.CalculationType,
                    RateOrAmount = recipient.RateOrAmount,
                    SortOrder = recipient.SortOrder == 0 ? index + 1 : recipient.SortOrder,
                    Notes = recipient.Notes?.Trim()
                })
                .ToList();

            _context.CommissionAgreementRecipients.AddRange(recipients);
            await _context.SaveChangesAsync();

            var sortLookup = recipients.ToDictionary(r => r.SortOrder, r => r.Id);
            for (var index = 0; index < model.Recipients.Count; index++)
            {
                var basisKey = model.Recipients[index].BasisRecipientKey;
                if (basisKey.HasValue && sortLookup.TryGetValue(basisKey.Value, out var basisRecipientId))
                {
                    recipients[index].BasisRecipientId = basisRecipientId;
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = isCreate ? "Agreement created." : "Agreement updated.";
            return RedirectToAction(nameof(Agreements));
        }

        private async Task<IActionResult> SaveImportProfileAsync(ImportProfileEditViewModel model, bool isCreate)
        {
            if (!ModelState.IsValid)
            {
                return View("EditProfile", model);
            }

            var mappings = new Dictionary<string, string?>
            {
                ["ExternalRowId"] = model.ExternalRowIdColumn,
                ["BusinessAccountExternalKey"] = model.BusinessAccountExternalKeyColumn,
                ["BusinessAccountName"] = model.BusinessAccountNameColumn,
                ["ProductName"] = model.ProductNameColumn,
                ["Quantity"] = model.QuantityColumn,
                ["GrossAmount"] = model.GrossAmountColumn,
                ["CostAmount"] = model.CostAmountColumn,
                ["SaleDate"] = model.SaleDateColumn,
                ["CreditedRepId"] = model.CreditedRepIdColumn
            }
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!, StringComparer.OrdinalIgnoreCase);

            ImportProfile profile;
            if (isCreate)
            {
                profile = new ImportProfile();
                _context.ImportProfiles.Add(profile);
            }
            else
            {
                var existingProfile = await _context.ImportProfiles.FindAsync(model.Id);
                if (existingProfile == null)
                {
                    return NotFound();
                }

                profile = existingProfile;
            }

            profile.Name = model.Name.Trim();
            profile.SourceSystem = model.SourceSystem?.Trim();
            profile.IsActive = model.IsActive;
            profile.ColumnMappingsJson = JsonSerializer.Serialize(mappings);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = isCreate ? "Import profile created." : "Import profile updated.";
            return RedirectToAction(nameof(ImportProfiles));
        }

        private async Task PopulateAgreementOptionsAsync()
        {
            ViewBag.BusinessAccounts = new SelectList(
                await _context.BusinessAccounts.AsNoTracking().Where(a => a.IsActive).OrderBy(a => a.Name).ToListAsync(),
                "Id",
                "Name");
            ViewBag.Beneficiaries = await BuildBeneficiarySelectListAsync();
        }

        private async Task<SelectList> BuildBeneficiarySelectListAsync(string? selectedValue = null)
        {
            var users = await _context.Users
                .AsNoTracking()
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .Select(u => new
                {
                    u.Id,
                    Display = string.IsNullOrWhiteSpace(u.FullName) ? (u.Email ?? u.Id) : $"{u.FullName} ({u.Email})"
                })
                .ToListAsync();

            return new SelectList(users, "Id", "Display", selectedValue);
        }

        private async Task PopulateImportRowOptionsAsync(int? businessAccountId, int? selectedAgreementId, string? creditedRepId)
        {
            var businessAccounts = await _context.BusinessAccounts
                .AsNoTracking()
                .Where(a => a.IsActive)
                .OrderBy(a => a.Name)
                .ToListAsync();

            var agreementsQuery = _context.CommissionAgreements
                .AsNoTracking()
                .Include(a => a.BusinessAccount)
                .Where(a => a.IsActive);

            if (businessAccountId.HasValue)
            {
                agreementsQuery = agreementsQuery.Where(a => a.BusinessAccountId == businessAccountId.Value);
            }

            var agreements = await agreementsQuery
                .OrderBy(a => a.BusinessAccount!.Name)
                .ThenBy(a => a.Name)
                .ToListAsync();

            var reps = await _context.Users
                .AsNoTracking()
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .Select(u => new
                {
                    u.Id,
                    Display = string.IsNullOrWhiteSpace(u.FullName) ? (u.Email ?? u.Id) : $"{u.FullName} ({u.Email})"
                })
                .ToListAsync();

            ViewBag.BusinessAccounts = new SelectList(businessAccounts, "Id", "Name", businessAccountId);
            ViewBag.Agreements = new SelectList(
                agreements.Select(a => new
                {
                    a.Id,
                    Display = $"{a.BusinessAccount!.Name} - {a.Name}"
                }),
                "Id",
                "Display",
                selectedAgreementId);
            ViewBag.CreditedReps = new SelectList(reps, "Id", "Display", creditedRepId);
        }

        private async Task<PayoutBatchCreateViewModel> BuildPayoutViewModelAsync(string? beneficiaryId)
        {
            var targetUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == beneficiaryId)
                ?? await _context.Users.AsNoTracking().OrderBy(u => u.FirstName).FirstOrDefaultAsync();

            if (targetUser == null)
            {
                return new PayoutBatchCreateViewModel
                {
                    Reference = $"PAYOUT-{DateTime.UtcNow:yyyyMMddHHmmss}"
                };
            }

            var items = await _commissionControlPlaneService.GetOutstandingItemsAsync(targetUser.Id);

            return new PayoutBatchCreateViewModel
            {
                Reference = $"PAYOUT-{DateTime.UtcNow:yyyyMMddHHmmss}",
                BeneficiaryId = targetUser.Id,
                BeneficiaryName = string.IsNullOrWhiteSpace(targetUser.FullName) ? (targetUser.Email ?? targetUser.Id) : targetUser.FullName,
                Items = items.Select(item => new PayoutSelectionItemViewModel
                {
                    ItemType = item.ItemType,
                    SourceId = item.SourceId,
                    Description = item.Description,
                    OutstandingAmount = item.OutstandingAmount,
                    SelectedAmount = item.OutstandingAmount
                }).ToList()
            };
        }

        private static string? TryGetMapping(string json, string key)
        {
            try
            {
                var mappings = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                return mappings != null && mappings.TryGetValue(key, out var value) ? value : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
