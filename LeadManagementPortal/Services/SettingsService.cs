using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using Microsoft.EntityFrameworkCore;

namespace LeadManagementPortal.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly ApplicationDbContext _context;

        public SettingsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<SystemSettings> GetAsync()
        {
            var settings = await _context.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1);
            if (settings == null)
            {
                settings = new SystemSettings();
                _context.Settings.Add(settings);
                await _context.SaveChangesAsync();
            }
            return settings;
        }

        public async Task UpdateAsync(SystemSettings settings)
        {
            var existing = await _context.Settings.FirstOrDefaultAsync(s => s.Id == 1);
            if (existing == null)
            {
                settings.Id = 1;
                _context.Settings.Add(settings);
            }
            else
            {
                existing.CoolingPeriodDays = settings.CoolingPeriodDays;
                existing.LeadInitialExpiryDays = settings.LeadInitialExpiryDays;
                existing.LeadExtensionDays = settings.LeadExtensionDays;
                _context.Settings.Update(existing);
            }
            await _context.SaveChangesAsync();
        }
    }
}
