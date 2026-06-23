using BusinessOS.Application.Common.Interfaces;
using System.Threading;

namespace BusinessOS.Infrastructure.MultiTenancy;

public class TenantProvider : ITenantProvider
{
    private static readonly AsyncLocal<Guid?> _tenantId = new();

    public Guid TenantId
    {
        get
        {
            return _tenantId.Value
                ?? throw new InvalidOperationException("TenantId has not been set.");
        }
    }

    public void SetTenantId(Guid tenantId)
    {
        _tenantId.Value = tenantId;
    }

    public bool HasTenant()
    {
        return _tenantId.Value.HasValue;
    }
}
