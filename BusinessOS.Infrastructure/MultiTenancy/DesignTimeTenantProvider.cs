using BusinessOS.Application.Common.Interfaces;
using System;

namespace BusinessOS.Infrastructure.MultiTenancy;

public class DesignTimeTenantProvider : ITenantProvider
{
    public Guid GetTenantId()
    {
        return Guid.Empty;
    }

    public void SetTenantId(Guid tenantId)
    {
        // no-op for design time
    }
    public Guid TenantId
    {
        get => Guid.Empty;   // design-time fallback
        set { /* no-op */ }
    }

    public bool HasTenant()
    {
        return false; // design-time: no tenant context
    }
}
