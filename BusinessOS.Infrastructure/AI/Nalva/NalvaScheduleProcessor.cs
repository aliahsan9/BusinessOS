using System.Text.Json;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.AI.Nalva;

public sealed class NalvaScheduleProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<NalvaScheduleProcessor> _logger;

    public NalvaScheduleProcessor(IServiceScopeFactory scopes, ILogger<NalvaScheduleProcessor> logger)
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
                await ProcessOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Nalva schedule processor pass failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task ProcessOnceAsync(CancellationToken cancellationToken)
    {
        using var lookup = _scopes.CreateScope();
        var db = lookup.ServiceProvider.GetRequiredService<BusinessOSDbContext>();
        var now = DateTime.UtcNow;
        var due = await db.NalvaWorkflowDefinitions.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.IsActive && x.TriggerKind == "Scheduled" && (x.NextRunAt == null || x.NextRunAt <= now))
            .OrderBy(x => x.NextRunAt)
            .Select(x => new { x.Id, x.TenantId })
            .Take(40)
            .ToListAsync(cancellationToken);
        foreach (var item in due.GroupBy(x => x.TenantId).SelectMany(group => group.Take(1)).Take(8))
        {
            using var job = _scopes.CreateScope();
            job.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantId(item.TenantId);
            var worker = job.ServiceProvider.GetRequiredService<NalvaScheduleWorker>();
            await worker.RunOneAsync(item.Id, cancellationToken);
        }

        await ScanSignalsAsync(cancellationToken);
    }

    private async Task ScanSignalsAsync(CancellationToken cancellationToken)
    {
        using var lookup = _scopes.CreateScope();
        var db = lookup.ServiceProvider.GetRequiredService<BusinessOSDbContext>();
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var stock = await db.Inventories.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.ReorderLevel > 0 && x.CurrentStock < x.ReorderLevel)
            .Select(x => new { x.TenantId, x.ProductId, x.CurrentStock, x.ReorderLevel })
            .Take(25)
            .ToListAsync(cancellationToken);
        foreach (var row in stock)
            await EnqueueAsync(row.TenantId, NalvaEventNames.StockBelowThreshold, $"{{\"productId\":\"{row.ProductId}\",\"currentStock\":{row.CurrentStock},\"reorderLevel\":{row.ReorderLevel}}}", $"StockBelowThreshold:{row.ProductId:N}:{today}", cancellationToken);

        var invoices = await db.Invoices.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.DueDate < DateTime.UtcNow && x.OutstandingAmount > 0 && x.Status != "Paid" && x.Status != "Cancelled")
            .Select(x => new { x.TenantId, x.Id, x.OutstandingAmount })
            .Take(25)
            .ToListAsync(cancellationToken);
        foreach (var row in invoices)
            await EnqueueAsync(row.TenantId, NalvaEventNames.InvoiceOverdue, $"{{\"invoiceId\":\"{row.Id}\",\"outstanding\":{row.OutstandingAmount}}}", $"InvoiceOverdue:{row.Id:N}:{today}", cancellationToken);
    }

    private async Task EnqueueAsync(Guid tenantId, string name, string payload, string key, CancellationToken cancellationToken)
    {
        using var job = _scopes.CreateScope();
        job.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantId(tenantId);
        var outbox = job.ServiceProvider.GetRequiredService<INalvaEventOutbox>();
        await outbox.EnqueueAsync(name, payload, key, cancellationToken);
    }
}

public sealed class NalvaScheduleWorker
{
    private readonly BusinessOSDbContext _db;
    private readonly INalvaWorkflowEngine _workflows;
    private readonly ILogger<NalvaScheduleWorker> _logger;

    public NalvaScheduleWorker(BusinessOSDbContext db, INalvaWorkflowEngine workflows, ILogger<NalvaScheduleWorker> logger)
    {
        _db = db;
        _workflows = workflows;
        _logger = logger;
    }

    public async Task RunOneAsync(Guid definitionId, CancellationToken cancellationToken)
    {
        var definition = await _db.NalvaWorkflowDefinitions.FirstOrDefaultAsync(x => x.Id == definitionId && x.IsActive && x.TriggerKind == "Scheduled", cancellationToken);
        if (definition is null)
            return;
        var now = DateTime.UtcNow;
        if (!NalvaSchedule.TryWindow(now, definition.TimeZoneId, definition.IntervalMinutes, definition.ScheduleStartUtc, definition.ScheduleEndUtc, definition.LastScheduleWindow, out var window, out var next))
        {
            definition.NextRunAt = next;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        definition.LastScheduleWindow = window;
        definition.NextRunAt = next;
        await _db.SaveChangesAsync(cancellationToken);
        try
        {
            await _workflows.RunAsync(definition.Id, "{}", new NalvaWorkflowRunOptions
            {
                TriggerSource = "Schedule",
                CorrelationId = $"schedule:{definition.Id:N}:{window}"
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Scheduled workflow {WorkflowId} did not start.", definitionId);
        }
    }
}
