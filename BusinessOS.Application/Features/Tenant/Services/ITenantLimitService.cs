using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Tenant.DTOs;

namespace BusinessOS.Application.Features.Tenant.Services;

public interface ITenantLimitService
{
    Task<TenantLimits> GetLimitsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task EnsureWithinLimitAsync(string resourceType, CancellationToken cancellationToken = default);
    Task RefreshUsageAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task IncrementAiUsageAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Consumes one AI request when the call is accepted. The same operation key does not consume twice.
    /// Provider failures after acceptance still consume the request. Validation failures do not.
    /// </summary>
    Task TryConsumeAiRequestAsync(
        string? operationKey,
        int? inputTokens = null,
        int? outputTokens = null,
        int? totalTokens = null,
        decimal? estimatedProviderCostUsd = null,
        CancellationToken cancellationToken = default);

    Task RecordAiProviderUsageAsync(
        int? inputTokens,
        int? outputTokens,
        int? totalTokens,
        decimal? estimatedProviderCostUsd,
        CancellationToken cancellationToken = default);
}
