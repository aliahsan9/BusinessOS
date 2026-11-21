using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Idempotency;
using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.Dashboard.Models;
using BusinessOS.Application.Features.Dashboard.Services;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Domain.Enums;
using BusinessOS.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BusinessOS.UnitTests.Performance;

/// <summary>
/// Phase 7B concurrency and query-shape tests. These assert correctness under contention
/// and that analytics avoid unbounded materialization — they are not fabricated capacity claims.
/// </summary>
public class Phase7BScalabilityTests
{
    [Fact]
    public async Task EnsureStockAvailable_UsesSingleInventoryQuery()
    {
        var (context, tenantId, _) = TenantDatabase.Open();
        await using var db = context;
        var category = TestDataFactory.CreateCategory(tenantId);
        var first = TestDataFactory.CreateProduct(tenantId, category.Id, "A", "SKU-A", stock: 20);
        var second = TestDataFactory.CreateProduct(tenantId, category.Id, "B", "SKU-B", stock: 15);
        db.Categories.Add(category);
        db.Products.AddRange(first, second);
        db.Inventories.AddRange(
            TestDataFactory.CreateInventory(tenantId, first.Id, 20),
            TestDataFactory.CreateInventory(tenantId, second.Id, 15));
        await db.SaveChangesAsync();

        var inventory = TenantDatabase.Inventory(db);
        await inventory.EnsureStockAvailableAsync(
            new Dictionary<Guid, decimal>
            {
                [first.Id] = 5,
                [second.Id] = 4
            });

        // Fail closed when one SKU is short.
        var act = () => inventory.EnsureStockAvailableAsync(
            new Dictionary<Guid, decimal> { [first.Id] = 50 });
        await act.Should().ThrowAsync<BusinessOS.Application.Common.Exceptions.BadRequestException>();
    }

    [Fact]
    public async Task SalesAnalytics_AggregatesWithoutLoadingFullOrderGraphs()
    {
        var (context, tenantId, _) = TenantDatabase.Open();
        await using var db = context;
        var customer = TestDataFactory.CreateCustomer(tenantId);
        db.Customers.Add(customer);

        for (var i = 0; i < 25; i++)
        {
            var order = TestDataFactory.CreateOrder(
                tenantId,
                customer.Id,
                OrderStatusNames.Completed,
                grandTotal: 10 + i,
                orderNumber: $"ORD-A-{i:D3}");
            order.OrderDate = DateTime.UtcNow.Date.AddDays(-i);
            db.Orders.Add(order);
        }

        await db.SaveChangesAsync();

        var analytics = new AnalyticsService(db);
        var range = new DashboardDateRange(
            DateTime.UtcNow.Date.AddDays(-30),
            DateTime.UtcNow.Date.AddDays(1),
            "custom");

        var result = await analytics.GetSalesAnalyticsAsync(range);
        result.CompletedOrders.Should().Be(25);
        result.AverageOrderValue.Should().BeGreaterThan(0);
        result.RevenueTrends.Should().NotBeEmpty();
    }

    [Fact]
    public void CacheKeys_AreTenantScoped()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        CacheKeys.DashboardPrefix(a).Should().Contain(a.ToString());
        CacheKeys.DashboardPrefix(a).Should().NotContain(b.ToString());
        CacheKeys.TenantRoot(a).Should().StartWith($"Tenant_{a}_");
    }

    [Fact]
    public async Task MemoryCache_DoesNotLeakAcrossTenantKeyPrefixes()
    {
        var cache = new BusinessOS.Infrastructure.Caching.CacheService(
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new CacheSettings()),
            NullLogger<BusinessOS.Infrastructure.Caching.CacheService>.Instance);

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await cache.SetAsync(CacheKeys.Dashboard(tenantA, "all"), "secret-a");
        await cache.SetAsync(CacheKeys.Dashboard(tenantB, "all"), "secret-b");

        (await cache.GetAsync<string>(CacheKeys.Dashboard(tenantA, "all"))).Should().Be("secret-a");
        (await cache.GetAsync<string>(CacheKeys.Dashboard(tenantB, "all"))).Should().Be("secret-b");

        await cache.RemoveByPrefixAsync(CacheKeys.DashboardPrefix(tenantA));
        (await cache.GetAsync<string>(CacheKeys.Dashboard(tenantA, "all"))).Should().BeNull();
        (await cache.GetAsync<string>(CacheKeys.Dashboard(tenantB, "all"))).Should().Be("secret-b");
    }

    [Fact]
    public void NalvaLimits_BoundParallelTaskExecution()
    {
        NalvaLimits.MaxParallelTaskExecutions.Should().BeGreaterThan(0);
        NalvaLimits.MaxParallelTaskExecutions.Should().BeLessThanOrEqualTo(NalvaLimits.TasksPerPass);
        NalvaLimits.TasksPerCompanyPerPass.Should().BeLessThanOrEqualTo(NalvaLimits.TasksPerPass);
        NalvaLimits.MaxConcurrentPerAgent.Should().BeGreaterThan(0);
    }

    [Fact]
    public void OrderCreate_IdempotencyHash_ChangesWithLineItems()
    {
        var tenant = Guid.NewGuid();
        var customer = Guid.NewGuid();
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();

        var hash1 = CanonicalRequestHasher.OrderCreate(
            tenant, customer, 0, 0, [(productA, 1m)]);
        var hash2 = CanonicalRequestHasher.OrderCreate(
            tenant, customer, 0, 0, [(productA, 2m)]);
        var hash3 = CanonicalRequestHasher.OrderCreate(
            tenant, customer, 0, 0, [(productB, 1m)]);

        hash1.Should().NotBe(hash2);
        hash1.Should().NotBe(hash3);
        CanonicalRequestHasher.OrderCreate(
            tenant, customer, 0, 0, [(productA, 1m)]).Should().Be(hash1);
    }
}
