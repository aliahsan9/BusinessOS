using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Integrations;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.Integrations;

/// <summary>
/// Periodically syncs inbound Gmail for tenants with a Connected Gmail integration.
/// </summary>
public sealed class GmailInboundSyncProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<GmailInboundSyncProcessor> _logger;

    public GmailInboundSyncProcessor(IServiceScopeFactory scopes, ILogger<GmailInboundSyncProcessor> logger)
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
                _logger.LogWarning(ex, "Gmail inbound sync processor pass failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task ProcessOnceAsync(CancellationToken cancellationToken)
    {
        using var lookup = _scopes.CreateScope();
        var db = lookup.ServiceProvider.GetRequiredService<BusinessOSDbContext>();
        var connections = await db.CompanyIntegrationConnections.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted
                && x.ProviderKey == "gmail"
                && x.Status == IntegrationConnectionStatuses.Connected)
            .Select(x => new { x.Id, x.TenantId })
            .Take(40)
            .ToListAsync(cancellationToken);

        foreach (var group in connections.GroupBy(x => x.TenantId))
        {
            using var scope = _scopes.CreateScope();
            var tenant = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
            tenant.SetTenantId(group.Key);
            var platform = scope.ServiceProvider.GetRequiredService<IIntegrationPlatform>();
            try
            {
                await platform.ExecuteAsync("gmail", "syncInbound", "{\"maxResults\":\"10\",\"query\":\"is:unread newer_than:2d\"}", false, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Gmail sync failed for tenant {TenantId}", group.Key);
            }
        }
    }
}
