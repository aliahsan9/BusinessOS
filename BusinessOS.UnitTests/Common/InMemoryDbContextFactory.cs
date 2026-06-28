using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.UnitTests.Common;

internal static class InMemoryDbContextFactory
{
    public static (BusinessOSDbContext Context, Guid TenantId, TenantProvider TenantProvider) Create(
        Guid? tenantId = null)
    {
        var resolvedTenantId = tenantId ?? Guid.NewGuid();
        var tenantProvider = new TenantProvider();
        tenantProvider.SetTenantId(resolvedTenantId);

        var options = new DbContextOptionsBuilder<BusinessOSDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return (new BusinessOSDbContext(options, tenantProvider), resolvedTenantId, tenantProvider);
    }
}
