using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Dashboard.Models;
using BusinessOS.Application.Features.Dashboard.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace BusinessOS.UnitTests.Handlers;

public class DashboardServiceTests
{
    [Fact]
    public async Task GetOverviewAsync_CalculatesAggregates()
    {
        var tenantId = Guid.NewGuid();
        var dateRange = new DashboardDateRange(DateTime.UnixEpoch, DateTime.UtcNow, DateRangePeriods.All);

        var products = TestMockDbSet.CreateMockDbSet(new List<Product>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "P1",
                SKU = "SKU1",
                CategoryId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            }
        }.AsQueryable());

        var categories = TestMockDbSet.CreateMockDbSet(new List<Category>
        {
            new() { Id = Guid.NewGuid(), TenantId = tenantId, Name = "C1", CreatedAt = DateTime.UtcNow }
        }.AsQueryable());

        var customers = TestMockDbSet.CreateMockDbSet(new List<Customer>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                FirstName = "A",
                LastName = "B",
                Email = "a@test.com",
                CreatedAt = DateTime.UtcNow
            }
        }.AsQueryable());

        var orders = TestMockDbSet.CreateMockDbSet(new List<Order>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CustomerId = Guid.NewGuid(),
                OrderNumber = "ORD-1",
                OrderDate = DateTime.UtcNow,
                Status = OrderStatusNames.Completed,
                GrandTotal = 100
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CustomerId = Guid.NewGuid(),
                OrderNumber = "ORD-2",
                OrderDate = DateTime.UtcNow,
                Status = OrderStatusNames.Cancelled,
                GrandTotal = 50
            }
        }.AsQueryable());

        var inventories = TestMockDbSet.CreateMockDbSet(new List<Inventory>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductId = Guid.NewGuid(),
                CurrentStock = 2,
                ReorderLevel = 5,
                Product = new Product
                {
                    Id = Guid.NewGuid(),
                    CostPrice = 10,
                    Name = "P1",
                    SKU = "SKU1",
                    CategoryId = Guid.NewGuid(),
                    TenantId = tenantId
                }
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductId = Guid.NewGuid(),
                CurrentStock = 0,
                ReorderLevel = 5,
                Product = new Product
                {
                    Id = Guid.NewGuid(),
                    CostPrice = 5,
                    Name = "P2",
                    SKU = "SKU2",
                    CategoryId = Guid.NewGuid(),
                    TenantId = tenantId
                }
            }
        }.AsQueryable());

        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.Products).Returns(products.Object);
        context.Setup(x => x.Categories).Returns(categories.Object);
        context.Setup(x => x.Customers).Returns(customers.Object);
        context.Setup(x => x.Orders).Returns(orders.Object);
        context.Setup(x => x.Inventories).Returns(inventories.Object);

        var userAnalytics = new Mock<IUserAnalyticsService>();
        userAnalytics.Setup(x => x.GetActiveUserCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(3);

        var service = new DashboardService(context.Object, userAnalytics.Object);
        var result = await service.GetOverviewAsync(dateRange, CancellationToken.None);

        result.TotalProducts.Should().Be(1);
        result.TotalCategories.Should().Be(1);
        result.TotalCustomers.Should().Be(1);
        result.TotalOrders.Should().Be(2);
        result.TotalRevenue.Should().Be(100);
        result.TotalInventoryValue.Should().Be(20);
        result.TotalActiveUsers.Should().Be(3);
        result.LowStockProducts.Should().Be(1);
        result.OutOfStockProducts.Should().Be(1);
    }
}

public class AnalyticsServiceTests
{
    [Fact]
    public async Task GetSalesAnalyticsAsync_CalculatesRevenueAndRates()
    {
        var dateRange = new DashboardDateRange(DateTime.UnixEpoch, DateTime.UtcNow, DateRangePeriods.All);
        var orders = TestMockDbSet.CreateMockDbSet(new List<Order>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                OrderNumber = "ORD-1",
                OrderDate = DateTime.UtcNow,
                Status = OrderStatusNames.Completed,
                GrandTotal = 200
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                OrderNumber = "ORD-2",
                OrderDate = DateTime.UtcNow,
                Status = OrderStatusNames.Completed,
                GrandTotal = 100
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                OrderNumber = "ORD-3",
                OrderDate = DateTime.UtcNow,
                Status = OrderStatusNames.Cancelled,
                GrandTotal = 50
            }
        }.AsQueryable());

        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.Orders).Returns(orders.Object);
        context.Setup(x => x.Customers).Returns(TestMockDbSet.CreateMockDbSet(new List<Customer>().AsQueryable()).Object);
        context.Setup(x => x.OrderItems).Returns(TestMockDbSet.CreateMockDbSet(new List<OrderItem>().AsQueryable()).Object);
        context.Setup(x => x.Inventories).Returns(TestMockDbSet.CreateMockDbSet(new List<Inventory>().AsQueryable()).Object);
        context.Setup(x => x.StockTransactions).Returns(TestMockDbSet.CreateMockDbSet(new List<StockTransaction>().AsQueryable()).Object);

        var service = new AnalyticsService(context.Object);
        var result = await service.GetSalesAnalyticsAsync(dateRange, CancellationToken.None);

        result.CompletedOrders.Should().Be(2);
        result.CancelledOrders.Should().Be(1);
        result.AverageOrderValue.Should().Be(150);
        result.RevenueTrends.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetOrderAnalyticsAsync_CalculatesSuccessAndCancellationRates()
    {
        var dateRange = new DashboardDateRange(DateTime.UnixEpoch, DateTime.UtcNow, DateRangePeriods.All);
        var orders = TestMockDbSet.CreateMockDbSet(new List<Order>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                OrderNumber = "ORD-1",
                OrderDate = DateTime.UtcNow,
                Status = OrderStatusNames.Completed,
                GrandTotal = 100
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                OrderNumber = "ORD-2",
                OrderDate = DateTime.UtcNow,
                Status = OrderStatusNames.Cancelled,
                GrandTotal = 50
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                OrderNumber = "ORD-3",
                OrderDate = DateTime.UtcNow,
                Status = OrderStatusNames.Pending,
                GrandTotal = 25
            },
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                OrderNumber = "ORD-4",
                OrderDate = DateTime.UtcNow,
                Status = OrderStatusNames.Pending,
                GrandTotal = 25
            }
        }.AsQueryable());

        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.Orders).Returns(orders.Object);

        var service = new AnalyticsService(context.Object);
        var result = await service.GetOrderAnalyticsAsync(dateRange, CancellationToken.None);

        result.OrderSuccessRate.Should().Be(25);
        result.CancellationRate.Should().Be(25);
        result.OrdersByStatus.Should().HaveCount(3);
    }

    [Fact]
    public void DashboardDateRangeResolver_CustomRange_RequiresBothDates()
    {
        var resolver = new DashboardDateRangeResolver();
        var act = () => resolver.Resolve(DateTime.UtcNow.AddDays(-7), null, DateRangePeriods.Custom);
        act.Should().Throw<Application.Common.Exceptions.BadRequestException>();
    }

    [Fact]
    public void DashboardDateRangeResolver_Today_ReturnsUtcDayBounds()
    {
        var resolver = new DashboardDateRangeResolver();
        var range = resolver.Resolve(null, null, DateRangePeriods.Today);

        range.Period.Should().Be(DateRangePeriods.Today);
        range.StartDate.Date.Should().Be(DateTime.UtcNow.Date);
    }
}
