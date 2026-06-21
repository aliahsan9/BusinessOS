using BusinessOS.Application.Common.Interfaces;

namespace BusinessOS.Infrastructure.MultiTenancy;

public class TenantProvider : ITenantProvider
{
    private Guid _tenantId;

    public Guid GetTenantId()
    {
        if (_tenantId == Guid.Empty)
            throw new Exception("Tenant not resolved");

        return _tenantId;
    }

    public void SetTenantId(Guid tenantId)
    {
        _tenantId = tenantId;
    }
}
