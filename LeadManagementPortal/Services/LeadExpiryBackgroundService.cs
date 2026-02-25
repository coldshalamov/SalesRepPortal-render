using LeadManagementPortal.Models;
using LeadManagementPortal.Services;

namespace LeadManagementPortal.Services
{
    public class LeadExpiryBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<LeadExpiryBackgroundService> _logger;
        private static readonly TimeSpan WarningNotificationDedupeWindow = TimeSpan.FromHours(23);

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

                    var now = DateTime.UtcNow;

                    // Get leads expiring within 3 days (warning) before they expire
                    await SendExpiryWarningsAsync(leadService, notificationService, now);

                    // Expire overdue leads and notify
                    await ExpireLeadsAndNotifyAsync(leadService, notificationService, now);

                    // Clean up old read notifications
                    await notificationService.CleanupOldAsync(30, includeUnread: true);

                    _logger.LogInformation("Lead Expiry Background Service completed check.");
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing Lead Expiry Background Service.");
                }

                // Run every hour
                try
                {
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            _logger.LogInformation("Lead Expiry Background Service is stopping.");
        }

        private async Task SendExpiryWarningsAsync(ILeadService leadService, INotificationService notificationService, DateTime utcNow)
        {
            var warningLeads = await leadService.GetLeadsExpiringSoonAsync(utcNow, 3);
            foreach (var lead in warningLeads)
            {
                if (string.IsNullOrWhiteSpace(lead.AssignedToId))
                {
                    continue;
                }

                var daysRemaining = Math.Max(0, (lead.ExpiryDateUtc - utcNow).Days);
                if (daysRemaining <= 0)
                {
                    continue;
                }

                await notificationService.NotifyUserDedupedAsync(
                    lead.AssignedToId,
                    "lead_expiring_soon",
                    "Lead Expiring Soon",
                    $"Your lead \"{lead.Company}\" expires in {daysRemaining} day{(daysRemaining == 1 ? "" : "s")}.",
                    $"/Leads/Details/{lead.LeadId}",
                    WarningNotificationDedupeWindow
                );
            }
        }

        private async Task ExpireLeadsAndNotifyAsync(ILeadService leadService, INotificationService notificationService, DateTime utcNow)
        {
            var expiredLeads = await leadService.ExpireOldLeadsAsync(utcNow);
            foreach (var lead in expiredLeads)
            {
                if (!string.IsNullOrWhiteSpace(lead.AssignedToId))
                {
                    await notificationService.NotifyUserAsync(
                        lead.AssignedToId,
                        "lead_expired",
                        "Lead Expired",
                        $"Your lead \"{lead.Company}\" has expired and is no longer active.",
                        $"/Leads/Details/{lead.LeadId}"
                    );
                }

                // Notify org admins too
                await notificationService.NotifyRoleAsync(
                    UserRoles.OrganizationAdmin,
                    "lead_expired",
                    "Lead Expired",
                    $"Lead \"{lead.Company}\" has expired.",
                    $"/Leads/Details/{lead.LeadId}"
                );
            }
        }
    }
}
