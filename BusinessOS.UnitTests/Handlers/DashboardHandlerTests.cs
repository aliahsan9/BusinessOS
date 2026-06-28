using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Dashboard.Models;
using BusinessOS.Application.Features.Dashboard.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.MultiTenancy;
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
        var context = await CreateSeededContextAsync(tenantId);
        var dateRange = new DashboardDateRange(DateTime.UnixEpoch, DateTime.UtcNow.AddMinutes(1), DateRangePeriods.All);

        var userAnalytics = new Mock<IUserAnalyticsService>();
        userAnalytics.Setup(x => x.GetActiveUserCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(3);

        var service = new DashboardService(context, userAnalytics.Object);
        var result = await service.GetOverviewAsync(dateRange, CancellationToken.None);

        result.TotalProducts.Should().Be(2);
        result.TotalCategories.Should().Be(1);
        result.TotalCustomers.Should().Be(1);
        result.TotalOrders.Should().Be(2);
        result.TotalRevenue.Should().Be(100);
        result.TotalInventoryValue.Should().Be(20);
        result.TotalActiveUsers.Should().Be(3);
        result.LowStockProducts.Should().Be(1);
        result.OutOfStockProducts.Should().Be(1);
    }

    private static async Task<BusinessOSDbContext> CreateSeededContextAsync(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<BusinessOSDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenantProvider = new TenantProvider();
        tenantProvider.SetTenantId(tenantId);

        var context = new BusinessOSDbContext(options, tenantProvider);
        var categoryId = Guid.NewGuid();
        var productId1 = Guid.NewGuid();
        var productId2 = Guid.NewGuid();

        context.Categories.Add(new Category
        {
            Id = categoryId,
            TenantId = tenantId,
            Name = "C1",
            CreatedAt = DateTime.UtcNow
        });

        context.Products.AddRange(
            new Product
            {
                Id = productId1,
                TenantId = tenantId,
                CategoryId = categoryId,
                Name = "P1",
                SKU = "SKU1",
                CostPrice = 10,
                CreatedAt = DateTime.UtcNow
            },
            new Product
            {
                Id = productId2,
                TenantId = tenantId,
                CategoryId = categoryId,
                Name = "P2",
                SKU = "SKU2",
                CostPrice = 5,
                CreatedAt = DateTime.UtcNow
            });

        context.Customers.Add(new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FirstName = "A",
            LastName = "B",
            Email = "a@test.com",
            PhoneNumber = "1",
            Address = "A",
            City = "B",
            Country = "C",
            PostalCode = "1",
            CreatedAt = DateTime.UtcNow
        });

        context.Orders.AddRange(
            new Order
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CustomerId = Guid.NewGuid(),
                OrderNumber = "ORD-1",
                OrderDate = DateTime.UtcNow,
                Status = OrderStatusNames.Completed,
                GrandTotal = 100
            },
            new Order
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CustomerId = Guid.NewGuid(),
                OrderNumber = "ORD-2",
                OrderDate = DateTime.UtcNow,
                Status = OrderStatusNames.Cancelled,
                GrandTotal = 50
            });

        context.Inventories.AddRange(
            new Inventory
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductId = productId1,
                CurrentStock = 2,
                ReorderLevel = 5
            },
            new Inventory
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductId = productId2,
                CurrentStock = 0,
                ReorderLevel = 5
            });

        await context.SaveChangesAsync();
        return context;
    }
}

public class AnalyticsServiceTests
{
    [Fact]
    public async Task GetSalesAnalyticsAsync_CalculatesRevenueAndRates()
    {
        var tenantId = Guid.NewGuid();
        var context = await CreateOrderContextAsync(tenantId);
        var dateRange = new DashboardDateRange(DateTime.UnixEpoch, DateTime.UtcNow.AddMinutes(1), DateRangePeriods.All);

        var service = new AnalyticsService(context);
        var result = await service.GetSalesAnalyticsAsync(dateRange, CancellationToken.None);

        result.CompletedOrders.Should().Be(2);
        result.CancelledOrders.Should().Be(1);
        result.AverageOrderValue.Should().Be(150);
        result.RevenueTrends.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetOrderAnalyticsAsync_CalculatesSuccessAndCancellationRates()
    {
        var tenantId = Guid.NewGuid();
        var context = await CreateOrderContextAsync(tenantId);
        var dateRange = new DashboardDateRange(DateTime.UnixEpoch, DateTime.UtcNow.AddMinutes(1), DateRangePeriods.All);

        var service = new AnalyticsService(context);
        var result = await service.GetOrderAnalyticsAsync(dateRange, CancellationToken.None);

        result.OrderSuccessRate.Should().Be(50);
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

    private static async Task<BusinessOSDbContext> CreateOrderContextAsync(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<BusinessOSDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenantProvider = new TenantProvider();
        tenantProvider.SetTenantId(tenantId);

        var context = new BusinessOSDbContext(options, tenantProvider);
        context.Orders.AddRange(
            new Order
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CustomerId = Guid.NewGuid(),
                OrderNumber = "ORD-1",
                OrderDate = DateTime.UtcNow,
                Status = OrderStatusNames.Completed,
                GrandTotal = 200
            },
            new Order
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CustomerId = Guid.NewGuid(),
                OrderNumber = "ORD-2",
                OrderDate = DateTime.UtcNow,
                Status = OrderStatusNames.Completed,
                GrandTotal = 100
            },
            new Order
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CustomerId = Guid.NewGuid(),
                OrderNumber = "ORD-3",
                OrderDate = DateTime.UtcNow,
                Status = OrderStatusNames.Cancelled,
                GrandTotal = 50
            },
            new Order
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CustomerId = Guid.NewGuid(),
                OrderNumber = "ORD-4",
                OrderDate = DateTime.UtcNow,
                Status = OrderStatusNames.Pending,
                GrandTotal = 25
            });

        await context.SaveChangesAsync();
        return context;
    }
}
