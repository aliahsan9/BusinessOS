using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.MultiTenancy;
using BusinessOS.UnitTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.UnitTests.Infrastructure;

public class SqlServerTenantConstraintTests
{
    [Fact]
    public async Task SkuUniqueIndex_RejectsDuplicatesInsideATenant_AndAllowsThemAcrossTenants()
    {
        var database = "BusinessOS_Qa_" + Guid.NewGuid().ToString("N");
        var connection =
            $"Server=(localdb)\\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
        var options = new DbContextOptionsBuilder<BusinessOSDbContext>()
            .UseSqlServer(connection)
            .Options;

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var providerA = new TenantProvider();
        providerA.SetTenantId(tenantA);
        var providerB = new TenantProvider();
        providerB.SetTenantId(tenantB);

        await using var contextA = new BusinessOSDbContext(options, providerA);
        await using var contextB = new BusinessOSDbContext(options, providerB);
        try
        {
            await contextA.Database.EnsureCreatedAsync();
            var categoryA = TestDataFactory.CreateCategory(tenantA, "A");
            var categoryB = TestDataFactory.CreateCategory(tenantB, "B");
            contextA.Categories.Add(categoryA);
            contextB.Categories.Add(categoryB);
            await contextA.SaveChangesAsync();
            await contextB.SaveChangesAsync();

            contextA.Products.Add(Product(categoryA.Id, "SKU-SHARED"));
            contextB.Products.Add(Product(categoryB.Id, "SKU-SHARED"));
            await contextA.SaveChangesAsync();
            await contextB.SaveChangesAsync();

            contextA.Products.Add(Product(categoryA.Id, "SKU-SHARED"));
            var duplicate = () => contextA.SaveChangesAsync();
            await duplicate.Should().ThrowAsync<DbUpdateException>();

            (await contextA.Products.CountAsync(x => x.SKU == "SKU-SHARED")).Should().Be(1);
            (await contextB.Products.CountAsync(x => x.SKU == "SKU-SHARED")).Should().Be(1);
        }
        finally
        {
            await contextA.Database.EnsureDeletedAsync();
        }
    }

    private static Product Product(Guid categoryId, string sku) =>
        new()
        {
            Id = Guid.NewGuid(),
            CategoryId = categoryId,
            Name = "Widget",
            SKU = sku,
            CostPrice = 1,
            SalePrice = 2,
            IsActive = true
        };
}
