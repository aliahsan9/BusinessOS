using System.Text.Json;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.FinanceAgent;
using BusinessOS.Application.Features.Marketing;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Application.Features.ProcurementAgent;
using BusinessOS.Application.Features.SalesAgent;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
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
            .Where(x => !x.IsDeleted
                && x.IsActive
                && !x.IsPaused
                && x.CancelledAt == null
                && x.TriggerKind == "Scheduled"
                && (x.NextRunAt == null || x.NextRunAt <= now))
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
        await RunAgentMonitorsAsync(cancellationToken);
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

        var overdueTasks = await db.WorkTasks.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted
                        && x.DueDate != null
                        && x.DueDate < DateTime.UtcNow
                        && x.Status != WorkTaskStatus.Done
                        && x.Status != WorkTaskStatus.Cancelled)
            .Select(x => new { x.TenantId, x.Id, x.ProjectId, x.Title })
            .Take(25)
            .ToListAsync(cancellationToken);
        foreach (var row in overdueTasks)
        {
            var title = JsonSerializer.Serialize(row.Title);
            await EnqueueAsync(
                row.TenantId,
                NalvaEventNames.TaskOverdue,
                $"{{\"taskId\":\"{row.Id}\",\"projectId\":\"{row.ProjectId}\",\"title\":{title}}}",
                $"TaskOverdue:{row.Id:N}:{today}",
                cancellationToken);
        }

        var delayedPurchases = await db.Purchases.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted
                        && x.ExpectedDeliveryDate != null
                        && x.ExpectedDeliveryDate < DateTime.UtcNow
                        && (x.Status == PurchaseOrderStatusNames.Approved
                            || x.Status == PurchaseOrderStatusNames.PartiallyReceived
                            || x.Status == PurchaseOrderStatusNames.Pending))
            .Select(x => new { x.TenantId, x.Id, x.ExpectedDeliveryDate })
            .Take(25)
            .ToListAsync(cancellationToken);
        foreach (var row in delayedPurchases)
        {
            await EnqueueAsync(
                row.TenantId,
                NalvaEventNames.DeliveryDelayed,
                $"{{\"purchaseOrderId\":\"{row.Id}\",\"expectedDeliveryDate\":\"{row.ExpectedDeliveryDate:O}\"}}",
                $"DeliveryDelayed:{row.Id:N}:{today}",
                cancellationToken);
        }
    }

    private async Task RunAgentMonitorsAsync(CancellationToken cancellationToken)
    {
        using var lookup = _scopes.CreateScope();
        var db = lookup.ServiceProvider.GetRequiredService<BusinessOSDbContext>();
        var today = DateTime.UtcNow.ToString("yyyyMMdd");

        var stockTenants = await db.Inventories.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.ReorderLevel > 0 && x.CurrentStock < x.ReorderLevel)
            .Select(x => x.TenantId)
            .Distinct()
            .Take(8)
            .ToListAsync(cancellationToken);
        var delayedTenants = await db.Purchases.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.ExpectedDeliveryDate != null && x.ExpectedDeliveryDate < DateTime.UtcNow)
            .Select(x => x.TenantId)
            .Distinct()
            .Take(8)
            .ToListAsync(cancellationToken);

        foreach (var tenantId in stockTenants.Concat(delayedTenants).Distinct().Take(8))
        {
            try
            {
                using var job = _scopes.CreateScope();
                job.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantId(tenantId);
                var procurement = job.ServiceProvider.GetService<IInventoryProcurementAgentService>();
                if (procurement is not null)
                    await procurement.RunScheduledMonitorAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Procurement scheduled monitor failed for tenant {TenantId}", tenantId);
            }
        }

        var financeTenantIds = await db.FinanceAgentSettings.IgnoreQueryFilters()
            .AsNoTracking()
            .Select(x => x.TenantId)
            .Distinct()
            .Take(8)
            .ToListAsync(cancellationToken);
        if (financeTenantIds.Count == 0)
        {
            financeTenantIds = await db.Invoices.IgnoreQueryFilters()
                .AsNoTracking()
                .Where(x => x.OutstandingAmount > 0)
                .Select(x => x.TenantId)
                .Distinct()
                .Take(8)
                .ToListAsync(cancellationToken);
        }

        foreach (var tenantId in financeTenantIds)
        {
            try
            {
                using var job = _scopes.CreateScope();
                job.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantId(tenantId);
                var finance = job.ServiceProvider.GetService<IFinanceAgentService>();
                if (finance is not null)
                    await finance.RunScheduledMonitorAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Finance scheduled monitor failed for tenant {TenantId}", tenantId);
            }
        }

        var marketingTenants = await db.MarketingAgentPolicies.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => !x.IsDeleted && !x.IsPaused && (x.DailyCronEnabled || x.WeeklyCronEnabled || x.MonthlyCronEnabled))
            .Select(x => x.TenantId)
            .Distinct()
            .Take(8)
            .ToListAsync(cancellationToken);
        foreach (var tenantId in marketingTenants)
        {
            using var job = _scopes.CreateScope();
            job.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantId(tenantId);
            var marketing = job.ServiceProvider.GetService<IMarketingOperationsService>();
            if (marketing is null)
                continue;
            // Idempotency: one maintenance work item key per UTC day via work-item title marker.
            var marker = $"Daily marketing maintenance counts:{today}";
            var exists = await job.ServiceProvider.GetRequiredService<BusinessOSDbContext>().NalvaWorkItems
                .AnyAsync(x => x.AgentKey == "marketing" && x.Kind == "MarketingMaintenance" && x.Title.Contains(today), cancellationToken);
            if (!exists)
                await marketing.RunRecurringMaintenanceAsync(cancellationToken);
            _ = marker;
        }

        var salesTenants = await db.SalesAgentSettings.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => !x.IsPaused)
            .Select(x => x.TenantId)
            .Distinct()
            .Take(8)
            .ToListAsync(cancellationToken);
        foreach (var tenantId in salesTenants)
        {
            using var job = _scopes.CreateScope();
            job.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantId(tenantId);
            var salesDb = job.ServiceProvider.GetRequiredService<BusinessOSDbContext>();
            var already = await salesDb.NalvaWorkItems.AnyAsync(
                x => x.AgentKey == "sales" && x.Kind == "SalesMonitor" && x.Title == $"Sales monitor {today}",
                cancellationToken);
            if (already)
                continue;

            var openFollowUps = await salesDb.FollowUps.CountAsync(x => x.Status == FollowUpStatuses.Open && x.DueAt < DateTime.UtcNow, cancellationToken);
            var pendingOutreach = await salesDb.SalesOutreachMessages.CountAsync(x => x.Status == SalesAgentStatuses.PendingApproval, cancellationToken);
            salesDb.NalvaWorkItems.Add(new NalvaWorkItem
            {
                TenantId = tenantId,
                AgentKey = "sales",
                Kind = "SalesMonitor",
                Title = $"Sales monitor {today}",
                Body = JsonSerializer.Serialize(new { openOverdueFollowUps = openFollowUps, pendingOutreachApprovals = pendingOutreach, fabricated = false }),
                Status = "Open"
            });
            await salesDb.SaveChangesAsync(cancellationToken);
            _ = job.ServiceProvider.GetService<ISalesAgentService>();
        }

        var supportTenants = await db.SupportTickets.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => !x.IsDeleted && (x.Status == "Open" || x.Status == "Pending" || x.Status == "Waiting"))
            .Select(x => x.TenantId)
            .Distinct()
            .Take(8)
            .ToListAsync(cancellationToken);
        foreach (var tenantId in supportTenants)
        {
            using var job = _scopes.CreateScope();
            job.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantId(tenantId);
            var supportDb = job.ServiceProvider.GetRequiredService<BusinessOSDbContext>();
            var already = await supportDb.NalvaWorkItems.AnyAsync(
                x => x.AgentKey == "support" && x.Kind == "SupportMonitor" && x.Title == $"Support monitor {today}",
                cancellationToken);
            if (already)
                continue;

            var openTickets = await supportDb.SupportTickets.CountAsync(
                x => x.Status == "Open" || x.Status == "Pending" || x.Status == "Waiting",
                cancellationToken);
            supportDb.NalvaWorkItems.Add(new NalvaWorkItem
            {
                TenantId = tenantId,
                AgentKey = "support",
                Kind = "SupportMonitor",
                Title = $"Support monitor {today}",
                Body = JsonSerializer.Serialize(new { openTickets, preparedResponsesOnly = true, fabricated = false }),
                Status = "Open"
            });
            await supportDb.SaveChangesAsync(cancellationToken);

            await EnqueueAsync(tenantId, NalvaEventNames.SupportTicketCreated, "{\"source\":\"schedule-monitor\"}", $"SupportMonitor:{tenantId:N}:{today}", cancellationToken);
        }
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

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, object> ClaimGates = new();

    public async Task RunOneAsync(Guid definitionId, CancellationToken cancellationToken)
    {
        var claim = Guid.NewGuid();
        var claimed = 0;
        try
        {
            claimed = await _db.NalvaWorkflowDefinitions
                .Where(x => x.Id == definitionId
                    && x.IsActive
                    && !x.IsPaused
                    && x.CancelledAt == null
                    && x.TriggerKind == "Scheduled"
                    && (x.ClaimToken == null || x.NextRunAt == null || x.NextRunAt <= DateTime.UtcNow))
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ClaimToken, claim), cancellationToken);
        }
        catch (InvalidOperationException)
        {
            var gate = ClaimGates.GetOrAdd(definitionId, static _ => new object());
            lock (gate)
            {
                var candidate = _db.NalvaWorkflowDefinitions.FirstOrDefault(x =>
                    x.Id == definitionId
                    && x.IsActive
                    && !x.IsPaused
                    && x.CancelledAt == null
                    && x.TriggerKind == "Scheduled"
                    && (x.ClaimToken == null || x.NextRunAt == null || x.NextRunAt <= DateTime.UtcNow));
                if (candidate is not null)
                {
                    candidate.ClaimToken = claim;
                    _db.SaveChanges();
                    claimed = 1;
                }
            }

            ClaimGates.TryRemove(definitionId, out _);
        }

        if (claimed == 0)
            return;

        var definition = await _db.NalvaWorkflowDefinitions.FirstOrDefaultAsync(x => x.Id == definitionId && x.ClaimToken == claim, cancellationToken);
        if (definition is null)
            return;

        var now = DateTime.UtcNow;
        var spec = new NalvaScheduleSpec(
            definition.TimeZoneId,
            definition.RecurrenceKind,
            definition.IntervalMinutes,
            definition.ScheduleStartUtc,
            definition.ScheduleEndUtc,
            definition.LocalTimeOfDay,
            definition.DaysOfWeek,
            definition.DayOfMonth);

        if (!NalvaSchedule.TryDue(spec, now, definition.LastScheduleWindow, out var window, out var next))
        {
            definition.NextRunAt = next;
            definition.ClaimToken = null;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        var running = await _db.NalvaWorkflowRuns.CountAsync(
            x => x.DefinitionId == definition.Id && x.Status == NalvaStatuses.Running,
            cancellationToken);
        if (running >= Math.Max(1, definition.MaxConcurrentRuns))
        {
            definition.NextRunAt = now.AddMinutes(1);
            definition.ClaimToken = null;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        // Loop / duplicate prevention: same schedule window correlation already completed or running.
        var correlation = $"schedule:{definition.Id:N}:{window}";
        var duplicate = await _db.NalvaWorkflowRuns.AnyAsync(
            x => x.DefinitionId == definition.Id
                 && x.CorrelationId == correlation
                 && (x.Status == NalvaStatuses.Running
                     || x.Status == NalvaStatuses.Completed
                     || x.Status == NalvaStatuses.AwaitingApproval),
            cancellationToken);
        if (duplicate)
        {
            definition.LastScheduleWindow = window;
            definition.NextRunAt = next;
            definition.ClaimToken = null;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        definition.LastScheduleWindow = window;
        definition.NextRunAt = next;
        definition.LastRunAt = now;
        definition.ClaimToken = null;
        await _db.SaveChangesAsync(cancellationToken);
        try
        {
            await _workflows.RunAsync(definition.Id, "{}", new NalvaWorkflowRunOptions
            {
                TriggerSource = "Schedule",
                CorrelationId = correlation
            }, cancellationToken);
            definition.ConsecutiveFailures = 0;
            definition.LastFailure = null;
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            definition.ConsecutiveFailures += 1;
            definition.LastFailure = ex.Message.Length > 1900 ? ex.Message[..1900] : ex.Message;
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogWarning(ex, "Scheduled workflow {WorkflowId} did not start.", definitionId);
        }
    }
}
