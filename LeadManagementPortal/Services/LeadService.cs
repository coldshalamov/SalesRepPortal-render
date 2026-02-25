using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeadManagementPortal.Services
{
    public class LeadService : ILeadService
    {
        private readonly ApplicationDbContext _context;
        private readonly ISettingsService _settingsService;
        private readonly ICustomerService _customerService;
        private readonly ILeadDocumentService _leadDocumentService;
        private readonly ILogger<LeadService> _logger;

        public LeadService(ApplicationDbContext context, ICustomerService customerService, ISettingsService settingsService, ILeadDocumentService leadDocumentService)
            : this(context, customerService, settingsService, leadDocumentService, NullLogger<LeadService>.Instance)
        {
        }

        public LeadService(
            ApplicationDbContext context,
            ICustomerService customerService,
            ISettingsService settingsService,
            ILeadDocumentService leadDocumentService,
            ILogger<LeadService> logger)
        {
            _context = context;
            _customerService = customerService;
            _settingsService = settingsService;
            _leadDocumentService = leadDocumentService;
            _logger = logger;
        }

        public async Task<Lead?> GetByIdAsync(string id)
        {
            return await _context.Leads
                .Include(l => l.AssignedTo).ThenInclude(u => u!.SalesOrg)
                .Include(l => l.SalesGroup)
                .Include(l => l.Products)
                .Include(l => l.CreatedBy)
                .FirstOrDefaultAsync(l => l.Id == id);
        }

        public async Task<IEnumerable<Lead>> GetAllAsync()
        {
            return await _context.Leads
                .Include(l => l.AssignedTo).ThenInclude(u => u!.SalesOrg)
                .Include(l => l.SalesGroup)
                .OrderByDescending(l => l.CreatedDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<Lead>> GetByUserAsync(string userId, string userRole)
        {
            var query = _context.Leads
                .Include(l => l.AssignedTo).ThenInclude(u => u!.SalesOrg)
                .Include(l => l.SalesGroup)
                .AsQueryable();

            if (userRole == UserRoles.OrganizationAdmin)
            {
                // Organization admin sees all leads
                return await query.OrderByDescending(l => l.CreatedDate).ToListAsync();
            }

            var user = await _context.Users.FindAsync(userId);

            if (userRole == UserRoles.GroupAdmin)
            {
                // Group admin sees all leads in their group
                if (user?.SalesGroupId != null)
                {
                    query = query.Where(l => l.SalesGroupId == user.SalesGroupId);
                }
                else
                {
                    return new List<Lead>();
                }
            }
            else if (userRole == UserRoles.SalesOrgAdmin)
            {
                // Sales Org Admin sees all leads in their org
                if (user?.SalesOrgId != null)
                {
                    query = query.Where(l => l.AssignedTo != null && l.AssignedTo.SalesOrgId == user.SalesOrgId);
                }
                else
                {
                    return new List<Lead>();
                }
            }
            else if (userRole == UserRoles.SalesRep)
            {
                // Sales rep sees only their assigned leads
                query = query.Where(l => l.AssignedToId == userId);
            }
            else
            {
                // Unknown role or no role - return empty
                return new List<Lead>();
            }

            return await query.OrderByDescending(l => l.CreatedDate).ToListAsync();
        }

        public async Task<IEnumerable<Lead>> GetBySalesGroupAsync(string salesGroupId)
        {
            return await _context.Leads
                .Include(l => l.AssignedTo).ThenInclude(u => u!.SalesOrg)
                .Include(l => l.SalesGroup)
                .Where(l => l.SalesGroupId == salesGroupId)
                .OrderByDescending(l => l.CreatedDate)
                .ToListAsync();
        }

        public async Task<bool> CreateAsync(Lead lead)
        {
            // Safety: ensure an assignee exists. If none provided, default to creator
            if (string.IsNullOrWhiteSpace(lead.AssignedToId))
            {
                lead.AssignedToId = lead.CreatedById;
            }

            // If SalesGroupId not provided, try to infer from assignee
            if (string.IsNullOrWhiteSpace(lead.SalesGroupId) && !string.IsNullOrWhiteSpace(lead.AssignedToId))
            {
                var assignee = await _context.Users.FirstOrDefaultAsync(u => u.Id == lead.AssignedToId);
                if (assignee != null && !string.IsNullOrWhiteSpace(assignee.SalesGroupId))
                {
                    lead.SalesGroupId = assignee.SalesGroupId;
                }
            }

            var settings = await _settingsService.GetAsync();
            // Set expiry date based on settings
            lead.ExpiryDate = DateTime.UtcNow.AddDays(settings.LeadInitialExpiryDays);
            lead.CreatedDate = DateTime.UtcNow;

            _context.Leads.Add(lead);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateAsync(Lead lead)
        {
            try
            {
                var existingLead = _context.Leads.Local.FirstOrDefault(l => l.Id == lead.Id);

                if (existingLead == null)
                {
                    existingLead = await _context.Leads
                        .Include(l => l.Products)
                        .FirstOrDefaultAsync(l => l.Id == lead.Id);
                }
                else
                {
                    var entry = _context.Entry(existingLead);
                    if (!entry.Collection(l => l.Products).IsLoaded)
                    {
                        await entry.Collection(l => l.Products).LoadAsync();
                    }
                }

                if (existingLead == null) return false;

                _context.Entry(existingLead).CurrentValues.SetValues(lead);

                if (lead.Products != null)
                {
                    var newProductIds = lead.Products.Select(p => p.Id).ToHashSet();
                    var productsToRemove = existingLead.Products.Where(p => !newProductIds.Contains(p.Id)).ToList();
                    foreach (var p in productsToRemove)
                    {
                        existingLead.Products.Remove(p);
                    }

                    var existingProductIds = existingLead.Products.Select(p => p.Id).ToHashSet();
                    var productsToAdd = lead.Products.Where(p => !existingProductIds.Contains(p.Id)).ToList();
                    foreach (var p in productsToAdd)
                    {
                        existingLead.Products.Add(p);
                    }
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LeadService.UpdateAsync failed for LeadId={LeadId}", lead.Id);
                return false;
            }
        }

        public async Task<bool> DeleteAsync(string id)
        {
            try
            {
                var lead = await _context.Leads.FindAsync(id);
                if (lead == null) return false;

                // Ensure storage-backed documents are removed before the lead is deleted (DB cascade only cleans rows).
                await _leadDocumentService.DeleteForLeadAsync(id);

                _context.Leads.Remove(lead);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LeadService.DeleteAsync failed for LeadId={LeadId}", id);
                return false;
            }
        }

        public async Task<bool> IsPhoneOrEmailExistsAsync(string phone, string email, string? excludeLeadId = null)
        {
            // Kept for backward compatibility; currently unused after switching to Company-based checks
            var query = _context.Leads
                .Where(l => (l.Phone == phone || l.Email == email) && !l.IsExpired && l.Status != LeadStatus.Converted);

            if (!string.IsNullOrEmpty(excludeLeadId))
            {
                query = query.Where(l => l.Id != excludeLeadId);
            }

            return await query.AnyAsync();
        }

        public async Task<bool> CanRegisterLeadAsync(string? company, string? address = null, string? city = null, string? state = null, string? zip = null, string? excludeLeadId = null)
        {
            var normalizedCompany = (company ?? string.Empty).Trim().ToLower();
            var nAddr = (address ?? string.Empty).Trim().ToLower();
            var nCity = (city ?? string.Empty).Trim().ToLower();
            var nState = (state ?? string.Empty).Trim().ToLower();
            var nZip = (zip ?? string.Empty).Trim().ToLower();
            
            // Use only first 5 characters of ZipCode for comparison
            if (nZip.Length > 5)
            {
                nZip = nZip.Substring(0, 5);
            }

            var activeLeadExists = await _context.Leads
                .AnyAsync(l => !l.IsExpired
                               && l.Status != LeadStatus.Converted
                               && (excludeLeadId == null || l.Id != excludeLeadId)
                               && (
                                   (!string.IsNullOrEmpty(normalizedCompany) && l.Company != null && l.Company.ToLower() == normalizedCompany)
                                   ||
                                   (!string.IsNullOrEmpty(nAddr) && l.Address != null && l.Address.ToLower() == nAddr
                                        && (string.IsNullOrEmpty(nCity) || (l.City != null && l.City.ToLower() == nCity))
                                        && (string.IsNullOrEmpty(nState) || (l.State != null && l.State.ToLower() == nState))
                                        && (string.IsNullOrEmpty(nZip) || (l.ZipCode != null && (l.ZipCode.Length >= 5 ? l.ZipCode.Substring(0, 5) : l.ZipCode).ToLower() == nZip))
                                   )
                               ));
            if (activeLeadExists)
                return false;

            var settings = await _settingsService.GetAsync();
            var recentCustomerExists = await _context.Customers
                .AnyAsync(c => c.ConversionDate >= DateTime.UtcNow.AddDays(-settings.CoolingPeriodDays)
                               && (
                                   (!string.IsNullOrEmpty(normalizedCompany) && c.Company != null && c.Company.ToLower() == normalizedCompany)
                                   ||
                                   (!string.IsNullOrEmpty(nAddr) && c.Address != null && c.Address.ToLower() == nAddr
                                        && (string.IsNullOrEmpty(nCity) || (c.City != null && c.City.ToLower() == nCity))
                                        && (string.IsNullOrEmpty(nState) || (c.State != null && c.State.ToLower() == nState))
                                        && (string.IsNullOrEmpty(nZip) || (c.ZipCode != null && (c.ZipCode.Length >= 5 ? c.ZipCode.Substring(0, 5) : c.ZipCode).ToLower() == nZip))
                                   )
                               ));

            return !recentCustomerExists;
        }

        public async Task<bool> CanRegisterLeadForGroupAsync(string? company, string? salesGroupId, string? address = null, string? city = null, string? state = null, string? zip = null)
        {
            var normalizedCompany = (company ?? string.Empty).Trim().ToLower();
            var nAddr = (address ?? string.Empty).Trim().ToLower();
            var nCity = (city ?? string.Empty).Trim().ToLower();
            var nState = (state ?? string.Empty).Trim().ToLower();
            var nZip = (zip ?? string.Empty).Trim().ToLower();

            // Use only first 5 characters of ZipCode for comparison (keep consistent with CanRegisterLeadAsync)
            if (nZip.Length > 5)
            {
                nZip = nZip.Substring(0, 5);
            }

            var conflicts = await _context.Leads
                .Where(l => !l.IsExpired
                            && l.Status != LeadStatus.Converted
                            && (
                                (!string.IsNullOrEmpty(normalizedCompany) && l.Company != null && l.Company.ToLower() == normalizedCompany)
                                &&
                                (
                                    string.IsNullOrEmpty(nAddr)
                                    ||
                                     (!string.IsNullOrEmpty(nAddr) && l.Address != null && l.Address.ToLower() == nAddr
                                         && (string.IsNullOrEmpty(nCity) || (l.City != null && l.City.ToLower() == nCity))
                                         && (string.IsNullOrEmpty(nState) || (l.State != null && l.State.ToLower() == nState))
                                         && (string.IsNullOrEmpty(nZip) || (l.ZipCode != null && (l.ZipCode.Length >= 5 ? l.ZipCode.Substring(0, 5) : l.ZipCode).ToLower() == nZip))
                                     )
                                 )
                             ))
                .Select(l => new { l.Status, l.SalesGroupId })
                .ToListAsync();

            if (conflicts.Count == 0)
            {
                // Fall back to customer recent conversion rule
                var settings2 = await _settingsService.GetAsync();
                var recentCustomerExists = await _context.Customers
                    .AnyAsync(c => c.ConversionDate >= DateTime.UtcNow.AddDays(-settings2.CoolingPeriodDays)
                                   && (
                                        (!string.IsNullOrEmpty(normalizedCompany) && c.Company != null && c.Company.ToLower() == normalizedCompany)
                                        &&
                                        (
                                            string.IsNullOrEmpty(nAddr)
                                            ||
                                             (!string.IsNullOrEmpty(nAddr) && c.Address != null && c.Address.ToLower() == nAddr
                                                 && (string.IsNullOrEmpty(nCity) || (c.City != null && c.City.ToLower() == nCity))
                                                 && (string.IsNullOrEmpty(nState) || (c.State != null && c.State.ToLower() == nState))
                                                 && (string.IsNullOrEmpty(nZip) || (c.ZipCode != null && (c.ZipCode.Length >= 5 ? c.ZipCode.Substring(0, 5) : c.ZipCode).ToLower() == nZip))
                                             )
                                         )
                                    ));
                return !recentCustomerExists;
            }

            // If there are any conflicts with a status other than Lost, block globally (existing rule)
            if (conflicts.Any(c => c.Status != LeadStatus.Lost))
            {
                return false;
            }

            // All conflicts are Lost. Enforce group-specific rule:
            // - Same group cannot create a new lead for this company
            // - Different groups can
            var sameGroupLostExists = conflicts.Any(c => c.SalesGroupId == salesGroupId);
            if (sameGroupLostExists)
            {
                return false;
            }

            // No Lost lead in the same group; allow
            return true;
        }

        public async Task<bool> ConvertToCustomerAsync(string leadId, string userId)
        {
            try
            {
                var lead = await GetByIdAsync(leadId);
                if (lead == null || lead.Status == LeadStatus.Converted || lead.Status == LeadStatus.Lost || lead.IsExpired)
                    return false;

                // Create customer from lead
                var customer = new Customer
                {
                    FirstName = lead.FirstName,
                    LastName = lead.LastName,
                    Email = lead.Email,
                    Phone = lead.Phone,
                    Company = lead.Company,
                    Address = lead.Address,
                    City = lead.City,
                    State = lead.State,
                    ZipCode = lead.ZipCode,
                    Notes = lead.Notes,
                    ConvertedById = userId,
                    SalesRepId = lead.AssignedToId,
                    SalesGroupId = lead.SalesGroupId,
                    ConversionDate = DateTime.UtcNow,
                    OriginalLeadId = lead.Id,
                    LeadCreatedDate = lead.CreatedDate,
                    DaysToConvert = (DateTime.UtcNow - lead.CreatedDate).Days
                };

                await _customerService.CreateAsync(customer);

                // Update lead status
                lead.Status = LeadStatus.Converted;
                lead.ConvertedDate = DateTime.UtcNow;
                await UpdateAsync(lead);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> GrantExtensionAsync(string leadId, string grantedBy)
        {
            try
            {
                var lead = await GetByIdAsync(leadId);
                // Allow extending Expired leads, but not Converted or Lost
                if (lead == null || lead.Status == LeadStatus.Converted || lead.Status == LeadStatus.Lost)
                    return false;

                // One-time extension: treat either flag as authoritative for legacy compatibility.
                if (lead.IsExtended || lead.ExtensionGrantedDate != null)
                {
                    return false;
                }

                var s = await _settingsService.GetAsync();
                var previousExpiry = lead.ExpiryDate;
                
                // If lead was expired, reset status and calculate new expiry from now
                if (lead.IsExpired || lead.Status == LeadStatus.Expired)
                {
                    lead.IsExpired = false;
                    lead.Status = LeadStatus.New; // Reset to New or appropriate active status
                    lead.ExpiryDate = DateTime.UtcNow.AddDays(s.LeadExtensionDays);
                }
                else
                {
                    // If not expired yet, add to existing expiry
                    lead.ExpiryDate = lead.ExpiryDate.AddDays(s.LeadExtensionDays);
                }

                lead.ExtensionGrantedDate = DateTime.UtcNow;
                lead.ExtensionGrantedBy = grantedBy;
                lead.IsExtended = true;

                _context.LeadExtensionAudits.Add(new LeadExtensionAudit
                {
                    LeadId = lead.Id,
                    GrantedById = grantedBy,
                    GrantedAtUtc = DateTime.UtcNow,
                    DaysAdded = s.LeadExtensionDays,
                    PreviousExpiry = previousExpiry,
                    NewExpiry = lead.ExpiryDate
                });

                await UpdateAsync(lead);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task ExpireOldLeadsAsync()
        {
            var expiredLeads = await _context.Leads
                .Where(l => l.ExpiryDate <= DateTime.UtcNow 
                    && !l.IsExpired 
                    && l.Status != LeadStatus.Converted)
                .ToListAsync();

            foreach (var lead in expiredLeads)
            {
                lead.IsExpired = true;
                lead.Status = LeadStatus.Expired;
            }

            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<Lead>> SearchAsync(string searchTerm, string userId, string userRole)
        {
            var query = _context.Leads
                .Include(l => l.AssignedTo).ThenInclude(u => u!.SalesOrg)
                .Include(l => l.SalesGroup)
                .AsQueryable();

            // Apply role-based filtering
            if (userRole == UserRoles.OrganizationAdmin)
            {
                // No filtering needed
            }
            else if (userRole == UserRoles.GroupAdmin)
            {
                var user = await _context.Users.FindAsync(userId);
                if (user?.SalesGroupId != null)
                {
                    query = query.Where(l => l.SalesGroupId == user.SalesGroupId);
                }
                else
                {
                    return new List<Lead>();
                }
            }
            else if (userRole == UserRoles.SalesOrgAdmin)
            {
                var user = await _context.Users.FindAsync(userId);
                if (user?.SalesOrgId != null)
                {
                    query = query.Where(l => l.AssignedTo != null && l.AssignedTo.SalesOrgId == user.SalesOrgId);
                }
                else
                {
                    return new List<Lead>();
                }
            }
            else if (userRole == UserRoles.SalesRep)
            {
                query = query.Where(l => l.AssignedToId == userId);
            }
            else
            {
                return new List<Lead>();
            }

            // Apply search filter (company + contact info + address + rep email + notes)
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                query = query.Where(l =>
                    l.FirstName.ToLower().Contains(searchTerm) ||
                    l.LastName.ToLower().Contains(searchTerm) ||
                    l.Email.ToLower().Contains(searchTerm) ||
                    l.Phone.ToLower().Contains(searchTerm) ||
                    (l.Company != null && l.Company.ToLower().Contains(searchTerm)) ||
                    (l.Address != null && l.Address.ToLower().Contains(searchTerm)) ||
                    (l.City != null && l.City.ToLower().Contains(searchTerm)) ||
                    (l.State != null && l.State.ToLower().Contains(searchTerm)) ||
                    (l.ZipCode != null && l.ZipCode.ToLower().Contains(searchTerm)) ||
                    (l.Notes != null && l.Notes.ToLower().Contains(searchTerm)) ||
                    (l.AssignedTo != null && l.AssignedTo.Email != null && l.AssignedTo.Email.ToLower().Contains(searchTerm))
                );
            }

            return await query.OrderByDescending(l => l.CreatedDate).ToListAsync();
        }

        public async Task<IEnumerable<Lead>> SearchTopAsync(string searchTerm, string userId, string userRole, int maxResults)
        {
            if (maxResults <= 0)
            {
                return new List<Lead>();
            }

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return new List<Lead>();
            }

            var query = _context.Leads
                .AsNoTracking()
                .AsQueryable();

            // Apply role-based filtering
            if (userRole == UserRoles.OrganizationAdmin)
            {
                // No filtering needed
            }
            else if (userRole == UserRoles.GroupAdmin)
            {
                var salesGroupId = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => u.SalesGroupId)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrWhiteSpace(salesGroupId))
                {
                    return new List<Lead>();
                }

                query = query.Where(l => l.SalesGroupId == salesGroupId);
            }
            else if (userRole == UserRoles.SalesOrgAdmin)
            {
                var salesOrgId = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => u.SalesOrgId)
                    .FirstOrDefaultAsync();

                if (!salesOrgId.HasValue)
                {
                    return new List<Lead>();
                }

                query = query.Where(l => l.AssignedTo != null && l.AssignedTo.SalesOrgId == salesOrgId.Value);
            }
            else if (userRole == UserRoles.SalesRep)
            {
                query = query.Where(l => l.AssignedToId == userId);
            }
            else
            {
                return new List<Lead>();
            }

            // Apply search filter (company + contact info + address + rep email + notes)
            var term = searchTerm.Trim().ToLowerInvariant();
            query = query.Where(l =>
                l.FirstName.ToLower().Contains(term) ||
                l.LastName.ToLower().Contains(term) ||
                l.Email.ToLower().Contains(term) ||
                l.Phone.ToLower().Contains(term) ||
                (l.Company != null && l.Company.ToLower().Contains(term)) ||
                (l.Address != null && l.Address.ToLower().Contains(term)) ||
                (l.City != null && l.City.ToLower().Contains(term)) ||
                (l.State != null && l.State.ToLower().Contains(term)) ||
                (l.ZipCode != null && l.ZipCode.ToLower().Contains(term)) ||
                (l.Notes != null && l.Notes.ToLower().Contains(term)) ||
                (l.AssignedTo != null && l.AssignedTo.Email != null && l.AssignedTo.Email.ToLower().Contains(term))
            );

            return await query
                .OrderByDescending(l => l.CreatedDate)
                .Take(maxResults)
                .ToListAsync();
        }

        public async Task<Dictionary<string, List<LeadFollowUpTask>>> GetFollowUpsForLeadsAsync(IEnumerable<string> leadIds, string userId, string userRole)
        {
            var result = new Dictionary<string, List<LeadFollowUpTask>>();
            var ids = leadIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList() ?? new List<string>();
            if (!ids.Any())
            {
                return result;
            }

            try
            {
                var scopedIds = await GetScopedLeadIdsAsync(userId, userRole);
                var allowedIds = ids.Where(id => scopedIds.Contains(id)).ToList();
                if (!allowedIds.Any())
                {
                    return result;
                }

                var tasks = await _context.LeadFollowUpTasks
                    .Where(task => allowedIds.Contains(task.LeadId))
                    .OrderBy(task => task.IsCompleted)
                    .ThenBy(task => task.DueDate)
                    .ThenBy(task => task.CreatedAt)
                    .ToListAsync();

                result = tasks
                    .GroupBy(task => task.LeadId)
                    .ToDictionary(group => group.Key, group => group.ToList());

                return result;
            }
            catch
            {
                return result;
            }
        }

        public async Task<List<LeadFollowUpTask>> GetFollowUpsForLeadAsync(string leadId, string userId, string userRole)
        {
            if (string.IsNullOrWhiteSpace(leadId))
            {
                return new List<LeadFollowUpTask>();
            }

            try
            {
                if (!await CanUserAccessLeadAsync(leadId, userId, userRole))
                {
                    return new List<LeadFollowUpTask>();
                }

                return await _context.LeadFollowUpTasks
                    .Where(task => task.LeadId == leadId)
                    .OrderBy(task => task.IsCompleted)
                    .ThenBy(task => task.DueDate)
                    .ThenBy(task => task.CreatedAt)
                    .ToListAsync();
            }
            catch
            {
                return new List<LeadFollowUpTask>();
            }
        }

        public async Task<LeadFollowUpTask?> AddFollowUpAsync(string leadId, string userId, string userRole, string type, string description, DateTime? dueDate)
        {
            if (string.IsNullOrWhiteSpace(leadId) || string.IsNullOrWhiteSpace(description))
            {
                return null;
            }

            try
            {
                if (!await CanUserAccessLeadAsync(leadId, userId, userRole))
                {
                    return null;
                }

                var normalizedType = string.IsNullOrWhiteSpace(type) ? "call" : type.Trim().ToLowerInvariant();
                var task = new LeadFollowUpTask
                {
                    LeadId = leadId,
                    Type = normalizedType.Length > 32 ? normalizedType.Substring(0, 32) : normalizedType,
                    Description = description.Trim().Length > 500 ? description.Trim().Substring(0, 500) : description.Trim(),
                    DueDate = dueDate?.Date,
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = userId
                };

                _context.LeadFollowUpTasks.Add(task);
                await _context.SaveChangesAsync();
                return task;
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> CompleteFollowUpAsync(string leadId, int followUpId, string userId, string userRole)
        {
            if (string.IsNullOrWhiteSpace(leadId) || followUpId <= 0)
            {
                return false;
            }

            try
            {
                if (!await CanUserAccessLeadAsync(leadId, userId, userRole))
                {
                    return false;
                }

                var task = await _context.LeadFollowUpTasks
                    .FirstOrDefaultAsync(item => item.Id == followUpId && item.LeadId == leadId);
                if (task == null)
                {
                    return false;
                }

                if (!task.IsCompleted)
                {
                    task.IsCompleted = true;
                    task.CompletedAt = DateTime.UtcNow;
                    task.CompletedById = userId;
                    await _context.SaveChangesAsync();
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<int> DeleteFollowUpsAsync(string leadId, IEnumerable<int> followUpIds, string userId, string userRole)
        {
            if (string.IsNullOrWhiteSpace(leadId))
            {
                return 0;
            }

            var ids = followUpIds?.Distinct().Where(id => id > 0).ToList() ?? new List<int>();
            if (!ids.Any())
            {
                return 0;
            }

            try
            {
                if (!await CanUserAccessLeadAsync(leadId, userId, userRole))
                {
                    return 0;
                }

                var tasks = await _context.LeadFollowUpTasks
                    .Where(task => task.LeadId == leadId && ids.Contains(task.Id))
                    .ToListAsync();
                if (!tasks.Any())
                {
                    return 0;
                }

                _context.LeadFollowUpTasks.RemoveRange(tasks);
                await _context.SaveChangesAsync();
                return tasks.Count;
            }
            catch
            {
                return 0;
            }
        }

        public async Task<int> GetOverdueFollowUpCountAsync(string userId, string userRole)
        {
            try
            {
                var scopedIds = await GetScopedLeadIdsAsync(userId, userRole);
                if (!scopedIds.Any())
                {
                    return 0;
                }

                var today = DateTime.UtcNow.Date;
                return await _context.LeadFollowUpTasks
                    .Where(task => scopedIds.Contains(task.LeadId) && !task.IsCompleted && task.DueDate.HasValue && task.DueDate.Value.Date < today)
                    .CountAsync();
            }
            catch
            {
                return 0;
            }
        }

        private async Task<HashSet<string>> GetScopedLeadIdsAsync(string userId, string userRole)
        {
            if (userRole == UserRoles.OrganizationAdmin)
            {
                return (await _context.Leads.Select(lead => lead.Id).ToListAsync()).ToHashSet();
            }

            var query = _context.Leads.AsQueryable();
            var user = await _context.Users.FindAsync(userId);

            if (userRole == UserRoles.GroupAdmin)
            {
                if (user?.SalesGroupId == null)
                {
                    return new HashSet<string>();
                }

                query = query.Where(lead => lead.SalesGroupId == user.SalesGroupId);
            }
            else if (userRole == UserRoles.SalesOrgAdmin)
            {
                if (user?.SalesOrgId == null)
                {
                    return new HashSet<string>();
                }

                query = query.Where(lead => lead.AssignedTo != null && lead.AssignedTo.SalesOrgId == user.SalesOrgId);
            }
            else if (userRole == UserRoles.SalesRep)
            {
                query = query.Where(lead => lead.AssignedToId == userId);
            }
            else
            {
                return new HashSet<string>();
            }

            return (await query.Select(lead => lead.Id).ToListAsync()).ToHashSet();
        }

        private async Task<bool> CanUserAccessLeadAsync(string leadId, string userId, string userRole)
        {
            var scopedLeadIds = await GetScopedLeadIdsAsync(userId, userRole);
            return scopedLeadIds.Contains(leadId);
        }
    }
}
