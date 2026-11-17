using System;

namespace BusinessOS.Application.Common.Interfaces;

public interface ITenantProvider
{
    Guid TenantId { get; }
    void SetTenantId(Guid tenantId);
    void Clear();
    bool HasTenant();
}
