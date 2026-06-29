using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Tenant.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class TenantLimitService : ITenantLimitService
{
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
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);

        if (tenant?.Plan is null)
        {
            return new TenantLimits(1, 25, 10, 100, 512, 0);
        }

        return new TenantLimits(
            tenant.Plan.MaxUsers,
            tenant.Plan.MaxCustomers,
            tenant.Plan.MaxProjects,
            tenant.Plan.MaxTasks,
            tenant.Plan.MaxStorageMb,
            tenant.Plan.MaxAiRequests);
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

        var exceeded = resourceType.ToLowerInvariant() switch
        {
            "users" when !PlanLimitHelper.IsWithinLimit(usage.UserCount, limits.MaxUsers) =>
                $"User limit reached ({PlanLimitHelper.FormatLimit(limits.MaxUsers)}). Upgrade your plan to add more users.",
            "customers" when !PlanLimitHelper.IsWithinLimit(usage.CustomerCount, limits.MaxCustomers) =>
                $"Customer limit reached ({PlanLimitHelper.FormatLimit(limits.MaxCustomers)}). Upgrade your plan to add more customers.",
            "projects" when !PlanLimitHelper.IsWithinLimit(usage.ProjectCount, limits.MaxProjects) =>
                $"Project limit reached ({PlanLimitHelper.FormatLimit(limits.MaxProjects)}). Upgrade your plan to add more projects.",
            "tasks" when !PlanLimitHelper.IsWithinLimit(usage.TaskCount, limits.MaxTasks) =>
                $"Task limit reached ({PlanLimitHelper.FormatLimit(limits.MaxTasks)}). Upgrade your plan to add more tasks.",
            "storage" when limits.MaxStorageMb != SubscriptionPlan.Unlimited && usage.StorageUsedMb >= limits.MaxStorageMb =>
                $"Storage limit reached ({limits.MaxStorageMb} MB). Upgrade your plan for more storage.",
            "ai" when !PlanLimitHelper.IsUnlimited(limits.MaxAiRequests) && usage.AiRequestsUsed >= limits.MaxAiRequests =>
                $"AI request limit reached ({PlanLimitHelper.FormatLimit(limits.MaxAiRequests)}). Upgrade your plan for more AI requests.",
            _ => null
        };

        if (exceeded is not null)
        {
            throw new BadRequestException(exceeded);
        }
    }

    public async Task IncrementAiUsageAsync(CancellationToken cancellationToken = default)
    {
        if (!_tenantProvider.HasTenant())
        {
            return;
        }

        var usage = await _context.TenantUsages
            .FirstOrDefaultAsync(x => x.TenantId == _tenantProvider.TenantId, cancellationToken);

        if (usage is null)
        {
            return;
        }

        usage.AiRequestsUsed++;
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
        usage.ProjectCount = projectCount;
        usage.TaskCount = taskCount;
        usage.LastCalculatedAt = DateTime.UtcNow;
        usage.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
