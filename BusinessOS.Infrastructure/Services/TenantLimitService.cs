using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Application.Features.Tenant.Services;
using BusinessOS.Domain.Billing;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Billing;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class TenantLimitService : ITenantLimitService
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantProvider _tenantProvider;
    private readonly IDbContextFactory<BusinessOSDbContext> _dbContextFactory;
    private readonly ISubscriptionEntitlementService _entitlements;

    public TenantLimitService(
        IApplicationDbContext context,
        ITenantProvider tenantProvider,
        IDbContextFactory<BusinessOSDbContext> dbContextFactory)
        : this(context, tenantProvider, dbContextFactory, new SubscriptionEntitlementService(dbContextFactory, tenantProvider))
    {
    }

    public TenantLimitService(
        IApplicationDbContext context,
        ITenantProvider tenantProvider,
        IDbContextFactory<BusinessOSDbContext> dbContextFactory,
        ISubscriptionEntitlementService entitlements)
    {
        _context = context;
        _tenantProvider = tenantProvider;
        _dbContextFactory = dbContextFactory;
        _entitlements = entitlements;
    }

    public Task<TenantLimits> GetLimitsAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        _entitlements.GetLimitsAsync(tenantId, cancellationToken);

    public async Task EnsureWithinLimitAsync(string resourceType, CancellationToken cancellationToken = default)
    {
        if (!_tenantProvider.HasTenant())
            return;

        var resource = resourceType.Trim().ToLowerInvariant() switch
        {
            "users" or "user" => QuotaResource.Users,
            "customers" or "customer" => QuotaResource.Customers,
            "products" or "product" => QuotaResource.Products,
            "projects" or "project" => QuotaResource.Projects,
            "tasks" or "task" => QuotaResource.Tasks,
            "storage" => QuotaResource.Storage,
            "ai" => QuotaResource.AiRequests,
            _ => (QuotaResource?)null
        };

        if (resource is null)
            return;

        var additional = resource == QuotaResource.Storage ? 1L : 1L;
        await _entitlements.EnsureCanCreateAsync(resource.Value, additional, cancellationToken);
    }

    public Task IncrementAiUsageAsync(CancellationToken cancellationToken = default) =>
        TryConsumeAiRequestAsync(null, cancellationToken: cancellationToken);

    public async Task TryConsumeAiRequestAsync(
        string? operationKey,
        int? inputTokens = null,
        int? outputTokens = null,
        int? totalTokens = null,
        decimal? estimatedProviderCostUsd = null,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantProvider.HasTenant())
            return;

        var tenantId = _tenantProvider.TenantId;
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var hold = await QuotaEnforcer.LockTenantAsync(dbContext, tenantId, cancellationToken);
        {
            var limits = await GetLimitsAsync(tenantId, cancellationToken);
            var usage = await LoadUsageAsync(dbContext, tenantId, cancellationToken);
            await ResetAiPeriodIfNeededAsync(dbContext, tenantId, usage, cancellationToken);

            if (!string.IsNullOrWhiteSpace(operationKey))
            {
                var duplicate = await dbContext.AiUsageLedgers
                    .IgnoreQueryFilters()
                    .AnyAsync(x => x.TenantId == tenantId && x.OperationKey == operationKey && !x.IsDeleted, cancellationToken);
                if (duplicate)
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                    hold.MarkSuccess();
                    return;
                }
            }

            if (limits.MaxAiRequests == 0 ||
                (!PlanLimitHelper.IsUnlimited(limits.MaxAiRequests) && usage.AiRequestsUsed >= limits.MaxAiRequests))
            {
                throw new PlanLimitExceededException("AI requests", usage.AiRequestsUsed, limits.MaxAiRequests);
            }

            usage.AiRequestsUsed++;
            ApplyTokenTotals(usage, inputTokens, outputTokens, totalTokens, estimatedProviderCostUsd);
            usage.UpdatedAt = DateTime.UtcNow;

            dbContext.AiUsageLedgers.Add(new AiUsageLedger
            {
                TenantId = tenantId,
                OperationKey = string.IsNullOrWhiteSpace(operationKey) ? Guid.NewGuid().ToString("N") : operationKey,
                InputTokens = inputTokens ?? 0,
                OutputTokens = outputTokens ?? 0,
                TotalTokens = totalTokens ?? ((inputTokens ?? 0) + (outputTokens ?? 0)),
                EstimatedProviderCostUsd = estimatedProviderCostUsd ?? 0,
                ProviderFailed = false,
                PeriodStart = usage.AiPeriodStart ?? DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            hold.MarkSuccess();
        }
    }

    public async Task RecordAiProviderUsageAsync(
        int? inputTokens,
        int? outputTokens,
        int? totalTokens,
        decimal? estimatedProviderCostUsd,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantProvider.HasTenant())
            return;

        if (inputTokens is null && outputTokens is null && totalTokens is null && estimatedProviderCostUsd is null)
            return;

        var usage = await _context.TenantUsages
            .FirstOrDefaultAsync(x => x.TenantId == _tenantProvider.TenantId, cancellationToken);
        if (usage is null)
            return;

        ApplyTokenTotals(usage, inputTokens, outputTokens, totalTokens, estimatedProviderCostUsd);
        usage.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RefreshUsageAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var measured = await QuotaEnforcer.MeasureAsync(dbContext, tenantId, cancellationToken);
        var userCount = measured.OccupiedSeats;
        var customerCount = measured.Customers;
        var projectCount = measured.Projects;
        var taskCount = measured.Tasks;
        var productCount = measured.Products;

        var usage = await dbContext.TenantUsages
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        if (usage is null)
        {
            usage = new TenantUsage
            {
                TenantId = tenantId,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.TenantUsages.Add(usage);
        }

        usage.UserCount = userCount;
        usage.CustomerCount = customerCount;
        usage.ProductCount = productCount;
        usage.ProjectCount = projectCount;
        usage.TaskCount = taskCount;
        usage.StorageUsedMb = measured.StorageBytes / (1024L * 1024L);
        usage.LastCalculatedAt = DateTime.UtcNow;
        usage.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<TenantUsage> LoadUsageAsync(
        BusinessOSDbContext dbContext,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var usage = await dbContext.TenantUsages
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && !x.IsDeleted, cancellationToken);

        if (usage is not null)
            return usage;

        usage = new TenantUsage
        {
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.TenantUsages.Add(usage);
        return usage;
    }

    private static async Task ResetAiPeriodIfNeededAsync(
        BusinessOSDbContext dbContext,
        Guid tenantId,
        TenantUsage usage,
        CancellationToken cancellationToken)
    {
        var subscription = await dbContext.TenantSubscriptions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && !x.IsDeleted, cancellationToken);

        var periodStart = ResolveAiPeriodStart(subscription, DateTime.UtcNow);
        if (usage.AiPeriodStart is not null && usage.AiPeriodStart >= periodStart)
            return;

        usage.AiRequestsUsed = 0;
        usage.AiInputTokens = 0;
        usage.AiOutputTokens = 0;
        usage.AiTotalTokens = 0;
        usage.EstimatedProviderCostUsd = 0;
        usage.AiPeriodStart = periodStart;
    }

    internal static DateTime ResolveAiPeriodStart(TenantSubscription? subscription, DateTime utcNow)
    {
        if (subscription?.CurrentPeriodEnd is DateTime end && end > utcNow)
        {
            return subscription.BillingInterval == BillingInterval.Yearly
                ? end.AddYears(-1)
                : end.AddMonths(-1);
        }

        return new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private static void ApplyTokenTotals(
        TenantUsage usage,
        int? inputTokens,
        int? outputTokens,
        int? totalTokens,
        decimal? estimatedProviderCostUsd)
    {
        if (inputTokens is > 0)
            usage.AiInputTokens += inputTokens.Value;
        if (outputTokens is > 0)
            usage.AiOutputTokens += outputTokens.Value;

        var total = totalTokens ?? ((inputTokens ?? 0) + (outputTokens ?? 0));
        if (total > 0)
            usage.AiTotalTokens += total;
        if (estimatedProviderCostUsd is > 0)
            usage.EstimatedProviderCostUsd += estimatedProviderCostUsd.Value;
    }
}
