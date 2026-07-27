using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Application.Features.Dashboard.Services;

public sealed class DashboardCacheOptions
{
    public const string SectionName = "Dashboard";

    public int CacheExpirationMinutes { get; set; } = 5;
}

/// <summary>
/// Dashboard-specific cache facade. Delegates to the centralized <see cref="ICacheService"/>
/// while preserving the existing <see cref="IDashboardCacheService"/> contract.
/// </summary>
public sealed class DashboardCacheService : IDashboardCacheService
{
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly TimeSpan _expiration;
    private readonly ILogger<DashboardCacheService> _logger;

    public DashboardCacheService(
        ICacheService cache,
        ITenantProvider tenantProvider,
        IOptions<CacheSettings> cacheSettings,
        IOptions<DashboardCacheOptions> dashboardOptions,
        ILogger<DashboardCacheService> logger)
    {
        _cache = cache;
        _tenantProvider = tenantProvider;
        _logger = logger;

        // Prefer CacheSettings; keep Dashboard:CacheExpirationMinutes as a legacy override when CacheSettings is unset (0).
        _expiration = cacheSettings.Value.DashboardExpirationMinutes > 0
            ? cacheSettings.Value.DashboardExpiration
            : TimeSpan.FromMinutes(Math.Max(1, dashboardOptions.Value.CacheExpirationMinutes));
    }

    public Task<T> GetOrCreateAsync<T>(
        string cacheKey,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantProvider.HasTenant())
        {
            _logger.LogDebug("Dashboard cache bypassed — no tenant context for key {CacheKey}", cacheKey);
            return factory(cancellationToken);
        }

        var fullKey = CacheKeys.Dashboard(_tenantProvider.TenantId, cacheKey);
        return _cache.GetOrSetAsync(fullKey, factory, _expiration, cancellationToken: cancellationToken);
    }
}
