using LeadManagementPortal.Models;
using Microsoft.EntityFrameworkCore;
using LeadManagementPortal.Data;
using Microsoft.AspNetCore.Identity;

namespace LeadManagementPortal.Data
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var env = serviceProvider.GetRequiredService<IHostEnvironment>();
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData");

            var roleManager = serviceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var db = serviceProvider.GetRequiredService<ApplicationDbContext>();

            // Ensure database is up to date
            await db.Database.MigrateAsync();

            // Create Roles
            string[] roleNames = { UserRoles.OrganizationAdmin, UserRoles.GroupAdmin, UserRoles.SalesRep, UserRoles.SalesOrgAdmin };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new ApplicationRole
                    {
                        Name = roleName,
                        Description = $"{roleName} role"
                    });
                }
            }

            // Create Default Organization Admin
            var adminEmail = configuration["SeedAdmin:Email"]?.Trim();
            if (string.IsNullOrWhiteSpace(adminEmail))
                adminEmail = "admin@dirxhealth.com";

            var adminPassword = configuration["SeedAdmin:Password"];
            if (string.IsNullOrWhiteSpace(adminPassword))
            {
                adminPassword = "Admin@123";
                if (env.IsProduction())
                {
                    logger.LogWarning("SeedAdmin:Password not set; using default admin password.");
                }
            }

            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FirstName = "System",
                    LastName = "Administrator",
                    EmailConfirmed = true,
                    IsActive = true
                };

                var result = await userManager.CreateAsync(adminUser, adminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, UserRoles.OrganizationAdmin);
                }
                else
                {
                    logger.LogWarning("Failed creating seeded admin user: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
                }
            }

            // Seed default Products
            var defaultProducts = new[] { "GLP1", "Peptides", "Generics", "TRT", "OTC" };
            foreach (var name in defaultProducts)
            {
                var exists = await db.Products.AnyAsync(p => p.Name == name);
                if (!exists)
                {
                    db.Products.Add(new Product { Name = name, IsActive = true });
                }
            }

            await db.SaveChangesAsync();

            // Ensure System Settings row exists
            if (!await db.Settings.AnyAsync(s => s.Id == 1))
            {
                db.Settings.Add(new SystemSettings
                {
                    Id = 1,
                    CoolingPeriodDays = 15,
                    LeadInitialExpiryDays = 15,
                    LeadExtensionDays = 5
                });
                await db.SaveChangesAsync();
            }
        }
    }
}
