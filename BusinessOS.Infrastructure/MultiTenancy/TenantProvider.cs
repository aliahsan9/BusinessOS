using BusinessOS.Application.Common.Interfaces;
using System.Threading;

namespace BusinessOS.Infrastructure.MultiTenancy;

public class TenantProvider : ITenantProvider
{
    private static readonly AsyncLocal<Guid> _tenantId = new();

    public Guid GetTenantId()
    {
        var id = _tenantId.Value;

        if (id == Guid.Empty)
            throw new Exception("Tenant not resolved for this request");

        return id;
    }

    public void SetTenantId(Guid tenantId)
    {
        _tenantId.Value = tenantId;
    }
}
