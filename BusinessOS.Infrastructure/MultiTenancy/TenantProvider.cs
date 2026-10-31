using BusinessOS.Application.Common.Interfaces;
using System.Threading;

namespace BusinessOS.Infrastructure.MultiTenancy;

public class TenantProvider : ITenantProvider
{
    private static readonly AsyncLocal<Guid?> AmbientTenantId = new();
    private Guid? _tenantId;

    public Guid TenantId
    {
        get
        {
            return _tenantId
                ?? AmbientTenantId.Value
                ?? throw new InvalidOperationException("TenantId has not been set.");
        }
    }

    public void SetTenantId(Guid tenantId)
    {
        _tenantId = tenantId;
        AmbientTenantId.Value = tenantId;
    }

    public bool HasTenant()
    {
        return _tenantId.HasValue || AmbientTenantId.Value.HasValue;
    }
}
