using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Billing.DTOs;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class BillingMetricsService : IBillingMetricsService
{
    private readonly IDbContextFactory<BusinessOSDbContext> _dbContextFactory;

    public BillingMetricsService(IDbContextFactory<BusinessOSDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<BillingMetricsDto> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var subscriptions = await db.TenantSubscriptions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(x => x.Plan)
            .ToListAsync(cancellationToken);

        var active = subscriptions.Where(x =>
            x.Status is SubscriptionStatus.Active or SubscriptionStatus.Trial).ToList();

        decimal mrr = 0;
        foreach (var sub in active)
        {
            mrr += sub.BillingInterval == BillingInterval.Yearly
                ? sub.Plan.AnnualPrice / 12m
                : sub.Plan.MonthlyPrice;
        }

        var totalRevenue = await db.BillingTransactions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.Status == BillingTransactionStatus.Completed)
            .SumAsync(x => x.Amount, cancellationToken);

        var totalTenants = await db.Tenants.IgnoreQueryFilters().CountAsync(x => !x.IsDeleted, cancellationToken);

        return new BillingMetricsDto
        {
            Mrr = mrr,
            Arr = mrr * 12,
            ActiveSubscriptions = subscriptions.Count(x => x.Status == SubscriptionStatus.Active),
            TrialSubscriptions = subscriptions.Count(x => x.Status == SubscriptionStatus.Trial),
            CancelledSubscriptions = subscriptions.Count(x => x.Status == SubscriptionStatus.Cancelled),
            TotalRevenue = totalRevenue,
            TotalTenants = totalTenants
        };
    }
}
