using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Idempotency;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Inventory.Services;
using BusinessOS.Application.Features.Orders.Commands.UpdateOrderStatus;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using BusinessOS.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace BusinessOS.UnitTests.Concurrency;

public class InventoryConcurrencyTests
{
    [Fact]
    public async Task SecondDeduction_IsRejectedWhenStockWouldGoNegative()
    {
        var (context, tenantId, _) = TenantDatabase.Open();
        await using var db = context;
        var category = TestDataFactory.CreateCategory(tenantId);
        var product = TestDataFactory.CreateProduct(tenantId, category.Id, stock: 10);
        db.Categories.Add(category);
        db.Products.Add(product);
        db.Inventories.Add(TestDataFactory.CreateInventory(tenantId, product.Id, 10));
        await db.SaveChangesAsync();

        var inventory = TenantDatabase.Inventory(db);
        var order = new Order
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderNumber = "ORD-1",
            Status = OrderStatusNames.Pending
        };

        await inventory.DeductForOrderAsync(order, [Line(product.Id, 7)]);

        var act = () => inventory.DeductForOrderAsync(order, [Line(product.Id, 7)]);

        await act.Should().ThrowAsync<BadRequestException>();
        (await ReloadStock(db, product.Id)).Should().Be(3);
    }

    [Fact]
    public async Task DuplicateLines_AreDeductedOnce()
    {
        var (context, tenantId, _) = TenantDatabase.Open();
        await using var db = context;
        var category = TestDataFactory.CreateCategory(tenantId);
        var product = TestDataFactory.CreateProduct(tenantId, category.Id, stock: 10);
        db.Categories.Add(category);
        db.Products.Add(product);
        db.Inventories.Add(TestDataFactory.CreateInventory(tenantId, product.Id, 10));
        await db.SaveChangesAsync();

        var inventory = TenantDatabase.Inventory(db);
        await inventory.DeductForOrderAsync(
            new Order { Id = Guid.NewGuid(), TenantId = tenantId, OrderNumber = "ORD-2" },
            [Line(product.Id, 3), Line(product.Id, 4)]);

        (await ReloadStock(db, product.Id)).Should().Be(3);
    }

    [Fact]
    public async Task RepeatedCancellation_DoesNotRestoreStockTwice()
    {
        var (context, tenantId, provider) = TenantDatabase.Open();
        await using var db = context;
        var category = TestDataFactory.CreateCategory(tenantId);
        var product = TestDataFactory.CreateProduct(tenantId, category.Id, stock: 3);
        var order = new Order
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerId = Guid.NewGuid(),
            OrderNumber = "ORD-3",
            Status = OrderStatusNames.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        order.OrderItems.Add(new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            ProductId = product.Id,
            Quantity = 7,
            UnitPrice = 1,
            TenantId = tenantId
        });
        db.Categories.Add(category);
        db.Products.Add(product);
        db.Inventories.Add(TestDataFactory.CreateInventory(tenantId, product.Id, 3));
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var handler = new UpdateOrderStatusCommandHandler(
            db,
            TenantDatabase.Inventory(db),
            TestHandlerDependencies.CreateBusinessEvents(),
            TestHandlerDependencies.CreateCache(),
            provider,
            NullLogger<UpdateOrderStatusCommandHandler>.Instance);

        await handler.Handle(new UpdateOrderStatusCommand(order.Id, OrderStatusNames.Cancelled), CancellationToken.None);
        await handler.Handle(new UpdateOrderStatusCommand(order.Id, OrderStatusNames.Cancelled), CancellationToken.None);

        (await ReloadStock(db, product.Id)).Should().Be(10);
    }

    private static OrderItem Line(Guid productId, decimal quantity) =>
        new() { ProductId = productId, Quantity = quantity };

    private static async Task<decimal> ReloadStock(BusinessOSDbContext db, Guid productId)
    {
        var inventory = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .FirstAsync(db.Inventories, x => x.ProductId == productId);
        return inventory.CurrentStock;
    }
}

