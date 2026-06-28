using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Inventory.Commands.AdjustStock;
using BusinessOS.Application.Features.Inventory.Queries;
using BusinessOS.Application.Features.Inventory.Queries.GetOutOfStockProducts;
using BusinessOS.Application.Features.Inventory.Queries.GetReorderProducts;
using BusinessOS.Application.Features.Inventory.Queries.GetStockTransactions;
using BusinessOS.Application.Features.Inventory.Services;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Repositories;
using BusinessOS.UnitTests.Common;
using FluentAssertions;
using Moq;

namespace BusinessOS.UnitTests.Handlers;

public class InventoryQueryHandlerTests
{
    [Fact]
    public async Task GetOutOfStockProductsQueryHandler_ReturnsZeroStockItems()
    {
        var inventoryService = new Mock<IInventoryService>();
        inventoryService
            .Setup(x => x.GetOutOfStockProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new InventorySummaryResponse { ProductId = Guid.NewGuid(), CurrentStock = 0, IsOutOfStock = true }
            ]);

        var handler = new GetOutOfStockProductsQueryHandler(inventoryService.Object);
        var result = await handler.Handle(new GetOutOfStockProductsQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].IsOutOfStock.Should().BeTrue();
    }

    [Fact]
    public async Task GetReorderProductsQueryHandler_ReturnsReorderCandidates()
    {
        var inventoryService = new Mock<IInventoryService>();
        inventoryService
            .Setup(x => x.GetReorderProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new InventorySummaryResponse { ProductId = Guid.NewGuid(), CurrentStock = 2, ReorderLevel = 5, IsLowStock = true }
            ]);

        var handler = new GetReorderProductsQueryHandler(inventoryService.Object);
        var result = await handler.Handle(new GetReorderProductsQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].IsLowStock.Should().BeTrue();
    }

    [Fact]
    public async Task AdjustStockCommandHandler_DelegatesToInventoryService()
    {
        var productId = Guid.NewGuid();
        var inventoryService = new Mock<IInventoryService>();
        inventoryService
            .Setup(x => x.AdjustStockAsync(It.IsAny<StockAdjustmentRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new AdjustStockCommandHandler(inventoryService.Object);
        await handler.Handle(
            new AdjustStockCommand(productId, 5, StockTransactionTypeNames.Adjustment, null, "Cycle count"),
            CancellationToken.None);

        inventoryService.Verify(
            x => x.AdjustStockAsync(
                It.Is<StockAdjustmentRequest>(r =>
                    r.ProductId == productId &&
                    r.TransactionType == StockTransactionTypeNames.Adjustment),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetStockTransactionsQueryHandler_ReturnsPagedTransactions()
    {
        var (context, tenantId) = await TestDataFactory.CreateCatalogContextAsync();
        var product = context.Products.First();
        context.StockTransactions.Add(
            TestDataFactory.CreateStockTransaction(
                tenantId,
                product.Id,
                StockTransactionTypeNames.Purchase,
                10,
                0,
                10,
                product));
        await context.SaveChangesAsync();

        var handler = new GetStockTransactionsQueryHandler(new StockTransactionRepository(context));
        var result = await handler.Handle(
            new GetStockTransactionsQuery(product.Id, null, null, 1, 10, "createdAt", SortDirection.Desc),
            CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetStockTransactionsQueryValidator_RejectsInvalidSortField()
    {
        var validator = new GetStockTransactionsQueryValidator();
        var result = validator.Validate(
            new GetStockTransactionsQuery(null, null, null, 1, 10, "invalid", SortDirection.Asc));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task InventoryService_AdjustStock_WithDamage_DecreasesStock()
    {
        var (context, tenantId) = await TestDataFactory.CreateCatalogContextAsync(productStock: 20);
        var productId = context.Products.First().Id;
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(x => x.UserId).Returns("user-1");

        var service = new InventoryService(
            new InventoryRepository(context),
            new StockTransactionRepository(context),
            context,
            currentUser.Object);

        await service.AdjustStockAsync(new StockAdjustmentRequest
        {
            ProductId = productId,
            Quantity = 3,
            TransactionType = StockTransactionTypeNames.Damage,
            Notes = "Damaged goods"
        });

        var inventory = context.Inventories.First(x => x.ProductId == productId);
        inventory.CurrentStock.Should().Be(17);
    }

    [Fact]
    public async Task InventoryService_GetReorderProducts_ReturnsAtOrBelowReorderLevel()
    {
        var (context, tenantId, _) = InMemoryDbContextFactory.Create();
        var category = TestDataFactory.CreateCategory(tenantId);
        var product = TestDataFactory.CreateProduct(tenantId, category.Id, stock: 3, reorderLevel: 10);
        context.Categories.Add(category);
        context.Products.Add(product);
        context.Inventories.Add(TestDataFactory.CreateInventoryWithProduct(tenantId, product, 3, 10));
        await context.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        var service = new InventoryService(
            new InventoryRepository(context),
            new StockTransactionRepository(context),
            context,
            currentUser.Object);

        var reorder = await service.GetReorderProductsAsync();

        reorder.Should().HaveCount(1);
    }
}
