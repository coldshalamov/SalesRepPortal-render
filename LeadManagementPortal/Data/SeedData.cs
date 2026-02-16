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

            var seedDemoUsersEnabled = configuration.GetValue<bool>("SeedDemoUsers:Enabled");
            if (seedDemoUsersEnabled)
            {
                var demoPassword = configuration["SeedDemoUsers:Password"];
                if (string.IsNullOrWhiteSpace(demoPassword))
                {
                    demoPassword = "Demo123";
                    if (env.IsProduction())
                    {
                        logger.LogWarning("SeedDemoUsers:Password not set; using default demo password.");
                    }
                }

                var demoDomain = configuration["SeedDemoUsers:Domain"]?.Trim();
                if (string.IsNullOrWhiteSpace(demoDomain))
                    demoDomain = "demo.local";

                var demoGroupName = configuration["SeedDemoUsers:SalesGroupName"]?.Trim();
                if (string.IsNullOrWhiteSpace(demoGroupName))
                    demoGroupName = "Demo Sales Group";

                var demoOrgName = configuration["SeedDemoUsers:SalesOrgName"]?.Trim();
                if (string.IsNullOrWhiteSpace(demoOrgName))
                    demoOrgName = "Demo Sales Org";

                var group = await db.SalesGroups.FirstOrDefaultAsync(g => g.Name == demoGroupName);
                if (group == null)
                {
                    group = new SalesGroup
                    {
                        Name = demoGroupName,
                        Description = "Seeded demo group (non-production).",
                        IsActive = true
                    };
                    db.SalesGroups.Add(group);
                    await db.SaveChangesAsync();
                }

                var salesOrg = await db.SalesOrgs.FirstOrDefaultAsync(o => o.Name == demoOrgName && o.SalesGroupId == group.Id);
                if (salesOrg == null)
                {
                    salesOrg = new SalesOrg
                    {
                        Name = demoOrgName,
                        SalesGroupId = group.Id
                    };
                    db.SalesOrgs.Add(salesOrg);
                    await db.SaveChangesAsync();
                }

                async Task<ApplicationUser?> EnsureUserAsync(string email, string firstName, string lastName, string? salesGroupId, int? salesOrgId, string role)
                {
                    var user = await userManager.FindByEmailAsync(email);
                    if (user == null)
                    {
                        user = new ApplicationUser
                        {
                            UserName = email,
                            Email = email,
                            FirstName = firstName,
                            LastName = lastName,
                            EmailConfirmed = true,
                            IsActive = true,
                            SalesGroupId = salesGroupId,
                            SalesOrgId = salesOrgId
                        };

                        var create = await userManager.CreateAsync(user, demoPassword);
                        if (!create.Succeeded)
                        {
                            logger.LogWarning("Failed creating demo user {Email}: {Errors}", email, string.Join("; ", create.Errors.Select(e => e.Description)));
                            return null;
                        }
                    }
                    else
                    {
                        var changed = false;
                        if (user.UserName != email) { user.UserName = email; changed = true; }
                        if (user.FirstName != firstName) { user.FirstName = firstName; changed = true; }
                        if (user.LastName != lastName) { user.LastName = lastName; changed = true; }
                        if (user.SalesGroupId != salesGroupId) { user.SalesGroupId = salesGroupId; changed = true; }
                        if (user.SalesOrgId != salesOrgId) { user.SalesOrgId = salesOrgId; changed = true; }
                        if (!user.EmailConfirmed) { user.EmailConfirmed = true; changed = true; }
                        if (!user.IsActive) { user.IsActive = true; changed = true; }

                        if (changed)
                        {
                            var update = await userManager.UpdateAsync(user);
                            if (!update.Succeeded)
                            {
                                logger.LogWarning("Failed updating demo user {Email}: {Errors}", email, string.Join("; ", update.Errors.Select(e => e.Description)));
                            }
                        }
                    }

                    if (!await userManager.IsInRoleAsync(user, role))
                    {
                        var addRole = await userManager.AddToRoleAsync(user, role);
                        if (!addRole.Succeeded)
                        {
                            logger.LogWarning("Failed assigning role {Role} to demo user {Email}: {Errors}", role, email, string.Join("; ", addRole.Errors.Select(e => e.Description)));
                        }
                    }

                    return user;
                }

                var orgAdminEmail = $"orgadmin@{demoDomain}";
                var groupAdminEmail = $"groupadmin@{demoDomain}";
                var salesOrgAdminEmail = $"salesorgadmin@{demoDomain}";
                var salesRepEmail = $"salesrep@{demoDomain}";

                await EnsureUserAsync(orgAdminEmail, "Org", "Admin", null, null, UserRoles.OrganizationAdmin);
                var demoGroupAdmin = await EnsureUserAsync(groupAdminEmail, "Group", "Admin", group.Id, salesOrg.Id, UserRoles.GroupAdmin);
                await EnsureUserAsync(salesOrgAdminEmail, "SalesOrg", "Admin", group.Id, salesOrg.Id, UserRoles.SalesOrgAdmin);
                await EnsureUserAsync(salesRepEmail, "Sales", "Rep", group.Id, salesOrg.Id, UserRoles.SalesRep);

                if (demoGroupAdmin != null && group.GroupAdminId != demoGroupAdmin.Id)
                {
                    group.GroupAdminId = demoGroupAdmin.Id;
                    await db.SaveChangesAsync();
                }

                logger.LogInformation(
                    "Seeded demo users enabled. OrgAdmin={OrgAdminEmail}, GroupAdmin={GroupAdminEmail}, SalesOrgAdmin={SalesOrgAdminEmail}, SalesRep={SalesRepEmail}",
                    orgAdminEmail, groupAdminEmail, salesOrgAdminEmail, salesRepEmail);
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
