namespace BusinessOS.Application.Common.Interfaces;

/// <summary>
/// Reads the tenant membership stored for an Identity user.
/// Callers must not trust a client-supplied tenant id in place of this result.
/// </summary>
public interface ITenantMembershipLookup
{
    Task<TenantMembership?> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default);
}

public sealed record TenantMembership(
    Guid TenantId,
    bool IsActive,
    int SecurityVersion = 1,
    DateTimeOffset? LockoutEnd = null);
