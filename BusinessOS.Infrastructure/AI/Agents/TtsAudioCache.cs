using System.Collections.Concurrent;
using BusinessOS.Application.Features.Agents.Services;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.AI.Agents;

/// <summary>
/// Remembers MP3 bytes for identical speech so the same reply is not synthesized twice.
/// </summary>
public sealed class TtsAudioCache
{
    private readonly TtsOptions _options;
    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _order = new();
    private readonly ConcurrentDictionary<string, Task<SophiaTtsResult>> _inflight = new(StringComparer.Ordinal);
    private long _bytes;

    public TtsAudioCache(IOptions<TtsOptions> options)
    {
        _options = options.Value;
    }

    public async Task<SophiaTtsResult> GetOrCreateAsync(
        string key,
        bool enabled,
        Func<CancellationToken, Task<SophiaTtsResult>> factory,
        CancellationToken cancellationToken)
    {
        if (!enabled)
            return await factory(cancellationToken);

        if (TryGet(key, out var cached))
            return AsCacheHit(cached);

        var task = _inflight.GetOrAdd(key, _ => CreateAsync(key, factory));
        try
        {
            return await task.WaitAsync(cancellationToken);
        }
        finally
        {
            if (task.IsCompleted)
                _inflight.TryRemove(new KeyValuePair<string, Task<SophiaTtsResult>>(key, task));
        }
    }

    private async Task<SophiaTtsResult> CreateAsync(
        string key,
        Func<CancellationToken, Task<SophiaTtsResult>> factory)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(50));
        var result = await factory(timeout.Token);
        Store(key, result);
        return result;
    }

    private bool TryGet(string key, out SophiaTtsResult result)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out var entry))
            {
                result = null!;
                return false;
            }

            if (_options.CacheMinutes > 0 &&
                DateTimeOffset.UtcNow - entry.StoredAt > TimeSpan.FromMinutes(_options.CacheMinutes))
            {
                RemoveLocked(key);
                result = null!;
                return false;
            }

            _order.Remove(entry.Node);
            _order.AddFirst(entry.Node);
            result = entry.Result;
            return true;
        }
    }

    private void Store(string key, SophiaTtsResult result)
    {
        var length = result.AudioBytes.LongLength;
        if (length < 32 || length > 2_000_000)
            return;

        lock (_gate)
        {
            if (_entries.ContainsKey(key))
                return;

            var node = new LinkedListNode<string>(key);
            _order.AddFirst(node);
            _entries[key] = new Entry(result, node, DateTimeOffset.UtcNow);
            _bytes += length;

            var maxEntries = Math.Max(1, _options.CacheMaxEntries);
            var maxBytes = Math.Max(32, _options.CacheMaxBytes);
            while ((_entries.Count > maxEntries || _bytes > maxBytes) && _order.Last is not null)
                RemoveLocked(_order.Last.Value);
        }
    }

    private void RemoveLocked(string key)
    {
        if (!_entries.Remove(key, out var entry))
            return;

        _bytes -= entry.Result.AudioBytes.LongLength;
        if (_bytes < 0)
            _bytes = 0;
        _order.Remove(entry.Node);
    }

    private static SophiaTtsResult AsCacheHit(SophiaTtsResult result) => new()
    {
        AudioBytes = result.AudioBytes,
        ContentType = result.ContentType,
        VoiceName = result.VoiceName,
        Provider = result.Provider,
        ServedFromCache = true
    };

    private sealed record Entry(SophiaTtsResult Result, LinkedListNode<string> Node, DateTimeOffset StoredAt);
}