public class PaymentIdempotencyTests
{
    [Fact]
    public async Task SameKeyAndHash_ReplaysTheCompletedResult()
    {
        var (context, _, provider) = TenantDatabase.Open();
        await using var db = context;
        var service = Service(db, provider);
        var hash = CanonicalRequestHasher.PaymentCreate(provider.TenantId, Guid.NewGuid(), Guid.NewGuid(), 10, "USD", "Cash", DateTime.UtcNow.Date, null);

        var first = await service.BeginLocalAsync(IdempotencyOperations.PaymentCreate, "key-1", hash);
        service.CompleteLocal(first.Record!, 201, """{"id":"abc"}""", Guid.NewGuid());
        await db.SaveChangesAsync();

        var second = await service.BeginLocalAsync(IdempotencyOperations.PaymentCreate, "key-1", hash);

        second.Kind.Should().Be(IdempotencyOutcomeKind.Replay);
        second.ResponseBody.Should().Be("""{"id":"abc"}""");
        db.IdempotencyRecords.Count().Should().Be(1);
    }

    [Fact]
    public async Task SameKeyDifferentAmount_IsAConflict()
    {
        var (context, _, provider) = TenantDatabase.Open();
        await using var db = context;
        var service = Service(db, provider);
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var when = DateTime.UtcNow.Date;
        var original = CanonicalRequestHasher.PaymentCreate(provider.TenantId, orderId, customerId, 10, "USD", "Cash", when, null);
        var changed = CanonicalRequestHasher.PaymentCreate(provider.TenantId, orderId, customerId, 11, "USD", "Cash", when, null);

        var first = await service.BeginLocalAsync(IdempotencyOperations.PaymentCreate, "key-2", original);
        service.CompleteLocal(first.Record!, 201, """{"id":"abc"}""", Guid.NewGuid());
        await db.SaveChangesAsync();

        var second = await service.BeginLocalAsync(IdempotencyOperations.PaymentCreate, "key-2", changed);

        second.Kind.Should().Be(IdempotencyOutcomeKind.Conflict);
    }

    [Fact]
    public async Task SameKeyDifferentCurrency_IsAConflict()
    {
        var (context, _, provider) = TenantDatabase.Open();
        await using var db = context;
        var service = Service(db, provider);
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var when = DateTime.UtcNow.Date;
        var original = CanonicalRequestHasher.PaymentCreate(provider.TenantId, orderId, customerId, 10, "USD", "Cash", when, null);
        var changed = CanonicalRequestHasher.PaymentCreate(provider.TenantId, orderId, customerId, 10, "PKR", "Cash", when, null);

        var first = await service.BeginLocalAsync(IdempotencyOperations.PaymentCreate, "key-3", original);
        service.CompleteLocal(first.Record!, 201, """{"id":"abc"}""", Guid.NewGuid());
        await db.SaveChangesAsync();

        var second = await service.BeginLocalAsync(IdempotencyOperations.PaymentCreate, "key-3", changed);

        second.Kind.Should().Be(IdempotencyOutcomeKind.Conflict);
    }

    [Fact]
    public async Task SameKeyInAnotherTenant_StartsASeparateOperation()
    {
        var shared = Guid.NewGuid().ToString("N");
        var (left, _, leftProvider) = TenantDatabase.Open(name: shared);
        var (right, _, rightProvider) = TenantDatabase.Open(name: shared);
        await using var leftDb = left;
        await using var rightDb = right;
        var hash = CanonicalRequestHasher.PaymentCreate(leftProvider.TenantId, Guid.NewGuid(), Guid.NewGuid(), 10, "USD", "Cash", DateTime.UtcNow.Date, null);

        var started = await Service(leftDb, leftProvider).BeginLocalAsync(IdempotencyOperations.PaymentCreate, "shared-key", hash);
        Service(leftDb, leftProvider).CompleteLocal(started.Record!, 201, """{"id":"left"}""", Guid.NewGuid());
        await leftDb.SaveChangesAsync();

        var other = await Service(rightDb, rightProvider).BeginLocalAsync(IdempotencyOperations.PaymentCreate, "shared-key", hash);

        other.Kind.Should().Be(IdempotencyOutcomeKind.Started);
    }

    [Fact]
    public void ProviderOperationKey_StaysStableForTheSameOperation()
    {
        var operationId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        ProviderOperationKey.Create(operationId, "jazzcash").Should().Be(ProviderOperationKey.Create(operationId, "JazzCash"));
        ProviderOperationKey.Create(operationId, "stripe").Should().StartWith("bos-");
        ProviderOperationKey.Create(operationId, "jazzcash").Length.Should().BeLessThanOrEqualTo(20);
    }

    private static IdempotencyService Service(
        BusinessOSDbContext context,
        ITenantProvider provider) =>
        new(context, provider, NullLogger<IdempotencyService>.Instance);
}
