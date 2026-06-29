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
            return new TenantLimits(3, 50, 5, 512, 50);
        }

        return new TenantLimits(
            tenant.Plan.MaxUsers,
            tenant.Plan.MaxCustomers,
            tenant.Plan.MaxProjects,
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
            "users" when usage.UserCount >= limits.MaxUsers =>
                $"User limit reached ({limits.MaxUsers}). Upgrade your plan to add more users.",
            "customers" when usage.CustomerCount >= limits.MaxCustomers =>
                $"Customer limit reached ({limits.MaxCustomers}). Upgrade your plan to add more customers.",
            "projects" when usage.ProjectCount >= limits.MaxProjects =>
                $"Project limit reached ({limits.MaxProjects}). Upgrade your plan to add more projects.",
            "storage" when usage.StorageUsedMb >= limits.MaxStorageMb =>
                $"Storage limit reached ({limits.MaxStorageMb} MB). Upgrade your plan for more storage.",
            "ai" when usage.AiRequestsUsed >= limits.MaxAiRequests =>
                $"AI request limit reached ({limits.MaxAiRequests}). Upgrade your plan for more AI requests.",
            _ => null
        };

        if (exceeded is not null)
        {
            throw new BadRequestException(exceeded);
        }
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
        usage.LastCalculatedAt = DateTime.UtcNow;
        usage.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
