using BusinessOS.Application.Common.Models;
using BusinessOS.Domain.Billing;

namespace BusinessOS.Application.Features.Billing.Services;

/// <summary>
/// Server-side subscription limits for the authenticated tenant.
/// Plan limits, subscription ids, and tenant ids supplied by the client are ignored.
/// </summary>
public interface ISubscriptionEntitlementService
{
    Task<TenantLimits> GetLimitsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<ResourceUsage> GetUsageAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Throws <see cref="Common.Exceptions.PlanLimitExceededException"/> when the tenant cannot add
    /// <paramref name="additionalUnits"/>. Persistence still re-checks inside the same database transaction.
    /// </summary>
    Task EnsureCanCreateAsync(
        QuotaResource resource,
        long additionalUnits = 1,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Live usage for one tenant. User seats include active members and unexpired pending invitations.
/// </summary>
public sealed record ResourceUsage(
    int ActiveUsers,
    int PendingInvitations,
    int Customers,
    int Products,
    int Projects,
    int Tasks,
    long StorageBytes,
    int AiRequestsUsed,
    DateTime? AiPeriodStart)
{
    public int OccupiedSeats => ActiveUsers + PendingInvitations;
}
