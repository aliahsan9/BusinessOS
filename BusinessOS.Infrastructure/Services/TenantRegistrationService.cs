using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class TenantRegistrationService : ITenantRegistrationService
{
    private readonly IDbContextFactory<BusinessOSDbContext> _dbContextFactory;

    public TenantRegistrationService(IDbContextFactory<BusinessOSDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<Guid> CreateTenantAsync(
        Guid tenantId,
        string businessName,
        string email,
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var tenant = new Tenant
        {
            Id = tenantId,
            Name = businessName,
            BusinessType = "General",
            Email = email,
            Phone = string.Empty,
            Address = string.Empty,
            SubscriptionPlan = "Free",
            IsActive = true,
            OwnerUserId = ownerUserId
        };

        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync(cancellationToken);

        return tenant.Id;
    }
}
