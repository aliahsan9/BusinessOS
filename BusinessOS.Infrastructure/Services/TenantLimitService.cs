using System.Collections.Concurrent;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Application.Features.Tenant.Services;
using BusinessOS.Domain.Billing;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class TenantLimitService : ITenantLimitService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> AiLocks = new();

    private readonly IApplicationDbContext _context;
    private readonly ITenantProvider _tenantProvider;
    private readonly IDbContextFactory<BusinessOSDbContext> _dbContextFactory;

    public TenantLimitService(
        IApplicationDbContext context,
        ITenantProvider tenantProvider,
        IDbContextFactory<BusinessOSDbContext> dbContextFactory)
    {
        _context = context;
        _tenantProvider = tenantProvider;
        _dbContextFactory = dbContextFactory;
    }

    public async Task<TenantLimits> GetLimitsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var tenant = await dbContext.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(x => x.Plan)
            .Include(x => x.Subscription)
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);

        if (tenant?.Plan is null)
            return new TenantLimits(1, 25, 3, 100, 512, 20, 50);

        return EntitlementResolver.Resolve(tenant.Plan, tenant.Subscription);
    }

    public async Task EnsureWithinLimitAsync(string resourceType, CancellationToken cancellationToken = default)
    {
        if (!_tenantProvider.HasTenant())
        {
            return;
        }

        var tenantId = _tenantProvider.TenantId;
        var limits = await GetLimitsAsync(tenantId, cancellationToken);
        await RefreshUsageAsync(tenantId, cancellationToken);

        var usage = await _context.TenantUsages
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        if (usage is null)
        {
            return;
        }

        var resource = resourceType.ToLowerInvariant();
        var (current, maximum, blocked) = resource switch
        {
            "users" => (usage.UserCount, (long)limits.MaxUsers, !PlanLimitHelper.IsWithinLimit(usage.UserCount, limits.MaxUsers)),
            "customers" => (usage.CustomerCount, (long)limits.MaxCustomers, !PlanLimitHelper.IsWithinLimit(usage.CustomerCount, limits.MaxCustomers)),
            "products" => (usage.ProductCount, (long)limits.MaxProducts, !PlanLimitHelper.IsWithinLimit(usage.ProductCount, limits.MaxProducts)),
            "projects" => (usage.ProjectCount, (long)limits.MaxProjects, !PlanLimitHelper.IsWithinLimit(usage.ProjectCount, limits.MaxProjects)),
            "tasks" => (usage.TaskCount, (long)limits.MaxTasks, !PlanLimitHelper.IsWithinLimit(usage.TaskCount, limits.MaxTasks)),
            "storage" => (usage.StorageUsedMb, limits.MaxStorageMb, limits.MaxStorageMb != SubscriptionPlan.Unlimited && usage.StorageUsedMb >= limits.MaxStorageMb),
            "ai" when limits.MaxAiRequests == 0 => (usage.AiRequestsUsed, 0L, true),
            "ai" => (usage.AiRequestsUsed, (long)limits.MaxAiRequests, !PlanLimitHelper.IsUnlimited(limits.MaxAiRequests) && usage.AiRequestsUsed >= limits.MaxAiRequests),
            _ => (0L, 0L, false)
        };

        if (blocked)
        {
            throw new PlanLimitExceededException(resource, current, maximum);
        }
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
        var gate = AiLocks.GetOrAdd(tenantId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var limits = await GetLimitsAsync(tenantId, cancellationToken);
            var usage = await LoadUsageAsync(dbContext, tenantId, cancellationToken);
            await ResetAiPeriodIfNeededAsync(dbContext, tenantId, usage, cancellationToken);

            if (!string.IsNullOrWhiteSpace(operationKey))
            {
                var duplicate = await dbContext.AiUsageLedgers
                    .IgnoreQueryFilters()
                    .AnyAsync(x => x.TenantId == tenantId && x.OperationKey == operationKey && !x.IsDeleted, cancellationToken);
                if (duplicate)
                    return;
            }

            if (limits.MaxAiRequests == 0 ||
                (!PlanLimitHelper.IsUnlimited(limits.MaxAiRequests) && usage.AiRequestsUsed >= limits.MaxAiRequests))
            {
                throw new PlanLimitExceededException("ai", usage.AiRequestsUsed, limits.MaxAiRequests);
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
        }
        finally
        {
            gate.Release();
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

        var userCount = await dbContext.Users
            .IgnoreQueryFilters()
            .CountAsync(x => x.TenantId == tenantId && x.IsActive, cancellationToken);

        var customerCount = await dbContext.Customers
            .IgnoreQueryFilters()
            .CountAsync(x => x.TenantId == tenantId && !x.IsDeleted, cancellationToken);

        var projectCount = await dbContext.Projects
            .IgnoreQueryFilters()
            .CountAsync(x => x.TenantId == tenantId && !x.IsDeleted, cancellationToken);

        var taskCount = await dbContext.WorkTasks
            .IgnoreQueryFilters()
            .CountAsync(x => x.TenantId == tenantId && !x.IsDeleted, cancellationToken);

        var productCount = await dbContext.Products
            .IgnoreQueryFilters()
            .CountAsync(x => x.TenantId == tenantId && !x.IsDeleted, cancellationToken);

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
