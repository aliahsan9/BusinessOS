using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Dashboard.Services;
using BusinessOS.Application.Features.Inventory.Commands.AdjustStock;
using BusinessOS.Application.Features.Inventory.Commands.IncreaseStock;
using BusinessOS.Application.Features.Inventory.Queries;
using BusinessOS.Application.Features.Inventory.Services;
using BusinessOS.Application.Features.Orders.Commands.CreateOrder;
using BusinessOS.Application.Features.Orders.Queries;
using BusinessOS.Application.Features.Orders.Services;
using BusinessOS.Application.Features.Products.Commands.CreateProduct;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.MultiTenancy;
using BusinessOS.Infrastructure.Services;
using BusinessOS.UnitTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace BusinessOS.UnitTests.Handlers;

public class OrderAndInventoryEdgeTests
{
    [Fact]
    public async Task CreateOrder_MultipleItems_UsesCatalogPricesAndDeductsStock()
    {
        var (context, provider, customer, first, second) = await SeedShopAsync(stock: 20, salePrice: 10.125m);
        var secondPrice = 3.5m;
        second.SalePrice = secondPrice;
        await context.SaveChangesAsync();

        var id = await Orders(context, provider).Handle(
            new CreateOrderCommand(customer.Id, 1.10m, 0.25m,
            [
                new CreateOrderItemDto(first.Id, 2),
                new CreateOrderItemDto(second.Id, 4)
            ]),
            CancellationToken.None);

        var order = await context.Orders.Include(x => x.OrderItems).SingleAsync(x => x.Id == id);
        order.OrderItems.Should().HaveCount(2);
        order.TotalAmount.Should().Be(34.25m);
        order.GrandTotal.Should().Be(33.40m);
        order.OrderItems.Single(x => x.ProductId == first.Id).UnitPrice.Should().Be(10.125m);

        var firstStock = await context.Inventories.SingleAsync(x => x.ProductId == first.Id);
        var secondStock = await context.Inventories.SingleAsync(x => x.ProductId == second.Id);
        firstStock.CurrentStock.Should().Be(18);
        secondStock.CurrentStock.Should().Be(16);
    }

