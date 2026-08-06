using BusinessOS.Application.Common.Models;

namespace BusinessOS.Application.Common.Interfaces;

/// <summary>
/// Provides access to the currently resolved multi-tenant context for the active HTTP request scope.
/// </summary>
public interface ITenantContext
{
    /// <summary>Gets the unique identifier of the active tenant.</summary>
    Guid TenantId { get; }

    /// <summary>Gets the display name of the active tenant.</summary>
    string TenantName { get; }

    /// <summary>Gets the URL-friendly slug identifier of the tenant.</summary>
    string Slug { get; }

    /// <summary>Gets the name of the active subscription plan.</summary>
    string? SubscriptionPlanName { get; }

    /// <summary>Gets the unique identifier of the active subscription plan.</summary>
    Guid? SubscriptionPlanId { get; }

    /// <summary>Gets the feature and usage limits configured for the tenant.</summary>
    TenantLimits Limits { get; }

    /// <summary>Gets a snapshot of current tenant usage statistics.</summary>
    TenantUsageSnapshot? Usage { get; }

    /// <summary>Gets a value indicating whether the tenant account is active.</summary>
    bool IsActive { get; }

    /// <summary>Gets a value indicating whether tenant details have been successfully resolved and loaded.</summary>
    bool IsLoaded { get; }

    /// <summary>
    /// Asynchronously loads tenant configuration, limits, and usage details.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    Task LoadAsync(CancellationToken cancellationToken = default);
}
