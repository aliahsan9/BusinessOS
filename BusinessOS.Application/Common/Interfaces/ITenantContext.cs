using BusinessOS.Application.Common.Models;

namespace BusinessOS.Application.Common.Interfaces;

public interface ITenantContext
{
    Guid TenantId { get; }
    string TenantName { get; }
    string Slug { get; }
    string? SubscriptionPlanName { get; }
    Guid? SubscriptionPlanId { get; }
    TenantLimits Limits { get; }
    TenantUsageSnapshot? Usage { get; }
    bool IsActive { get; }
    bool IsLoaded { get; }

    Task LoadAsync(CancellationToken cancellationToken = default);
}
