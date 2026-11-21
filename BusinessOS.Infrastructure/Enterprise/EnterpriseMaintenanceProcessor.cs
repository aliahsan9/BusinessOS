using BusinessOS.Application.Features.Enterprise;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.Enterprise;

/// <summary>Escalates overdue approvals and purges expired enterprise audit rows.</summary>
public sealed class EnterpriseMaintenanceProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EnterpriseMaintenanceProcessor> _logger;

    public EnterpriseMaintenanceProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<EnterpriseMaintenanceProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<BusinessOSDbContext>();
                var tenantProvider = scope.ServiceProvider.GetRequiredService<Application.Common.Interfaces.ITenantProvider>();
                var approvals = scope.ServiceProvider.GetRequiredService<IApprovalGovernanceService>();
                var audit = scope.ServiceProvider.GetRequiredService<IEnterpriseAuditService>();

                var tenantIds = await db.TenantEnterpriseSecuritySettings.IgnoreQueryFilters()
                    .Where(x => !x.IsDeleted)
                    .Select(x => x.TenantId)
                    .Distinct()
                    .ToListAsync(stoppingToken);

                foreach (var tenantId in tenantIds)
                {
                    tenantProvider.SetTenantId(tenantId);
                    await approvals.EscalateOverdueAsync(stoppingToken);
                }

                await audit.PurgeExpiredAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Enterprise maintenance cycle failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }
}
