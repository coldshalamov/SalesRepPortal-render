using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LeadManagementPortal.Tests
{
    public class LocalSeeder
    {
        [Fact]
        public async Task SeedDatabase()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            // Try to find the real appsettings.json if not in base dir
            string projectDir = AppContext.BaseDirectory;
            while (!System.IO.File.Exists(System.IO.Path.Combine(projectDir, "appsettings.json")) && System.IO.Directory.GetParent(projectDir) != null)
            {
                projectDir = System.IO.Directory.GetParent(projectDir).FullName;
            }
            
            // If we found the web project dir, use its appsettings
            var webConfigPath = System.IO.Path.Combine(projectDir, "LeadManagementPortal", "appsettings.json");
            if (System.IO.File.Exists(webConfigPath)) {
                 config = new ConfigurationBuilder()
                    .AddJsonFile(webConfigPath)
                    .Build();
            }

            var connectionString = config.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString)) return;

            var services = new ServiceCollection();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
            services.AddIdentity<ApplicationUser, ApplicationRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();
            
            services.AddLogging();
            var serviceProvider = services.BuildServiceProvider();

            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

            await db.Database.MigrateAsync();

            // 1. Ensure Roles
            string[] roleNames = { UserRoles.OrganizationAdmin, UserRoles.GroupAdmin, UserRoles.SalesRep, UserRoles.SalesOrgAdmin };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new ApplicationRole { Name = roleName, Description = roleName });
                }
            }

            // 2. Create Group and Org
            var testGroupId = "local-dev-group";
            var testGroup = await db.SalesGroups.FindAsync(testGroupId);
            if (testGroup == null)
            {
                testGroup = new SalesGroup { Id = testGroupId, Name = "Debug Group" };
                db.SalesGroups.Add(testGroup);
                await db.SaveChangesAsync();
            }

            var testOrg = await db.SalesOrgs.FirstOrDefaultAsync(o => o.Name == "Debug Org");
            if (testOrg == null)
            {
                testOrg = new SalesOrg { Name = "Debug Org", SalesGroupId = testGroupId };
                db.SalesOrgs.Add(testOrg);
                await db.SaveChangesAsync();
            }

            // 3. Create Users
            var accounts = new[]
            {
                new { Email = "org_admin@debug.local", Role = UserRoles.OrganizationAdmin, FirstName = "Org", LastName = "Admin" },
                new { Email = "sales_org_admin@debug.local", Role = UserRoles.SalesOrgAdmin, FirstName = "Org", LastName = "Boss" },
                new { Email = "group_admin@debug.local", Role = UserRoles.GroupAdmin, FirstName = "Group", LastName = "Lead" },
                new { Email = "sales_rep@debug.local", Role = UserRoles.SalesRep, FirstName = "Sales", LastName = "Rep" }
            };

            foreach (var acc in accounts)
            {
                var user = await userManager.FindByEmailAsync(acc.Email);
                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = acc.Email, Email = acc.Email, FirstName = acc.FirstName, LastName = acc.LastName,
                        EmailConfirmed = true, IsActive = true, SalesGroupId = testGroupId,
                        SalesOrgId = (acc.Role == UserRoles.SalesOrgAdmin || acc.Role == UserRoles.SalesRep) ? testOrg.Id : (int?)null
                    };
                    var res = await userManager.CreateAsync(user, "Debug@123");
                    if (res.Succeeded) await userManager.AddToRoleAsync(user, acc.Role);
                }
            }

            // 4. Create Leads
            var rep = await userManager.FindByEmailAsync("sales_rep@debug.local");
            if (rep != null && !await db.Leads.AnyAsync(l => l.AssignedToId == rep.Id))
            {
                db.Leads.Add(new Lead { 
                    FirstName = "John", LastName = "Doe", Email = "john@example.com", Phone = "555-0101", 
                    Company = "Acme Corp", AssignedToId = rep.Id, SalesGroupId = testGroupId, SalesOrgId = testOrg.Id,
                    ExpiryDate = DateTime.UtcNow.AddDays(30), CreatedById = rep.Id
                });
                await db.SaveChangesAsync();
            }
        }
    }
}
