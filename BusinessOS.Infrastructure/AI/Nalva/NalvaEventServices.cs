using System.Text.Json;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.AI.Nalva;

public sealed class NalvaEventOutbox : INalvaEventOutbox
{
    private readonly BusinessOSDbContext _db;
    private readonly ITenantProvider _tenant;

    public NalvaEventOutbox(BusinessOSDbContext db, ITenantProvider tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<Guid?> EnqueueAsync(string name, string payloadJson, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (!_tenant.HasTenant())
            throw new BadRequestException("Company context is required.");
        if (!NalvaEventNames.IsAllowed(name))
            throw new BadRequestException("That event is not registered.");
        if (NalvaGuard.RejectsArguments(payloadJson, out var rejected))
            throw new BadRequestException(rejected);
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _db.NalvaBusinessEvents.AsNoTracking()
                .AnyAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
            if (existing)
                return null;
        }

        var suppressed = NalvaExecutionAmbient.Depth > 0;
        var row = new NalvaBusinessEvent
        {
            Name = name,
            PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson,
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim(),
            Status = suppressed ? "Suppressed" : "Pending",
            CorrelationId = Guid.NewGuid().ToString("N"),
            OccurredAt = DateTime.UtcNow,
            ProcessedAt = suppressed ? DateTime.UtcNow : null
        };
        _db.NalvaBusinessEvents.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return suppressed ? null : row.Id;
    }
}

public sealed class NalvaEventDispatcher : INalvaEventDispatcher
{
    private readonly IServiceScopeFactory _scopes;
    private readonly NalvaEventWorker _worker;
    private readonly ILogger<NalvaEventDispatcher> _logger;

    public NalvaEventDispatcher(IServiceScopeFactory scopes, NalvaEventWorker worker, ILogger<NalvaEventDispatcher> logger)
    {
        _scopes = scopes;
        _worker = worker;
        _logger = logger;
    }

    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken = default)
    {
        using var lookup = _scopes.CreateScope();
        var db = lookup.ServiceProvider.GetRequiredService<BusinessOSDbContext>();
        var now = DateTime.UtcNow;
        var pending = await db.NalvaBusinessEvents.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.Status == "Pending" && (x.NextAttemptAt == null || x.NextAttemptAt <= now))
            .OrderBy(x => x.OccurredAt)
            .Select(x => new { x.Id, x.TenantId })
            .Take(40)
            .ToListAsync(cancellationToken);
        var selected = pending.GroupBy(x => x.TenantId).SelectMany(group => group.Take(2)).Take(10).ToList();
        var started = 0;
        foreach (var item in selected)
        {
            using var job = _scopes.CreateScope();
            job.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantId(item.TenantId);
            var worker = job.ServiceProvider.GetRequiredService<NalvaEventWorker>();
            started += await worker.DispatchOneAsync(item.Id, cancellationToken);
        }
        return started;
    }

    public Task<int> DispatchOneAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        _worker.DispatchOneAsync(eventId, cancellationToken);
}

public sealed class NalvaEventWorker
{
    private readonly BusinessOSDbContext _db;
    private readonly INalvaWorkflowEngine _workflows;
    private readonly ILogger<NalvaEventWorker> _logger;

    public NalvaEventWorker(BusinessOSDbContext db, INalvaWorkflowEngine workflows, ILogger<NalvaEventWorker> logger)
    {
        _db = db;
        _workflows = workflows;
        _logger = logger;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, object> ClaimGates = new();

    public async Task<int> DispatchOneAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var claimed = 0;
        var claimedAt = DateTime.UtcNow;
        try
        {
            claimed = await _db.NalvaBusinessEvents
                .Where(x => x.Id == eventId && x.Status == "Pending")
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.Status, "Running")
                        .SetProperty(x => x.UpdatedAt, claimedAt),
                    cancellationToken);
        }
        catch (InvalidOperationException)
        {
            var gate = ClaimGates.GetOrAdd(eventId, static _ => new object());
            lock (gate)
            {
                var candidate = _db.NalvaBusinessEvents.FirstOrDefault(x => x.Id == eventId && x.Status == "Pending");
                if (candidate is not null)
                {
                    candidate.Status = "Running";
                    candidate.UpdatedAt = claimedAt;
                    _db.SaveChanges();
                    claimed = 1;
                }
            }

            ClaimGates.TryRemove(eventId, out _);
        }

        if (claimed == 0)
            return 0;

        var row = await _db.NalvaBusinessEvents.FirstAsync(x => x.Id == eventId, cancellationToken);
        try
        {
            var matches = await _db.NalvaWorkflowDefinitions.AsNoTracking()
                .Where(x => x.IsActive && x.TriggerKind == "Event" && x.TriggerEvent == row.Name)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            var started = 0;
            foreach (var id in matches)
            {
                await _workflows.RunAsync(id, row.PayloadJson, new NalvaWorkflowRunOptions
                {
                    TriggerSource = "Event",
                    CorrelationId = row.CorrelationId + ":" + id.ToString("N")
                }, cancellationToken);
                started++;
            }

            row.Status = "Processed";
            row.ProcessedAt = DateTime.UtcNow;
            row.WorkflowsStarted = started;
            row.LastError = null;
            await _db.SaveChangesAsync(cancellationToken);
            return started;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            row.Attempt += 1;
            row.LastError = NalvaGuard.Clip(ex.Message, 500);
            row.Status = row.Attempt >= NalvaLimits.MaxRetries ? "DeadLetter" : "Pending";
            row.NextAttemptAt = DateTime.UtcNow.AddSeconds(Math.Pow(2, row.Attempt));
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogWarning(ex, "Nalva event {EventId} could not be dispatched.", eventId);
            return 0;
        }
    }
}

public sealed class NalvaEventHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<NalvaEventHostedService> _logger;

    public NalvaEventHostedService(IServiceScopeFactory scopes, ILogger<NalvaEventHostedService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<INalvaEventDispatcher>();
                await dispatcher.DispatchPendingAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Nalva event dispatcher pass failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}
