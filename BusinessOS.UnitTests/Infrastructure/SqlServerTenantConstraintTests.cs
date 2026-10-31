using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
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

    [Fact]
    public async Task ConcurrentStockDeductions_CannotOversell()
    {
        var database = "BusinessOS_Qa_" + Guid.NewGuid().ToString("N");
        var connection =
            $"Server=(localdb)\\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
        var options = new DbContextOptionsBuilder<BusinessOSDbContext>()
            .UseSqlServer(connection)
            .Options;
        var tenantId = Guid.NewGuid();
        var provider = new TenantProvider();
        provider.SetTenantId(tenantId);

        await using var setup = new BusinessOSDbContext(options, provider);
        try
        {
            await setup.Database.EnsureCreatedAsync();
            var category = TestDataFactory.CreateCategory(tenantId);
            var product = TestDataFactory.CreateProduct(tenantId, category.Id, stock: 10);
            setup.Categories.Add(category);
            setup.Products.Add(product);
            setup.Inventories.Add(TestDataFactory.CreateInventory(tenantId, product.Id, 10));
            await setup.SaveChangesAsync();

            async Task<bool> DeductAsync()
            {
                await using var context = new BusinessOSDbContext(options, provider);
                var result = await context.TryAdjustStockAsync(product.Id, -7);
                return result.Applied;
            }

            var results = await Task.WhenAll(DeductAsync(), DeductAsync());
            results.Count(applied => applied).Should().Be(1);

            await using var verify = new BusinessOSDbContext(options, provider);
            var stock = await verify.Inventories.Where(x => x.ProductId == product.Id).Select(x => x.CurrentStock).SingleAsync();
            stock.Should().Be(3);
        }
        finally
        {
            await setup.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task IdempotencyUniqueIndex_RejectsASecondClaimForTheSameTenantOperationAndKey()
    {
        var database = "BusinessOS_Qa_" + Guid.NewGuid().ToString("N");
        var connection =
            $"Server=(localdb)\\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
        var options = new DbContextOptionsBuilder<BusinessOSDbContext>()
            .UseSqlServer(connection)
            .Options;
        var tenantId = Guid.NewGuid();
        var provider = new TenantProvider();
        provider.SetTenantId(tenantId);

        await using var setup = new BusinessOSDbContext(options, provider);
        try
        {
            await setup.Database.EnsureCreatedAsync();
            setup.IdempotencyRecords.Add(Record(tenantId, "payments.create", "same-key"));
            await setup.SaveChangesAsync();

            await using var other = new BusinessOSDbContext(options, provider);
            other.IdempotencyRecords.Add(Record(tenantId, "payments.create", "same-key"));
            var act = () => other.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateException>();
        }
        finally
        {
            await setup.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task MultiProductDeduction_RollsBackWhenALaterLineHasInsufficientStock()
    {
        var database = "BusinessOS_Qa_" + Guid.NewGuid().ToString("N");
        var connection =
            $"Server=(localdb)\\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
        var options = new DbContextOptionsBuilder<BusinessOSDbContext>()
            .UseSqlServer(connection)
            .Options;
        var tenantId = Guid.NewGuid();
        var provider = new TenantProvider();
        provider.SetTenantId(tenantId);

        await using var setup = new BusinessOSDbContext(options, provider);
        try
        {
            await setup.Database.EnsureCreatedAsync();
            var category = TestDataFactory.CreateCategory(tenantId);
            var first = TestDataFactory.CreateProduct(tenantId, category.Id, name: "First", sku: "SKU-A", stock: 10);
            var second = TestDataFactory.CreateProduct(tenantId, category.Id, name: "Second", sku: "SKU-B", stock: 1);
            setup.Categories.Add(category);
            setup.Products.AddRange(first, second);
            setup.Inventories.Add(TestDataFactory.CreateInventory(tenantId, first.Id, 10));
            setup.Inventories.Add(TestDataFactory.CreateInventory(tenantId, second.Id, 1));
            await setup.SaveChangesAsync();

            var inventory = TenantDatabase.Inventory(setup);
            var act = () => inventory.DeductForOrderAsync(
                new Order { Id = Guid.NewGuid(), TenantId = tenantId, OrderNumber = "ORD-ROLL" },
                [
                    new OrderItem { ProductId = first.Id, Quantity = 4 },
                    new OrderItem { ProductId = second.Id, Quantity = 5 }
                ]);

            await act.Should().ThrowAsync<BadRequestException>();

            await using var verify = new BusinessOSDbContext(options, provider);
            (await verify.Inventories.Where(x => x.ProductId == first.Id).Select(x => x.CurrentStock).SingleAsync()).Should().Be(10);
            (await verify.Inventories.Where(x => x.ProductId == second.Id).Select(x => x.CurrentStock).SingleAsync()).Should().Be(1);
        }
        finally
        {
            await setup.Database.EnsureDeletedAsync();
        }
    }

    private static IdempotencyRecord Record(Guid tenantId, string operation, string key) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Operation = operation,
            Key = key,
            RequestHash = "hash",
            State = IdempotencyState.Processing,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(90)
        };

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
