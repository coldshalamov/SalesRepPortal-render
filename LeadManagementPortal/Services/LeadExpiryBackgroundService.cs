using LeadManagementPortal.Models;
using LeadManagementPortal.Services;

namespace LeadManagementPortal.Services
{
    public class LeadExpiryBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<LeadExpiryBackgroundService> _logger;

        public LeadExpiryBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<LeadExpiryBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Lead Expiry Background Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Lead Expiry Background Service is checking for expired leads.");

                    using var scope = _serviceProvider.CreateScope();
                    var leadService = scope.ServiceProvider.GetRequiredService<ILeadService>();
                    var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                    // Get leads expiring within 3 days (warning) before they expire
                    await SendExpiryWarningsAsync(leadService, notificationService);

                    // Expire overdue leads and notify
                    await ExpireLeadsAndNotifyAsync(leadService, notificationService);

                    // Clean up old read notifications
                    await notificationService.CleanupOldAsync(30);

                    _logger.LogInformation("Lead Expiry Background Service completed check.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing Lead Expiry Background Service.");
                }

                // Run every hour
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }

            _logger.LogInformation("Lead Expiry Background Service is stopping.");
        }

        private async Task SendExpiryWarningsAsync(ILeadService leadService, INotificationService notificationService)
        {
            var allLeads = await leadService.GetAllAsync();
            var warningLeads = allLeads.Where(l =>
                !l.IsExpired &&
                l.Status != LeadStatus.Converted &&
                l.Status != LeadStatus.Lost &&
                l.Status != LeadStatus.Expired &&
                l.DaysRemaining > 0 &&
                l.DaysRemaining <= 3 &&
                !string.IsNullOrEmpty(l.AssignedToId)
            ).ToList();

            foreach (var lead in warningLeads)
            {
                await notificationService.NotifyUserAsync(
                    lead.AssignedToId,
                    "lead_expiring_soon",
                    "Lead Expiring Soon",
                    $"Your lead \"{lead.Company}\" expires in {lead.DaysRemaining} day{(lead.DaysRemaining == 1 ? "" : "s")}.",
                    $"/Leads/Details/{lead.Id}"
                );
            }
        }

        private async Task ExpireLeadsAndNotifyAsync(ILeadService leadService, INotificationService notificationService)
        {
            // Fetch leads that WILL be expired this sweep, before expiring them
            var allLeads = await leadService.GetAllAsync();
            var toExpire = allLeads.Where(l =>
                l.ExpiryDate <= DateTime.UtcNow &&
                !l.IsExpired &&
                l.Status != LeadStatus.Converted
            ).ToList();

            // Run the expiry sweep
            await leadService.ExpireOldLeadsAsync();

            // Now notify for each expired lead
            foreach (var lead in toExpire)
            {
                if (!string.IsNullOrEmpty(lead.AssignedToId))
                {
                    await notificationService.NotifyUserAsync(
                        lead.AssignedToId,
                        "lead_expired",
                        "Lead Expired",
                        $"Your lead \"{lead.Company}\" has expired and is no longer active.",
                        $"/Leads/Details/{lead.Id}"
                    );
                }

                // Notify org admins too
                await notificationService.NotifyRoleAsync(
                    UserRoles.OrganizationAdmin,
                    "lead_expired",
                    "Lead Expired",
                    $"Lead \"{lead.Company}\" has expired.",
                    $"/Leads/Details/{lead.Id}"
                );
            }
        }
    }
}
