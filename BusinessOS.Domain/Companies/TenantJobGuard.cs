namespace BusinessOS.Domain.Companies;

public static class TenantJobGuard
{
    public static Guid Require(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("Company-specific background work requires a tenant id.");

        return tenantId;
    }
}
