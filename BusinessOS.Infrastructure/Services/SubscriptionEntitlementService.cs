using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Domain.Billing;
using BusinessOS.Infrastructure.Billing;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class SubscriptionEntitlementService : ISubscriptionEntitlementService
{
    private readonly IDbContextFactory<BusinessOSDbContext> _dbContextFactory;
    private readonly ITenantProvider _tenantProvider;

    public SubscriptionEntitlementService(
        IDbContextFactory<BusinessOSDbContext> dbContextFactory,
        ITenantProvider tenantProvider)
    {
        _dbContextFactory = dbContextFactory;
        _tenantProvider = tenantProvider;
    }

    public async Task<TenantLimits> GetLimitsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await QuotaEnforcer.TryGetLimitsAsync(db, tenantId, cancellationToken)
            ?? new TenantLimits(1, 25, 3, 100, 512, 20, 50);
    }

    public async Task<ResourceUsage> GetUsageAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await QuotaEnforcer.MeasureAsync(db, tenantId, cancellationToken);
    }

    public async Task EnsureCanCreateAsync(
        QuotaResource resource,
        long additionalUnits = 1,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantProvider.HasTenant())
            throw new BadRequestException("Tenant context is required.");

        var tenantId = _tenantProvider.TenantId;
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var hold = await QuotaEnforcer.LockTenantAsync(db, tenantId, cancellationToken);

        var limits = await QuotaEnforcer.TryGetLimitsAsync(db, tenantId, cancellationToken);
        if (limits is null)
        {
            hold.MarkSuccess();
            return;
        }

        var usage = await QuotaEnforcer.MeasureAsync(db, tenantId, cancellationToken);
        if (resource == QuotaResource.Storage)
        {
            var maxBytes = QuotaEnforcer.MegabytesToBytes(QuotaEnforcer.LimitFor(limits, resource));
            if (QuotaEnforcer.Exceeds(usage.StorageBytes, additionalUnits, maxBytes))
            {
                throw new PlanLimitExceededException(
                    "Storage",
                    QuotaEnforcer.ToDisplayMb(usage.StorageBytes),
                    limits.MaxStorageMb);
            }
        }
        else
        {
            QuotaEnforcer.ThrowIfExceeded(
                resource,
                QuotaEnforcer.CurrentFor(usage, resource),
                additionalUnits,
                QuotaEnforcer.LimitFor(limits, resource));
        }

        hold.MarkSuccess();
    }
}
