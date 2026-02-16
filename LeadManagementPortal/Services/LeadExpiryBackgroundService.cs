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

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var leadService = scope.ServiceProvider.GetRequiredService<ILeadService>();
                        await leadService.ExpireOldLeadsAsync();
                    }

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
    }
}
