using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.AI.Nalva;

public sealed class NalvaTaskProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<NalvaTaskProcessor> _logger;

    public NalvaTaskProcessor(IServiceScopeFactory scopes, ILogger<NalvaTaskProcessor> logger)
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
                _logger.LogWarning(ex, "Nalva task processor pass failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }

    private async Task ProcessOnceAsync(CancellationToken cancellationToken)
    {
        using var lookup = _scopes.CreateScope();
        var db = lookup.ServiceProvider.GetRequiredService<BusinessOSDbContext>();
        var now = DateTime.UtcNow;
        var due = await db.NalvaTasks.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted
                && x.Status == NalvaStatuses.Queued
                && x.RunAfter <= now
                && (x.NextAttemptAt == null || x.NextAttemptAt <= now))
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.Deadline ?? DateTime.MaxValue)
            .ThenBy(x => x.RunAfter)
            .Select(x => new { x.Id, x.TenantId })
            .Take(40)
            .ToListAsync(cancellationToken);
        var selected = due
            .GroupBy(x => x.TenantId)
            .SelectMany(group => group.Take(NalvaLimits.TasksPerCompanyPerPass))
            .Take(NalvaLimits.TasksPerPass)
            .ToList();

        foreach (var item in selected)
        {
            using var job = _scopes.CreateScope();
            var tenant = job.ServiceProvider.GetRequiredService<ITenantProvider>();
            tenant.SetTenantId(item.TenantId);
            var engine = job.ServiceProvider.GetRequiredService<INalvaExecutionEngine>();
            await engine.RunQueuedTaskAsync(item.Id, cancellationToken);
        }
    }
}
