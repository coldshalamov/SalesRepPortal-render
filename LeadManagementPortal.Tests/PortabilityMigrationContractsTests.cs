using System.Reflection;
using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using LeadManagementPortal.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LeadManagementPortal.Tests;

public class PortabilityMigrationContractsTests
{
    private static ApplicationDbContext CreateSqlServerContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=PortabilityContractDb;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public void AllMigrationTypes_ShouldHaveMigrationAttribute()
    {
        var migrationTypes = typeof(ApplicationDbContext).Assembly
            .GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(Migration).IsAssignableFrom(type))
            .ToList();

        Assert.NotEmpty(migrationTypes);

        var missingAttribute = migrationTypes
            .Where(type => type.GetCustomAttribute<MigrationAttribute>() is null)
            .Select(type => type.FullName ?? type.Name)
            .OrderBy(name => name)
            .ToList();

        Assert.True(
            missingAttribute.Count == 0,
            $"Migration types missing [Migration] attribute: {string.Join(", ", missingAttribute)}");
    }

    [Fact]
    public void LeadFollowUpTaskDbSet_ShouldHaveConcreteMigration()
    {
        var hasFollowUpDbSet = typeof(ApplicationDbContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(property =>
                property.PropertyType.IsGenericType &&
                property.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>) &&
                property.PropertyType.GetGenericArguments()[0] == typeof(LeadFollowUpTask));

        if (!hasFollowUpDbSet)
        {
            return;
        }

        using var context = CreateSqlServerContext();
        var migrations = context.Database.GetMigrations().ToList();

        Assert.Contains(
            migrations,
            migration => migration.Contains("AddLeadFollowUpTasks", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LeadsControllerNotificationDependency_ShouldHaveConcreteMigration()
    {
        var ctorNeedsNotifications = typeof(LeadManagementPortal.Controllers.LeadsController)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(ctor => ctor.GetParameters())
            .Any(parameter => parameter.ParameterType == typeof(INotificationService));

        if (!ctorNeedsNotifications)
        {
            return;
        }

        using var context = CreateSqlServerContext();
        var migrations = context.Database.GetMigrations().ToList();

        Assert.Contains(
            migrations,
            migration => migration.Contains("AddNotifications", StringComparison.OrdinalIgnoreCase));
    }
}

