using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Inventory.Services;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.MultiTenancy;
using BusinessOS.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BusinessOS.UnitTests.Common;

internal static class TenantDatabase
{
    public static (BusinessOSDbContext Context, Guid TenantId, TenantProvider Provider) Open(
        Guid? tenantId = null,
        string? name = null)
    {
        var id = tenantId ?? Guid.NewGuid();
        var provider = new TenantProvider();
        provider.SetTenantId(id);

        var options = new DbContextOptionsBuilder<BusinessOSDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;

        return (new BusinessOSDbContext(options, provider), id, provider);
    }

    public static InventoryService Inventory(BusinessOSDbContext context)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns("user-1");

        return new InventoryService(
            new InventoryRepository(context),
            new StockTransactionRepository(context),
            context,
            currentUser.Object,
            NullLogger<InventoryService>.Instance);
    }
}
