using System.Collections.Concurrent;
using System.Diagnostics;
using BusinessOS.Application.Common.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Application.Common.Caching;

/// <summary>
/// Production-ready <see cref="IMemoryCache"/> wrapper with stampede protection,
/// prefix-based invalidation, and Serilog-compatible structured logging.
/// Registered as a singleton so locks and key indexes are shared across requests.
/// </summary>
public sealed class CacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly CacheSettings _settings;
    private readonly ILogger<CacheService> _logger;
    private readonly ConcurrentDictionary<string, byte> _keys = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public CacheService(
        IMemoryCache cache,
        IOptions<CacheSettings> options,
        ILogger<CacheService> logger)
    {
        _cache = cache;
        _settings = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sw = Stopwatch.StartNew();
        if (_cache.TryGetValue(key, out T? value) && value is not null)
        {
            sw.Stop();
            _logger.LogInformation(
                "[Cache Hit] {CacheKey} in {ElapsedMs} ms",
                key,
                sw.Elapsed.TotalMilliseconds);
            return Task.FromResult<T?>(value);
        }

        sw.Stop();
        _logger.LogInformation(
            "[Cache Miss] {CacheKey} in {ElapsedMs} ms",
            key,
            sw.Elapsed.TotalMilliseconds);
        return Task.FromResult<T?>(default);
    }

    /// <inheritdoc />
    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sw = Stopwatch.StartNew();
        var options = BuildEntryOptions(absoluteExpiration, slidingExpiration);
        _cache.Set(key, value, options);
        _keys[key] = 0;
        sw.Stop();

        _logger.LogInformation(
            "[Cache Set] {CacheKey} Absolute={Absolute} Sliding={Sliding} in {ElapsedMs} ms",
            key,
            absoluteExpiration ?? _settings.DefaultExpiration,
            slidingExpiration,
            sw.Elapsed.TotalMilliseconds);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sw = Stopwatch.StartNew();
        _cache.Remove(key);
        _keys.TryRemove(key, out _);
        sw.Stop();

        _logger.LogInformation(
            "[Cache Removed] {CacheKey} in {ElapsedMs} ms",
            key,
            sw.Elapsed.TotalMilliseconds);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(prefix))
            return Task.CompletedTask;

        var sw = Stopwatch.StartNew();
        var removed = 0;

        foreach (var key in _keys.Keys)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!key.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            _cache.Remove(key);
            if (_keys.TryRemove(key, out _))
                removed++;
        }

        sw.Stop();
        _logger.LogInformation(
            "[Cache Removed] prefix {CachePrefix} ({RemovedCount} entries) in {ElapsedMs} ms",
            prefix,
            removed,
            sw.Elapsed.TotalMilliseconds);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_cache.TryGetValue(key, out _));
    }

    /// <inheritdoc />
    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var existing = await GetAsync<T>(key, cancellationToken);
        if (existing is not null)
            return existing;

        var gate = _locks.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring the lock to prevent cache stampede.
            existing = await GetAsync<T>(key, cancellationToken);
            if (existing is not null)
                return existing;

            var sw = Stopwatch.StartNew();
            var value = await factory(cancellationToken);
            sw.Stop();

            await SetAsync(key, value, absoluteExpiration, slidingExpiration, cancellationToken);

            _logger.LogDebug(
                "[Cache Factory] {CacheKey} produced value in {ElapsedMs} ms",
                key,
                sw.Elapsed.TotalMilliseconds);

            return value;
        }
        finally
        {
            gate.Release();
        }
    }

    private MemoryCacheEntryOptions BuildEntryOptions(
        TimeSpan? absoluteExpiration,
        TimeSpan? slidingExpiration)
    {
        var options = new MemoryCacheEntryOptions();

        if (slidingExpiration.HasValue)
            options.SlidingExpiration = slidingExpiration;

        if (absoluteExpiration.HasValue)
            options.AbsoluteExpirationRelativeToNow = absoluteExpiration;
        else if (!slidingExpiration.HasValue)
            options.AbsoluteExpirationRelativeToNow = _settings.DefaultExpiration;

        options.RegisterPostEvictionCallback(static (key, _, _, state) =>
        {
            if (state is ConcurrentDictionary<string, byte> keys && key is string keyString)
                keys.TryRemove(keyString, out _);
        }, _keys);

        return options;
    }
}
