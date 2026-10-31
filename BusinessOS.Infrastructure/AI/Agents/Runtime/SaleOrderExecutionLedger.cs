using System.Collections.Concurrent;

namespace BusinessOS.Infrastructure.AI.Agents.Runtime;

public interface ISaleOrderExecutionLedger
{
    Task<SaleOrderLease> AcquireAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>
/// Stops one conversation from creating the same confirmed order twice, including overlapping requests.
/// </summary>
public sealed class SaleOrderExecutionLedger : ISaleOrderExecutionLedger
{
    private readonly ConcurrentDictionary<string, Guid> _completed = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new();

    public async Task<SaleOrderLease> AcquireAsync(string key, CancellationToken cancellationToken = default)
    {
        var gate = _gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        _completed.TryGetValue(key, out var existing);
        return new SaleOrderLease(key, existing == Guid.Empty ? null : existing, gate, _completed);
    }
}

public sealed class SaleOrderLease : IAsyncDisposable
{
    private readonly string _key;
    private readonly SemaphoreSlim _gate;
    private readonly ConcurrentDictionary<string, Guid> _completed;
    private bool _released;
    private Guid? _newOrderId;

    internal SaleOrderLease(
        string key,
        Guid? existingOrderId,
        SemaphoreSlim gate,
        ConcurrentDictionary<string, Guid> completed)
    {
        _key = key;
        ExistingOrderId = existingOrderId is { } id && id != Guid.Empty ? id : null;
        _gate = gate;
        _completed = completed;
    }

    public Guid? ExistingOrderId { get; }

    public void Complete(Guid orderId) => _newOrderId = orderId;

    public ValueTask DisposeAsync()
    {
        if (_released)
            return ValueTask.CompletedTask;

        _released = true;
        if (_newOrderId is Guid created && created != Guid.Empty)
            _completed[_key] = created;
        _gate.Release();
        return ValueTask.CompletedTask;
    }
}
