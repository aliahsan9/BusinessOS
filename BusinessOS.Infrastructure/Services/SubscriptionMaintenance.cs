using BusinessOS.Domain.Billing;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.Services;

public static class SubscriptionMaintenance
{
    public static async Task ApplyDueTransitionsAsync(
        BusinessOSDbContext db,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var free = await db.SubscriptionPlans.FirstOrDefaultAsync(x => x.Slug == "free", cancellationToken);

        var trials = await db.TenantSubscriptions.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted &&
                        x.Status == SubscriptionStatus.Trial &&
                        x.TrialEndDate != null &&
                        x.TrialEndDate < utcNow &&
                        x.StripeSubscriptionId == null)
            .ToListAsync(cancellationToken);

        foreach (var subscription in trials)
            await MoveToFreeAsync(db, subscription, free, "TrialExpired", SubscriptionStatus.Expired, utcNow, cancellationToken);

        var cancellations = await db.TenantSubscriptions.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted &&
                        x.CancelAtPeriodEnd &&
                        x.CurrentPeriodEnd != null &&
                        x.CurrentPeriodEnd < utcNow)
            .ToListAsync(cancellationToken);

        foreach (var subscription in cancellations)
            await MoveToFreeAsync(db, subscription, free, "CancellationApplied", SubscriptionStatus.Cancelled, utcNow, cancellationToken);

        if (trials.Count > 0 || cancellations.Count > 0)
            await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task MoveToFreeAsync(
        BusinessOSDbContext db,
        TenantSubscription subscription,
        SubscriptionPlan? free,
        string action,
        SubscriptionStatus status,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var from = SubscriptionStatusNames.ToApi(subscription.Status);
        subscription.Status = status;
        subscription.CancelAtPeriodEnd = false;
        subscription.AutoRenewEnabled = false;
        subscription.EndDate = utcNow;
        subscription.UpdatedAt = utcNow;
        if (free is not null)
        {
            subscription.SubscriptionPlanId = free.Id;
            subscription.PreserveContract = false;
            subscription.ContractedLimitsJson = null;
            var tenant = await db.Tenants.IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == subscription.TenantId, cancellationToken);
            if (tenant is not null)
            {
                tenant.SubscriptionPlanId = free.Id;
                tenant.SubscriptionPlan = free.Name;
                tenant.Status = TenantStatus.Active;
            }
        }

        db.SubscriptionHistories.Add(new SubscriptionHistory
        {
            TenantId = subscription.TenantId,
            SubscriptionPlanId = free?.Id,
            Action = action,
            FromStatus = from,
            ToStatus = SubscriptionStatusNames.ToApi(status),
            Notes = "Applied by subscription maintenance. No new charge was created.",
            CreatedAt = utcNow
        });
    }
}

public sealed class SubscriptionMaintenanceHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<SubscriptionMaintenanceHostedService> _logger;

    public SubscriptionMaintenanceHostedService(
        IServiceScopeFactory scopes,
        ILogger<SubscriptionMaintenanceHostedService> logger)
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
                var db = scope.ServiceProvider.GetRequiredService<BusinessOSDbContext>();
                await SubscriptionMaintenance.ApplyDueTransitionsAsync(db, DateTime.UtcNow, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Subscription maintenance failed.");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
