using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Billing.DTOs;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Domain.Billing;
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

        decimal mrr = 0;
        decimal trial = 0;
        decimal atRisk = 0;
        foreach (var sub in subscriptions.Where(x => x.Plan is not null))
        {
            var monthly = SubscriptionRevenueMath.MonthlyRecurring(sub.Plan, sub);
            if (SubscriptionRevenueMath.CountsAsRecurring(sub.Status))
                mrr += monthly;
            else if (SubscriptionRevenueMath.CountsAsTrialPipeline(sub.Status))
                trial += monthly;
            else if (SubscriptionRevenueMath.CountsAsAtRisk(sub.Status))
                atRisk += monthly;
        }

        var collected = await db.BillingTransactions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.Status == BillingTransactionStatus.Completed)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        var booked = await db.BillingInvoices
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.Status != BillingInvoiceStatus.Void)
            .SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0m;

        var failed = await db.BillingTransactions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted && x.Status == BillingTransactionStatus.Failed, cancellationToken);

        var ai = await db.AiUsageLedgers.IgnoreQueryFilters().AsNoTracking()
            .Where(x => !x.IsDeleted)
            .SumAsync(x => (decimal?)x.EstimatedProviderCostUsd, cancellationToken) ?? 0m;
        var agent = await db.NalvaExecutions.IgnoreQueryFilters().AsNoTracking()
            .Where(x => !x.IsDeleted)
            .SumAsync(x => (decimal?)x.EstimatedCostUsd, cancellationToken) ?? 0m;

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var totalTenants = await db.Tenants.IgnoreQueryFilters().CountAsync(x => !x.IsDeleted, cancellationToken);

        return new BillingMetricsDto
        {
            Mrr = mrr,
            Arr = mrr * 12,
            ActiveSubscriptions = subscriptions.Count(x => x.Status == SubscriptionStatus.Active),
            TrialSubscriptions = subscriptions.Count(x => x.Status == SubscriptionStatus.Trial),
            CancelledSubscriptions = subscriptions.Count(x => x.Status == SubscriptionStatus.Cancelled),
            TotalRevenue = collected,
            CollectedRevenue = collected,
            BookedRevenue = booked,
            TrialPipelineMonthly = trial,
            AtRiskMonthly = atRisk,
            EstimatedAiCostUsd = ai + agent,
            FailedPayments = failed,
            NewSubscriptionsThisMonth = subscriptions.Count(x => x.Status == SubscriptionStatus.Active && x.StartDate >= monthStart),
            ChurnedThisMonth = subscriptions.Count(x => x.Status == SubscriptionStatus.Cancelled && x.EndDate >= monthStart),
            TotalTenants = totalTenants
        };
    }
}
