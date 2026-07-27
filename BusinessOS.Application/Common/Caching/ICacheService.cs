namespace BusinessOS.Application.Common.Caching;

/// <summary>
/// Centralized in-memory cache abstraction. Application code must not use <c>IMemoryCache</c> directly.
/// </summary>
public interface ICacheService
{
    /// <summary>Gets a cached value by key, or <c>null</c> on a cache miss.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a value in the cache.
    /// When neither expiration is supplied, the configured default absolute expiration is applied.
    /// </summary>
    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null,
        CancellationToken cancellationToken = default);

    /// <summary>Removes a single cache entry.</summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Removes all cache entries whose keys start with the given prefix.</summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);

    /// <summary>Returns whether a cache entry exists for the key.</summary>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache-aside helper with stampede protection: concurrent callers for the same key
    /// share a single factory invocation and reuse the cached result.
    /// </summary>
    Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null,
        CancellationToken cancellationToken = default);
}
