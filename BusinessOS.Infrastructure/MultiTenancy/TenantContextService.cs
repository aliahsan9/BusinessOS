using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.MultiTenancy;

public sealed class TenantContextService : ITenantContext
{
    private readonly ITenantProvider _tenantProvider;
    private readonly IDbContextFactory<BusinessOSDbContext> _dbContextFactory;

    private Guid _tenantId;
    private string _tenantName = string.Empty;
    private string _slug = string.Empty;
    private string? _subscriptionPlanName;
    private Guid? _subscriptionPlanId;
    private TenantLimits _limits = new(1, 25, 10, 100, 512, 0);
    private TenantUsageSnapshot? _usage;
    private bool _isActive;
    private bool _isLoaded;

    public TenantContextService(
        ITenantProvider tenantProvider,
        IDbContextFactory<BusinessOSDbContext> dbContextFactory)
    {
        _tenantProvider = tenantProvider;
        _dbContextFactory = dbContextFactory;
    }

    public Guid TenantId => _isLoaded ? _tenantId : _tenantProvider.TenantId;
    public string TenantName => _tenantName;
    public string Slug => _slug;
    public string? SubscriptionPlanName => _subscriptionPlanName;
    public Guid? SubscriptionPlanId => _subscriptionPlanId;
    public TenantLimits Limits => _limits;
    public TenantUsageSnapshot? Usage => _usage;
    public bool IsActive => _isActive;
    public bool IsLoaded => _isLoaded;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!_tenantProvider.HasTenant())
        {
            return;
        }

        _tenantId = _tenantProvider.TenantId;

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var tenant = await dbContext.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == _tenantId && !x.IsDeleted, cancellationToken);

        if (tenant is null)
        {
            _isLoaded = false;
            return;
        }

        var plan = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenant.SubscriptionPlanId, cancellationToken);

        var usage = await dbContext.TenantUsages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenant.Id && !x.IsDeleted, cancellationToken);

        _tenantName = tenant.Name;
        _slug = tenant.Slug;
        _subscriptionPlanName = plan?.Name ?? tenant.SubscriptionPlan;
        _subscriptionPlanId = tenant.SubscriptionPlanId;
        _isActive = tenant.IsActive && tenant.Status is Domain.Enums.TenantStatus.Active or Domain.Enums.TenantStatus.Trial;

        if (plan is not null)
        {
            _limits = new TenantLimits(
                plan.MaxUsers,
                plan.MaxCustomers,
                plan.MaxProjects,
                plan.MaxTasks,
                plan.MaxStorageMb,
                plan.MaxAiRequests);
        }

        if (usage is not null)
        {
            _usage = new TenantUsageSnapshot(
                usage.UserCount,
                usage.CustomerCount,
                usage.ProjectCount,
                usage.TaskCount,
                usage.StorageUsedMb,
                usage.AiRequestsUsed,
                usage.LastCalculatedAt);
        }

        _isLoaded = true;
    }
}