    [Fact]
    public async Task CreateOrder_SumsDuplicateLinesBeforeCheckingStock()
    {
        var (context, provider, customer, product, _) = await SeedShopAsync(stock: 5, salePrice: 8);

        var act = () => Orders(context, provider).Handle(
            new CreateOrderCommand(customer.Id, 0, 0,
            [
                new CreateOrderItemDto(product.Id, 3),
                new CreateOrderItemDto(product.Id, 3)
            ]),
            CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*Insufficient stock*");
        (await context.Orders.CountAsync()).Should().Be(0);
        (await context.Inventories.SingleAsync(x => x.ProductId == product.Id)).CurrentStock.Should().Be(5);
    }

    [Fact]
    public async Task CreateOrder_NegativeGrandTotal_DoesNotPersist()
    {
        var (context, provider, customer, product, _) = await SeedShopAsync(stock: 10, salePrice: 5);

        var act = () => Orders(context, provider).Handle(
            new CreateOrderCommand(customer.Id, 100, 0, [new CreateOrderItemDto(product.Id, 1)]),
            CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*cannot be negative*");
        (await context.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateOrder_RejectsAnotherTenantsProductAndCustomer()
    {
        var database = Guid.NewGuid().ToString();
        var (home, _, customer, product, _) = await SeedShopAsync(database, stock: 9, salePrice: 12);
        var (other, otherProvider, otherCustomer, otherProduct, _) = await SeedShopAsync(database, stock: 9, salePrice: 12);

        var foreignProduct = () => Orders(home, TenantOf(home)).Handle(
            new CreateOrderCommand(customer.Id, 0, 0, [new CreateOrderItemDto(otherProduct.Id, 1)]),
            CancellationToken.None);
        await foreignProduct.Should().ThrowAsync<BadRequestException>().WithMessage("*do not exist*");

        var foreignCustomer = () => Orders(other, otherProvider).Handle(
            new CreateOrderCommand(customer.Id, 0, 0, [new CreateOrderItemDto(otherProduct.Id, 1)]),
            CancellationToken.None);
        await foreignCustomer.Should().ThrowAsync<NotFoundException>().WithMessage("*Customer not found*");

        (await home.Orders.CountAsync()).Should().Be(0);
        (await other.Orders.CountAsync()).Should().Be(0);
        (await home.Inventories.SingleAsync(x => x.ProductId == product.Id)).CurrentStock.Should().Be(9);
        (await other.Inventories.SingleAsync(x => x.ProductId == otherProduct.Id)).CurrentStock.Should().Be(9);
        otherCustomer.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateOrder_InactiveProduct_IsRejected()
    {
        var (context, provider, customer, product, _) = await SeedShopAsync(stock: 10, salePrice: 5);
        product.IsActive = false;
        await context.SaveChangesAsync();

        var act = () => Orders(context, provider).Handle(
            new CreateOrderCommand(customer.Id, 0, 0, [new CreateOrderItemDto(product.Id, 1)]),
            CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*not active*");
    }

    [Fact]
    public async Task AdjustStock_DamageReducesStock_AndInvalidTypeDoesNot()
    {
        var (context, _, _, product, _) = await SeedShopAsync(stock: 8, salePrice: 5, reorderLevel: 5);
        var service = TenantDatabase.Inventory(context);

        await service.AdjustStockAsync(new StockAdjustmentRequest
        {
            ProductId = product.Id,
            Quantity = 3,
            TransactionType = StockTransactionTypeNames.Damage
        });

        (await context.Inventories.SingleAsync(x => x.ProductId == product.Id)).CurrentStock.Should().Be(5);
        (await context.StockTransactions.SingleAsync(x => x.ProductId == product.Id)).TransactionType.Should().Be(StockTransactionTypeNames.Damage);

        var act = () => service.AdjustStockAsync(new StockAdjustmentRequest
        {
            ProductId = product.Id,
            Quantity = 1,
            TransactionType = "Steal"
        });
        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*Invalid transaction type*");
        (await context.Inventories.SingleAsync(x => x.ProductId == product.Id)).CurrentStock.Should().Be(5);
    }

    [Fact]
    public async Task DecreaseStock_AtReorderBoundary_RemainsAvailableAndLow()
    {
        var (context, _, _, product, _) = await SeedShopAsync(stock: 6, salePrice: 5, reorderLevel: 4);
        var service = TenantDatabase.Inventory(context);

        await service.DecreaseStockAsync(new StockChangeRequest { ProductId = product.Id, Quantity = 2 });

        var low = await service.GetLowStockProductsAsync();
        low.Should().ContainSingle();
        low[0].CurrentStock.Should().Be(4);
        low[0].IsLowStock.Should().BeTrue();
        low[0].IsOutOfStock.Should().BeFalse();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1_000_001)]
    public void StockValidators_RejectNonPositiveAndOverflowQuantities(decimal quantity)
    {
        new IncreaseStockCommandValidator()
            .Validate(new IncreaseStockCommand(Guid.NewGuid(), quantity, null, null))
            .IsValid.Should().BeFalse();

        new AdjustStockCommandValidator()
            .Validate(new AdjustStockCommand(Guid.NewGuid(), quantity, StockTransactionTypeNames.Sale, null, null))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void StockValidators_AcceptUpperBoundary()
    {
        new IncreaseStockCommandValidator()
            .Validate(new IncreaseStockCommand(Guid.NewGuid(), 1_000_000m, null, null))
            .IsValid.Should().BeTrue();
    }

    private static CreateOrderCommandHandler Orders(BusinessOSDbContext context, ITenantProvider provider)
    {
        var numbers = new Mock<IOrderNumberGenerator>();
        numbers.Setup(x => x.GenerateNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync("ORD-100001");
        return new CreateOrderCommandHandler(
            context,
            numbers.Object,
            TenantDatabase.Inventory(context),
            TestHandlerDependencies.CreateBusinessEvents(),
            TestHandlerDependencies.CreateCache(),
            provider,
            TestHandlerDependencies.CreateLogger<CreateOrderCommandHandler>());
    }

    private static ITenantProvider TenantOf(BusinessOSDbContext context)
    {
        var tenantId = context.Products.Select(x => x.TenantId).First();
        var provider = new TenantProvider();
        provider.SetTenantId(tenantId);
        return provider;
    }

    private static async Task<(BusinessOSDbContext Context, TenantProvider Provider, Customer Customer, Product First, Product Second)> SeedShopAsync(
        string? database = null,
        decimal stock = 10,
        decimal salePrice = 20,
        decimal reorderLevel = 5)
    {
        var (context, _, provider) = TenantDatabase.Open(name: database);
        var category = TestDataFactory.CreateCategory(provider.TenantId);
        var customer = TestDataFactory.CreateCustomer(provider.TenantId, $"{Guid.NewGuid():N}@test.com");
        var first = TestDataFactory.CreateProduct(provider.TenantId, category.Id, "First", $"SKU-{Guid.NewGuid():N}", salePrice: salePrice, stock: stock, reorderLevel: reorderLevel);
        var second = TestDataFactory.CreateProduct(provider.TenantId, category.Id, "Second", $"SKU-{Guid.NewGuid():N}", salePrice: salePrice, stock: stock, reorderLevel: reorderLevel);
        context.AddRange(
            category,
            customer,
            first,
            second,
            TestDataFactory.CreateInventory(provider.TenantId, first.Id, stock, reorderLevel),
            TestDataFactory.CreateInventory(provider.TenantId, second.Id, stock, reorderLevel));
        await context.SaveChangesAsync();
        return (context, provider, customer, first, second);
    }
}

public class ProductCreationTests
{
    [Fact]
    public async Task CreateProduct_RejectsUnknownCategory_AndOpensAZeroStockRecord()
    {
        var (context, _, provider) = TenantDatabase.Open();
        var missing = () => Products(context, provider).Handle(
            new CreateProductCommand(Guid.NewGuid(), "Widget", "SKU-1", null, 4, 9, 2),
            CancellationToken.None);
        await missing.Should().ThrowAsync<BadRequestException>().WithMessage("*Category does not exist*");

        var category = TestDataFactory.CreateCategory(provider.TenantId, "Widgets");
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var id = await Products(context, provider).Handle(
            new CreateProductCommand(category.Id, "Widget", "SKU-1", "  ", 4.5m, 9.5m, 2),
            CancellationToken.None);

        var inventory = await context.Inventories.SingleAsync(x => x.ProductId == id);
        inventory.CurrentStock.Should().Be(0);
        inventory.ReorderLevel.Should().Be(2);
    }

    [Fact]
    public async Task SameSku_IsIsolatedBetweenTenants()
    {
        var database = Guid.NewGuid().ToString();
        var (left, _, leftProvider) = TenantDatabase.Open(name: database);
        var (right, _, rightProvider) = TenantDatabase.Open(name: database);
        var leftCategory = TestDataFactory.CreateCategory(leftProvider.TenantId);
        var rightCategory = TestDataFactory.CreateCategory(rightProvider.TenantId);
        left.Categories.Add(leftCategory);
        right.Categories.Add(rightCategory);
        await left.SaveChangesAsync();
        await right.SaveChangesAsync();

        await Products(left, leftProvider).Handle(
            new CreateProductCommand(leftCategory.Id, "Shared", "SKU-SHARED", null, 1, 2, 0),
            CancellationToken.None);
        await Products(right, rightProvider).Handle(
            new CreateProductCommand(rightCategory.Id, "Shared", "SKU-SHARED", null, 1, 2, 0),
            CancellationToken.None);

        (await left.Products.CountAsync(x => x.SKU == "SKU-SHARED")).Should().Be(1);
        (await right.Products.CountAsync(x => x.SKU == "SKU-SHARED")).Should().Be(1);
        (await left.Products.AnyAsync(x => x.TenantId == rightProvider.TenantId)).Should().BeFalse();
    }

    private static CreateProductCommandHandler Products(BusinessOSDbContext context, ITenantProvider provider) =>
        new(
            context,
            TenantDatabase.Inventory(context),
            TestHandlerDependencies.CreateCache(),
            provider,
            TestHandlerDependencies.CreateLogger<CreateProductCommandHandler>());
}

public class FinanceCalculationTests
{
    [Fact]
    public async Task Dashboard_RoundsMoney_AndIgnoresPendingAndOtherTenants()
    {
        var database = Guid.NewGuid().ToString();
        var (context, tenantId, _) = TenantDatabase.Open(name: database);
        var (other, _, _) = TenantDatabase.Open(name: database);
        var when = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);
        var category = new ExpenseCategory { Id = Guid.NewGuid(), Name = "Rent", IsActive = true };
        context.ExpenseCategories.Add(category);
        await context.SaveChangesAsync();

        context.Orders.AddRange(
            CompletedOrder(120.126m, when),
            CompletedOrder(10.004m, when),
            PendingOrder(999m, when));
        context.Expenses.Add(new Expense
        {
            Id = Guid.NewGuid(),
            Title = "Office",
            Amount = 40.124m,
            ExpenseDate = when,
            ExpenseCategoryId = category.Id,
            PaymentMethod = PaymentMethodNames.Cash,
            Status = ExpenseStatusNames.Paid
        });
        context.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            Amount = 15.556m,
            PaymentMethod = PaymentMethodNames.Cash,
            PaymentDate = when
        });
        context.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = "INV-1",
            OrderId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            InvoiceDate = when,
            DueDate = when.AddDays(7),
            Status = InvoiceStatusNames.Sent,
            GrandTotal = 80,
            OutstandingAmount = 25.339m
        });
        await context.SaveChangesAsync();

        other.Orders.Add(CompletedOrder(5_000m, when));
        await other.SaveChangesAsync();

        var service = new FinanceService(context, new DashboardDateRangeResolver());
        var dashboard = await service.GetDashboardAsync(when.Date, when.Date.AddDays(1), "custom");

        dashboard.TotalRevenue.Should().Be(130.13m);
        dashboard.TotalExpenses.Should().Be(40.12m);
        dashboard.NetProfit.Should().Be(90.01m);
        dashboard.TotalPayments.Should().Be(15.56m);
        dashboard.OutstandingInvoices.Should().Be(25.34m);
        dashboard.CompletedOrders.Should().Be(2);
        dashboard.TotalRevenue.Should().NotBe(5_000m);
        tenantId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task ProfitLoss_RejectsUnknownGrouping_AndCustomRangeRequiresBothDates()
    {
        var (context, _, _) = TenantDatabase.Open();
        var service = new FinanceService(context, new DashboardDateRangeResolver());

        var group = () => service.GetProfitLossAsync(null, null, "month", "week");
        await group.Should().ThrowAsync<BadRequestException>().WithMessage("*groupBy*");

        var range = () => service.GetDashboardAsync(DateTime.UtcNow, null, "custom");
        await range.Should().ThrowAsync<BadRequestException>().WithMessage("*startDate and endDate*");

        var inverted = () => service.GetDashboardAsync(
            new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            "custom");
        await inverted.Should().ThrowAsync<BadRequestException>().WithMessage("*startDate must be less*");
    }

    [Fact]
    public async Task Dashboard_HonorsCancellation()
    {
        var (context, _, _) = TenantDatabase.Open();
        var service = new FinanceService(context, new DashboardDateRangeResolver());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var act = () => service.GetDashboardAsync(null, null, "all", cancellation.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static Order CompletedOrder(decimal total, DateTime when) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = Guid.NewGuid(),
        OrderNumber = $"ORD-{Guid.NewGuid():N}"[..16],
        OrderDate = when,
        Status = OrderStatusNames.Completed,
        GrandTotal = total
    };

    private static Order PendingOrder(decimal total, DateTime when)
    {
        var order = CompletedOrder(total, when);
        order.Status = OrderStatusNames.Pending;
        return order;
    }
}
