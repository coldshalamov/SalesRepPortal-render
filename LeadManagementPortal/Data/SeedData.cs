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

            // Demo / sandbox seed — only runs when SEED_DEMO_DATA=true
            var seedDemo = string.Equals(
                configuration["SeedDemoData"] ?? Environment.GetEnvironmentVariable("SEED_DEMO_DATA"),
                "true",
                StringComparison.OrdinalIgnoreCase);

            if (seedDemo)
            {
                await SeedDemoData(db, userManager, logger);
            }
        }

        private static async Task SeedDemoData(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ILogger logger)
        {
            const string demoGroupId = "demo-group-001";

            // 1. SalesGroup
            if (!await db.SalesGroups.AnyAsync(g => g.Id == demoGroupId))
            {
                db.SalesGroups.Add(new SalesGroup
                {
                    Id = demoGroupId,
                    Name = "Demo Group",
                    Description = "Sandbox demo group — safe to delete",
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }

            // 2. SalesOrg (int PK — find-or-create by name)
            var demoOrg = await db.SalesOrgs.FirstOrDefaultAsync(o => o.Name == "Demo Org");
            if (demoOrg == null)
            {
                demoOrg = new SalesOrg { Name = "Demo Org", SalesGroupId = demoGroupId };
                db.SalesOrgs.Add(demoOrg);
                await db.SaveChangesAsync(); // flush to get the auto-assigned Id
            }

            // 3. GroupAdmin user
            var groupAdmin = await userManager.FindByEmailAsync("groupadmin@demo.com");
            if (groupAdmin == null)
            {
                groupAdmin = new ApplicationUser
                {
                    UserName = "groupadmin@demo.com",
                    Email = "groupadmin@demo.com",
                    FirstName = "Demo",
                    LastName = "GroupAdmin",
                    EmailConfirmed = true,
                    IsActive = true,
                    SalesGroupId = demoGroupId
                };
                var result = await userManager.CreateAsync(groupAdmin, "GroupAdmin@123");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(groupAdmin, UserRoles.GroupAdmin);
                else
                    logger.LogWarning("Demo GroupAdmin seed failed: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
            }

            // 4. Wire GroupAdmin back onto the SalesGroup
            var demoGroup = await db.SalesGroups.FindAsync(demoGroupId);
            if (demoGroup != null && demoGroup.GroupAdminId != groupAdmin.Id)
            {
                demoGroup.GroupAdminId = groupAdmin.Id;
                await db.SaveChangesAsync();
            }

            // 5. SalesOrgAdmin user
            var orgAdmin = await userManager.FindByEmailAsync("orgadmin@demo.com");
            if (orgAdmin == null)
            {
                orgAdmin = new ApplicationUser
                {
                    UserName = "orgadmin@demo.com",
                    Email = "orgadmin@demo.com",
                    FirstName = "Demo",
                    LastName = "OrgAdmin",
                    EmailConfirmed = true,
                    IsActive = true,
                    SalesGroupId = demoGroupId,
                    SalesOrgId = demoOrg.Id
                };
                var result = await userManager.CreateAsync(orgAdmin, "OrgAdmin@123");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(orgAdmin, UserRoles.SalesOrgAdmin);
                else
                    logger.LogWarning("Demo OrgAdmin seed failed: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
            }

            // 6. SalesRep user
            var salesRep = await userManager.FindByEmailAsync("rep@demo.com");
            if (salesRep == null)
            {
                salesRep = new ApplicationUser
                {
                    UserName = "rep@demo.com",
                    Email = "rep@demo.com",
                    FirstName = "Demo",
                    LastName = "Rep",
                    EmailConfirmed = true,
                    IsActive = true,
                    SalesGroupId = demoGroupId,
                    SalesOrgId = demoOrg.Id
                };
                var result = await userManager.CreateAsync(salesRep, "SalesRep@123");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(salesRep, UserRoles.SalesRep);
                else
                    logger.LogWarning("Demo SalesRep seed failed: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
            }

            logger.LogInformation("Demo seed complete: group={Group}, org={Org}", demoGroupId, demoOrg.Id);
        }
    }
}
