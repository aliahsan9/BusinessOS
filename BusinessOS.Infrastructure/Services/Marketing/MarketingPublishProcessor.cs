using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Marketing;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.Services.Marketing;

public sealed class MarketingPublishProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<MarketingPublishProcessor> _logger;

    public MarketingPublishProcessor(IServiceScopeFactory scopes, ILogger<MarketingPublishProcessor> logger)
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
                _logger.LogWarning(ex, "Marketing publish processor pass failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(25), stoppingToken);
        }
    }

    private async Task ProcessOnceAsync(CancellationToken cancellationToken)
    {
        using var lookup = _scopes.CreateScope();
        var db = lookup.ServiceProvider.GetRequiredService<BusinessOSDbContext>();
        var now = DateTime.UtcNow;
        var due = await db.MarketingPublishJobs.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted
                && x.Status == "Pending"
                && x.RunAtUtc <= now
                && x.Attempt < x.MaxAttempts)
            .OrderBy(x => x.RunAtUtc)
            .Select(x => new { x.Id, x.TenantId })
            .Take(40)
            .ToListAsync(cancellationToken);

        foreach (var group in due.GroupBy(x => x.TenantId))
        {
            using var job = _scopes.CreateScope();
            var tenant = job.ServiceProvider.GetRequiredService<ITenantProvider>();
            tenant.SetTenantId(group.Key);
            var marketing = job.ServiceProvider.GetRequiredService<IMarketingOperationsService>();
            await marketing.ProcessDuePublishJobsAsync(cancellationToken);
        }
    }
}
