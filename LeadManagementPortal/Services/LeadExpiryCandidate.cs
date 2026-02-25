namespace LeadManagementPortal.Services
{
    public sealed record LeadExpiryCandidate(
        string LeadId,
        string Company,
        DateTime ExpiryDateUtc,
        string? AssignedToId,
        int? SalesOrgId);
}

