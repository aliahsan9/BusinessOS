using BusinessOS.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BusinessOS.Application.Features.Dashboard.Services;

public sealed class DashboardCacheOptions
{
    public const string SectionName = "Dashboard";

    public int CacheExpirationMinutes { get; set; } = 5;
}

public sealed class DashboardCacheService : IDashboardCacheService
{
    private readonly IMemoryCache _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly TimeSpan _expiration;

    public DashboardCacheService(
        IMemoryCache cache,
        ITenantProvider tenantProvider,
        IOptions<DashboardCacheOptions> options)
    {
        _cache = cache;
        _tenantProvider = tenantProvider;
        _expiration = TimeSpan.FromMinutes(Math.Max(1, options.Value.CacheExpirationMinutes));
    }

    public async Task<T> GetOrCreateAsync<T>(
        string cacheKey,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default)
    {
        var tenantKey = _tenantProvider.HasTenant()
            ? _tenantProvider.TenantId.ToString()
            : "global";

        var fullKey = $"dashboard:{tenantKey}:{cacheKey}";

        if (_cache.TryGetValue(fullKey, out T? cached) && cached is not null)
            return cached;

        var result = await factory(cancellationToken);

        _cache.Set(fullKey, result, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _expiration
        });

        return result;
    }
}
