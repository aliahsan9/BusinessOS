using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Inventory.Queries.GetInventoryAnalytics;
using BusinessOS.Application.Features.Inventory.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.MultiTenancy;
using BusinessOS.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace BusinessOS.UnitTests.Handlers;

public class InventoryServiceTests
{
    [Fact]
    public async Task IncreaseStock_UpdatesCurrentStockAndCreatesTransaction()
    {
        var (service, context, productId) = await CreateServiceWithProduct(stock: 10);

        await service.IncreaseStockAsync(
            new Application.Features.Inventory.Queries.StockChangeRequest
            {
                ProductId = productId,
                Quantity = 5,
                Notes = "Restock"
            });

        var inventory = await context.Inventories.FirstAsync(x => x.ProductId == productId);
        inventory.CurrentStock.Should().Be(15);

        var transaction = await context.StockTransactions.FirstAsync(x => x.ProductId == productId);
        transaction.TransactionType.Should().Be(StockTransactionTypeNames.Purchase);
        transaction.PreviousStock.Should().Be(10);
        transaction.NewStock.Should().Be(15);
        transaction.Quantity.Should().Be(5);
    }

    [Fact]
    public async Task DecreaseStock_ReducesStock()
    {
        var (service, context, productId) = await CreateServiceWithProduct(stock: 10);

        await service.DecreaseStockAsync(
            new Application.Features.Inventory.Queries.StockChangeRequest
            {
                ProductId = productId,
                Quantity = 3
            });

        var inventory = await context.Inventories.FirstAsync(x => x.ProductId == productId);
        inventory.CurrentStock.Should().Be(7);
    }

    [Fact]
    public async Task DecreaseStock_WhenInsufficient_ThrowsBadRequestException()
    {
        var (service, _, productId) = await CreateServiceWithProduct(stock: 2);

        var act = () => service.DecreaseStockAsync(
            new Application.Features.Inventory.Queries.StockChangeRequest
            {
                ProductId = productId,
                Quantity = 5
            });

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Insufficient stock*");
    }

    [Fact]
    public async Task GetLowStockProducts_ReturnsProductsBelowReorderLevel()
    {
        var (service, _, _) = await CreateServiceWithProduct(stock: 3, reorderLevel: 5);

        var lowStock = await service.GetLowStockProductsAsync();

        lowStock.Should().HaveCount(1);
        lowStock[0].IsLowStock.Should().BeTrue();
    }

    [Fact]
    public async Task GetOutOfStockProducts_ReturnsZeroStockProducts()
    {
        var (service, _, _) = await CreateServiceWithProduct(stock: 0, reorderLevel: 5);

        var outOfStock = await service.GetOutOfStockProductsAsync();

        outOfStock.Should().HaveCount(1);
        outOfStock[0].IsOutOfStock.Should().BeTrue();
    }

    [Fact]
    public async Task DeductForOrder_ReducesStockPerItem()
    {
        var (service, context, productId) = await CreateServiceWithProduct(stock: 20);
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-2026-000001",
            OrderItems =
            [
                new OrderItem { ProductId = productId, Quantity = 4 }
            ]
        };

        await service.DeductForOrderAsync(order, order.OrderItems);

        var inventory = await context.Inventories.FirstAsync(x => x.ProductId == productId);
        inventory.CurrentStock.Should().Be(16);
    }

    [Fact]
    public async Task RestoreForCancelledOrder_RestoresStock()
    {
        var (service, context, productId) = await CreateServiceWithProduct(stock: 10);
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-2026-000001",
            OrderItems =
            [
                new OrderItem { ProductId = productId, Quantity = 4 }
            ]
        };

        await service.DeductForOrderAsync(order, order.OrderItems);
        await service.RestoreForCancelledOrderAsync(order, order.OrderItems);

        var inventory = await context.Inventories.FirstAsync(x => x.ProductId == productId);
        inventory.CurrentStock.Should().Be(10);
    }

    [Fact]
    public async Task GetInventoryAnalytics_CalculatesTotals()
    {
        var (service, context, _) = await CreateServiceWithProduct(stock: 10, costPrice: 5, reorderLevel: 20);
        var handler = new GetInventoryAnalyticsQueryHandler(
            new InventoryRepository(context),
            new StockTransactionRepository(context));

        var analytics = await handler.Handle(new GetInventoryAnalyticsQuery(), CancellationToken.None);

        analytics.TotalProducts.Should().Be(1);
        analytics.TotalStockQuantity.Should().Be(10);
        analytics.LowStockCount.Should().Be(1);
        analytics.InventoryValue.Should().Be(50);
    }

    private static async Task<(InventoryService Service, BusinessOSDbContext Context, Guid ProductId)> CreateServiceWithProduct(
        decimal stock,
        decimal reorderLevel = 5,
        decimal costPrice = 10)
    {
        var tenantId = Guid.NewGuid();
        var tenantProvider = new TenantProvider();
        tenantProvider.SetTenantId(tenantId);

        var options = new DbContextOptionsBuilder<BusinessOSDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new BusinessOSDbContext(options, tenantProvider);
        var productId = Guid.NewGuid();

        var product = new Product
        {
            Id = productId,
            TenantId = tenantId,
            CategoryId = Guid.NewGuid(),
            Name = "Widget",
            SKU = "W-1",
            CostPrice = costPrice,
            SalePrice = 20,
            CurrentStock = stock,
            ReorderLevel = reorderLevel,
            IsActive = true
        };

        context.Products.Add(product);
        context.Inventories.Add(new Domain.Entities.Inventory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = productId,
            CurrentStock = stock,
            ReorderLevel = reorderLevel,
            MinimumStockLevel = 0,
            MaximumStockLevel = 100,
            LastUpdated = DateTime.UtcNow,
            Product = product
        });

        await context.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(x => x.UserId).Returns("user-1");

        var service = new InventoryService(
            new InventoryRepository(context),
            new StockTransactionRepository(context),
            context,
            currentUser.Object);

        return (service, context, productId);
    }
}

public class InventoryValidatorTests
{
    [Fact]
    public void IncreaseStock_WithZeroQuantity_IsInvalid()
    {
        var validator = new Application.Features.Inventory.Commands.IncreaseStock.IncreaseStockCommandValidator();
        var result = validator.Validate(new Application.Features.Inventory.Commands.IncreaseStock.IncreaseStockCommand(
            Guid.NewGuid(), 0, null, null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void AdjustStock_WithInvalidTransactionType_IsInvalid()
    {
        var validator = new Application.Features.Inventory.Commands.AdjustStock.AdjustStockCommandValidator();
        var result = validator.Validate(new Application.Features.Inventory.Commands.AdjustStock.AdjustStockCommand(
            Guid.NewGuid(), 5, "InvalidType", null, null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateInventory_WithMaxBelowMin_IsInvalid()
    {
        var validator = new Application.Features.Inventory.Commands.UpdateInventory.UpdateInventoryCommandValidator();
        var result = validator.Validate(new Application.Features.Inventory.Commands.UpdateInventory.UpdateInventoryCommand(
            Guid.NewGuid(), 10, 5, 8));

        result.IsValid.Should().BeFalse();
    }
}
