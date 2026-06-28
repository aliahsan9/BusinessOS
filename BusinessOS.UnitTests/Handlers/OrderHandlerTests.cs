using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Orders.Commands.CreateOrder;
using BusinessOS.Application.Features.Orders.Commands.DeleteOrder;
using BusinessOS.Application.Features.Orders.Commands.UpdateOrder;
using BusinessOS.Application.Features.Orders.Commands.UpdateOrderStatus;
using BusinessOS.Application.Features.Orders.Queries;
using BusinessOS.Application.Features.Orders.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.UnitTests;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace BusinessOS.UnitTests.Handlers;

public class CreateOrderHandlerTests
{
    [Fact]
    public async Task Handle_WithInvalidProduct_ThrowsBadRequestException()
    {
        var context = CreateContext(
            customers: [],
            products: [],
            orders: []);

        var handler = CreateHandler(context);

        var act = () => handler.Handle(
            new CreateOrderCommand(
                "Ali",
                "ali@test.com",
                "123",
                "Address",
                0,
                0,
                [new CreateOrderItemDto(Guid.NewGuid(), 1)]),
            CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*products do not exist*");
    }

    [Fact]
    public async Task Handle_WithInvalidQuantity_ThrowsValidationException()
    {
        var validator = new CreateOrderCommandValidator();
        var result = validator.Validate(new CreateOrderCommand(
            "Ali",
            "ali@test.com",
            "",
            "",
            0,
            0,
            [new CreateOrderItemDto(Guid.NewGuid(), 0)]));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithValidData_ReturnsIdAndCalculatesTotals()
    {
        var productId = Guid.NewGuid();
        var products = new List<Product>
        {
            new()
            {
                Id = productId,
                Name = "Widget",
                SKU = "W-1",
                SalePrice = 10,
                IsActive = true,
                TenantId = Guid.NewGuid()
            }
        };

        var context = CreateContext([], products, []);
        context.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var orderNumberGenerator = new Mock<IOrderNumberGenerator>();
        orderNumberGenerator
            .Setup(x => x.GenerateNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("ORD-2026-000001");

        var handler = new CreateOrderCommandHandler(
            context.Object,
            orderNumberGenerator.Object,
            Mock.Of<ILogger<CreateOrderCommandHandler>>());

        var id = await handler.Handle(
            new CreateOrderCommand(
                "Ali",
                "ali@test.com",
                "123",
                "Address",
                1,
                2,
                [new CreateOrderItemDto(productId, 3)]),
            CancellationToken.None);

        id.Should().NotBe(Guid.Empty);
        context.Verify(x => x.Orders.Add(It.Is<Order>(o =>
            o.TotalAmount == 30 &&
            o.Discount == 1 &&
            o.Tax == 2 &&
            o.GrandTotal == 31 &&
            o.OrderNumber == "ORD-2026-000001" &&
            o.Status == OrderStatusNames.Pending)), Times.Once);
    }

    private static CreateOrderCommandHandler CreateHandler(Mock<IApplicationDbContext> context)
    {
        var orderNumberGenerator = new Mock<IOrderNumberGenerator>();
        orderNumberGenerator
            .Setup(x => x.GenerateNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("ORD-2026-000001");

        return new CreateOrderCommandHandler(
            context.Object,
            orderNumberGenerator.Object,
            Mock.Of<ILogger<CreateOrderCommandHandler>>());
    }

    private static Mock<IApplicationDbContext> CreateContext(
        List<Customer> customers,
        List<Product> products,
        List<Order> orders)
    {
        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.Customers).Returns(TestMockDbSet.CreateMockDbSet(customers.AsQueryable()).Object);
        context.Setup(x => x.Products).Returns(TestMockDbSet.CreateMockDbSet(products.AsQueryable()).Object);
        context.Setup(x => x.Orders).Returns(TestMockDbSet.CreateMockDbSet(orders.AsQueryable()).Object);
        context.Setup(x => x.OrderItems).Returns(TestMockDbSet.CreateMockDbSet(new List<OrderItem>().AsQueryable()).Object);
        return context;
    }
}

public class UpdateOrderHandlerTests
{
    [Fact]
    public async Task Handle_WithMissingOrder_ThrowsNotFoundException()
    {
        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.Orders).Returns(TestMockDbSet.CreateMockDbSet(new List<Order>().AsQueryable()).Object);

        var handler = new UpdateOrderCommandHandler(
            context.Object,
            Mock.Of<ILogger<UpdateOrderCommandHandler>>());

        var act = () => handler.Handle(
            new UpdateOrderCommand(
                Guid.NewGuid(),
                "Ali",
                "ali@test.com",
                "",
                "",
                0,
                0,
                [new CreateOrderItemDto(Guid.NewGuid(), 1)]),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WithValidOrder_UpdatesTotals()
    {
        var tenantId = Guid.NewGuid();
        var tenantProvider = new BusinessOS.Infrastructure.MultiTenancy.TenantProvider();
        tenantProvider.SetTenantId(tenantId);

        var options = new DbContextOptionsBuilder<BusinessOS.Infrastructure.Data.BusinessOSDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new BusinessOS.Infrastructure.Data.BusinessOSDbContext(options, tenantProvider);

        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        db.Customers.Add(new Customer
        {
            Id = customerId,
            TenantId = tenantId,
            Name = "Ali",
            Email = "ali@test.com",
            Phone = "123",
            Address = "Street"
        });

        db.Products.Add(new Product
        {
            Id = productId,
            TenantId = tenantId,
            CategoryId = Guid.NewGuid(),
            Name = "Widget",
            SKU = "W-1",
            SalePrice = 10,
            CostPrice = 5,
            IsActive = true
        });

        db.Orders.Add(new Order
        {
            Id = orderId,
            TenantId = tenantId,
            CustomerId = customerId,
            OrderNumber = "ORD-2026-000001",
            Status = OrderStatusNames.Pending,
            TotalAmount = 10,
            GrandTotal = 10,
            OrderItems =
            [
                new OrderItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProductId = productId,
                    Quantity = 1,
                    UnitPrice = 10,
                    Total = 10
                }
            ]
        });

        await db.SaveChangesAsync();

        var handler = new UpdateOrderCommandHandler(
            db,
            Mock.Of<ILogger<UpdateOrderCommandHandler>>());

        await handler.Handle(
            new UpdateOrderCommand(
                orderId,
                "Ali Updated",
                "ali@test.com",
                "123",
                "Street",
                0,
                0,
                [new CreateOrderItemDto(productId, 3)]),
            CancellationToken.None);

        var updated = await db.Orders.FindAsync(orderId);
        updated!.TotalAmount.Should().Be(30);
        updated.GrandTotal.Should().Be(30);
    }
}

public class DeleteOrderHandlerTests
{
    [Fact]
    public async Task Handle_WithCompletedOrder_ThrowsConflictException()
    {
        var orderId = Guid.NewGuid();
        var orders = new List<Order>
        {
            new()
            {
                Id = orderId,
                Status = OrderStatusNames.Completed,
                TenantId = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                OrderNumber = "ORD-1"
            }
        };

        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.Orders).Returns(TestMockDbSet.CreateMockDbSet(orders.AsQueryable()).Object);

        var handler = new DeleteOrderCommandHandler(
            context.Object,
            Mock.Of<ILogger<DeleteOrderCommandHandler>>());

        var act = () => handler.Handle(new DeleteOrderCommand(orderId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }
}

public class UpdateOrderStatusHandlerTests
{
    [Fact]
    public async Task Handle_WithInvalidTransition_ThrowsBadRequestException()
    {
        var orderId = Guid.NewGuid();
        var orders = new List<Order>
        {
            new()
            {
                Id = orderId,
                Status = OrderStatusNames.Completed,
                TenantId = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                OrderNumber = "ORD-1"
            }
        };

        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.Orders).Returns(TestMockDbSet.CreateMockDbSet(orders.AsQueryable()).Object);

        var handler = new UpdateOrderStatusCommandHandler(
            context.Object,
            Mock.Of<ILogger<UpdateOrderStatusCommandHandler>>());

        var act = () => handler.Handle(
            new UpdateOrderStatusCommand(orderId, OrderStatusNames.Pending),
            CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task Handle_WithValidTransition_UpdatesStatus()
    {
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            Id = orderId,
            Status = OrderStatusNames.Pending,
            TenantId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            OrderNumber = "ORD-1"
        };

        var orders = new List<Order> { order };
        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.Orders).Returns(TestMockDbSet.CreateMockDbSet(orders.AsQueryable()).Object);
        context.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new UpdateOrderStatusCommandHandler(
            context.Object,
            Mock.Of<ILogger<UpdateOrderStatusCommandHandler>>());

        await handler.Handle(
            new UpdateOrderStatusCommand(orderId, OrderStatusNames.Confirmed),
            CancellationToken.None);

        order.Status.Should().Be(OrderStatusNames.Confirmed);
    }
}

public class OrderNumberGeneratorTests
{
    [Fact]
    public async Task GenerateNextAsync_WithNoExistingOrders_ReturnsFirstSequence()
    {
        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.Orders).Returns(TestMockDbSet.CreateMockDbSet(new List<Order>().AsQueryable()).Object);

        var generator = new OrderNumberGenerator(context.Object);
        var number = await generator.GenerateNextAsync();

        number.Should().StartWith($"ORD-{DateTime.UtcNow.Year}-");
        number.Should().EndWith("000001");
    }

    [Fact]
    public async Task GenerateNextAsync_WithExistingOrders_IncrementsSequence()
    {
        var year = DateTime.UtcNow.Year;
        var orders = new List<Order>
        {
            new()
            {
                Id = Guid.NewGuid(),
                OrderNumber = $"ORD-{year}-000005",
                TenantId = Guid.NewGuid(),
                CustomerId = Guid.NewGuid()
            }
        };

        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.Orders).Returns(TestMockDbSet.CreateMockDbSet(orders.AsQueryable()).Object);

        var generator = new OrderNumberGenerator(context.Object);
        var number = await generator.GenerateNextAsync();

        number.Should().Be($"ORD-{year}-000006");
    }
}

public class OrderStatusRulesTests
{
    [Fact]
    public void CanTransition_FromCompletedToPending_ReturnsFalse()
    {
        OrderStatusRules.CanTransition(OrderStatusNames.Completed, OrderStatusNames.Pending)
            .Should().BeFalse();
    }

    [Fact]
    public void CanTransition_FromPendingToConfirmed_ReturnsTrue()
    {
        OrderStatusRules.CanTransition(OrderStatusNames.Pending, OrderStatusNames.Confirmed)
            .Should().BeTrue();
    }
}
