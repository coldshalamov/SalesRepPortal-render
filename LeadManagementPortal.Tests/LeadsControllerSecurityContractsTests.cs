using System.Reflection;
using LeadManagementPortal.Controllers;
using LeadManagementPortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeadManagementPortal.Tests;

public class LeadsControllerSecurityContractsTests
{
    [Theory]
    [InlineData("UpdateStatus")]
    [InlineData("AddFollowUp")]
    [InlineData("CompleteFollowUp")]
    [InlineData("DeleteFollowUps")]
    [InlineData("GrantExtension")]
    [InlineData("Convert")]
    public void MutatingActions_ShouldRequireValidateAntiForgeryToken(string actionName)
    {
        var candidates = typeof(LeadsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.Name == actionName)
            .ToList();

        Assert.NotEmpty(candidates);

        foreach (var method in candidates)
        {
            var hasAntiForgery = method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>() is not null;
            Assert.True(hasAntiForgery, $"{method.Name} is missing [ValidateAntiForgeryToken].");
        }
    }

    [Fact]
    public void ReassignPost_ShouldRequireValidateAntiForgeryToken()
    {
        var method = typeof(LeadsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(candidate =>
                candidate.Name == "Reassign" &&
                candidate.GetCustomAttribute<HttpPostAttribute>() is not null);

        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public void GrantExtension_ShouldAllowExpectedAdminRolesOnly()
    {
        var method = typeof(LeadsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(candidate => candidate.Name == "GrantExtension");

        Assert.NotNull(method);

        var authorize = method!.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorize);
        Assert.False(string.IsNullOrWhiteSpace(authorize!.Roles));

        var roles = authorize.Roles!
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(UserRoles.OrganizationAdmin, roles);
        Assert.Contains(UserRoles.GroupAdmin, roles);
        Assert.Contains(UserRoles.SalesOrgAdmin, roles);
        Assert.DoesNotContain(UserRoles.SalesRep, roles);
    }
}
