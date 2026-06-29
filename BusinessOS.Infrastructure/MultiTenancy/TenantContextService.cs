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
            .Include(x => x.Plan)
            .Include(x => x.Usage)
            .FirstOrDefaultAsync(x => x.Id == _tenantId && !x.IsDeleted, cancellationToken);

        if (tenant is null)
        {
            _isLoaded = false;
            return;
        }

        _tenantName = tenant.Name;
        _slug = tenant.Slug;
        _subscriptionPlanName = tenant.Plan?.Name ?? tenant.SubscriptionPlan;
        _subscriptionPlanId = tenant.SubscriptionPlanId;
        _isActive = tenant.IsActive && tenant.Status is Domain.Enums.TenantStatus.Active or Domain.Enums.TenantStatus.Trial;

        if (tenant.Plan is not null)
        {
            _limits = new TenantLimits(
                tenant.Plan.MaxUsers,
                tenant.Plan.MaxCustomers,
                tenant.Plan.MaxProjects,
                tenant.Plan.MaxTasks,
                tenant.Plan.MaxStorageMb,
                tenant.Plan.MaxAiRequests);
        }

        if (tenant.Usage is not null)
        {
            _usage = new TenantUsageSnapshot(
                tenant.Usage.UserCount,
                tenant.Usage.CustomerCount,
                tenant.Usage.ProjectCount,
                tenant.Usage.TaskCount,
                tenant.Usage.StorageUsedMb,
                tenant.Usage.AiRequestsUsed,
                tenant.Usage.LastCalculatedAt);
        }

        _isLoaded = true;
    }
}
