using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Tenant.DTOs;

namespace BusinessOS.Application.Features.Tenant.Services;

public interface ITenantLimitService
{
    Task<TenantLimits> GetLimitsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task EnsureWithinLimitAsync(string resourceType, CancellationToken cancellationToken = default);
    Task RefreshUsageAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
