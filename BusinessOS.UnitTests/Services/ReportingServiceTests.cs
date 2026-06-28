using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Dashboard.DTOs;
using BusinessOS.Application.Features.Dashboard.Models;
using BusinessOS.Application.Features.Dashboard.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BusinessOS.UnitTests.Services;

public class ReportingServiceTests
{
    [Theory]
    [InlineData(ChartTypes.Revenue)]
    [InlineData(ChartTypes.Orders)]
    [InlineData(ChartTypes.Customers)]
    [InlineData(ChartTypes.Products)]
    [InlineData(ChartTypes.Inventory)]
    public async Task GetChartDataAsync_SupportedTypes_ReturnChart(string chartType)
    {
        var (context, tenantId) = await CreateReportingContextAsync();
        var analytics = new AnalyticsService(context);
        var service = new ReportingService(context, analytics);
        var dateRange = new DashboardDateRange(DateTime.UnixEpoch, DateTime.UtcNow.AddMinutes(1), DateRangePeriods.All);

        var result = await service.GetChartDataAsync(chartType, dateRange, topLimit: 5);

        result.ChartType.Should().NotBeNullOrWhiteSpace();
        result.Title.Should().NotBeNullOrWhiteSpace();
        result.DateRange.Period.Should().Be("all");
    }

    [Fact]
    public async Task GetChartDataAsync_UnsupportedType_ThrowsBadRequest()
    {
        var (context, _) = await CreateReportingContextAsync();
        var service = new ReportingService(context, new AnalyticsService(context));
        var dateRange = new DashboardDateRange(DateTime.UnixEpoch, DateTime.UtcNow, DateRangePeriods.All);

        var act = () => service.GetChartDataAsync("invalid", dateRange);

        await act.Should().ThrowAsync<Application.Common.Exceptions.BadRequestException>();
    }

    private static async Task<(IApplicationDbContext Context, Guid TenantId)> CreateReportingContextAsync()
    {
        var (context, tenantId) = await TestDataFactory.CreateCatalogContextAsync(productStock: 5, reorderLevel: 10);
        var customer = TestDataFactory.CreateCustomer(tenantId);
        var order = TestDataFactory.CreateOrder(tenantId, customer.Id, OrderStatusNames.Completed, 150);
        var product = context.Products.First();

        context.Customers.Add(customer);
        context.Orders.Add(order);
        context.OrderItems.Add(TestDataFactory.CreateOrderItem(order.Id, product.Id, 2, 75));
        context.StockTransactions.Add(
            TestDataFactory.CreateStockTransaction(tenantId, product.Id, product: product));

        await context.SaveChangesAsync();
        return (context, tenantId);
    }
}

public class AnalyticsServiceExtendedTests
{
    [Fact]
    public async Task GetCustomerAnalyticsAsync_CalculatesGrowthAndTopCustomers()
    {
        var (context, tenantId) = await TestDataFactory.CreateCatalogContextAsync();
        var customer = TestDataFactory.CreateCustomer(tenantId, "top@test.com", "Top", "Customer");
        var order = TestDataFactory.CreateOrder(tenantId, customer.Id, OrderStatusNames.Completed, 500);

        context.Customers.Add(customer);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var service = new AnalyticsService(context);
        var dateRange = new DashboardDateRange(DateTime.UnixEpoch, DateTime.UtcNow.AddMinutes(1), DateRangePeriods.All);

        var result = await service.GetCustomerAnalyticsAsync(dateRange);

        result.TotalCustomers.Should().Be(1);
        result.ActiveCustomers.Should().Be(1);
        result.TopCustomers.Should().ContainSingle(x => x.Email == "top@test.com");
    }

    [Fact]
    public async Task GetProductAnalyticsAsync_RanksProductsByRevenue()
    {
        var (context, tenantId) = await TestDataFactory.CreateCatalogContextAsync();
        var customer = TestDataFactory.CreateCustomer(tenantId);
        var order = TestDataFactory.CreateOrder(tenantId, customer.Id, OrderStatusNames.Completed, 200);
        var product = context.Products.First();

        context.Customers.Add(customer);
        context.Orders.Add(order);
        context.OrderItems.Add(TestDataFactory.CreateOrderItem(order.Id, product.Id, 4, 50));
        await context.SaveChangesAsync();

        var service = new AnalyticsService(context);
        var dateRange = new DashboardDateRange(DateTime.UnixEpoch, DateTime.UtcNow.AddMinutes(1), DateRangePeriods.All);

        var result = await service.GetProductAnalyticsAsync(dateRange, topLimit: 5);

        result.ProductRevenue.Should().NotBeEmpty();
        result.BestSellingProducts.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetInventoryAnalyticsAsync_IdentifiesLowAndOutOfStock()
    {
        var (context, tenantId) = await TestDataFactory.CreateCatalogContextAsync(productStock: 0, reorderLevel: 5);
        var product = context.Products.First();
        context.StockTransactions.Add(
            TestDataFactory.CreateStockTransaction(tenantId, product.Id, StockTransactionTypeNames.Sale, 5, 5, 0, product));
        await context.SaveChangesAsync();

        var service = new AnalyticsService(context);
        var dateRange = new DashboardDateRange(DateTime.UnixEpoch, DateTime.UtcNow.AddMinutes(1), DateRangePeriods.All);

        var result = await service.GetInventoryAnalyticsAsync(dateRange);

        result.OutOfStockProducts.Should().BeGreaterThan(0);
        result.ReorderRecommendations.Should().NotBeEmpty();
    }
}

public class DashboardQueryHandlerTests
{
    [Fact]
    public async Task GetOrderAnalyticsQueryHandler_ReturnsCachedAnalytics()
    {
        var (context, tenantId) = await TestDataFactory.CreateCatalogContextAsync();
        var customer = TestDataFactory.CreateCustomer(tenantId);
        context.Customers.Add(customer);
        context.Orders.Add(TestDataFactory.CreateOrder(tenantId, customer.Id, OrderStatusNames.Completed));
        await context.SaveChangesAsync();

        var handler = CreateDashboardHandler(context);
        var result = await handler.Handle(
            new Application.Features.Dashboard.Queries.GetOrderAnalytics.GetOrderAnalyticsQuery(null, null, "all"),
            CancellationToken.None);

        result.OrdersByStatus.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetCustomerAnalyticsDashboardQueryHandler_ReturnsCustomerMetrics()
    {
        var (context, tenantId) = await TestDataFactory.CreateCatalogContextAsync();
        context.Customers.Add(TestDataFactory.CreateCustomer(tenantId));
        await context.SaveChangesAsync();

        var handler = CreateCustomerDashboardHandler(context);
        var result = await handler.Handle(
            new Application.Features.Dashboard.Queries.GetCustomerAnalytics.GetCustomerAnalyticsDashboardQuery(null, null, "all"),
            CancellationToken.None);

        result.TotalCustomers.Should().BeGreaterThan(0);
    }

    private static Application.Features.Dashboard.Queries.GetOrderAnalytics.GetOrderAnalyticsQueryHandler CreateDashboardHandler(
        IApplicationDbContext context)
    {
        var tenantProvider = new BusinessOS.Infrastructure.MultiTenancy.TenantProvider();
        tenantProvider.SetTenantId(Guid.NewGuid());

        var cache = new DashboardCacheService(
            new MemoryCache(new MemoryCacheOptions()),
            tenantProvider,
            Options.Create(new DashboardCacheOptions()));

        return new Application.Features.Dashboard.Queries.GetOrderAnalytics.GetOrderAnalyticsQueryHandler(
            new DashboardDateRangeResolver(),
            new AnalyticsService(context),
            cache);
    }

    private static Application.Features.Dashboard.Queries.GetCustomerAnalytics.GetCustomerAnalyticsDashboardQueryHandler CreateCustomerDashboardHandler(
        IApplicationDbContext context)
    {
        var tenantProvider = new BusinessOS.Infrastructure.MultiTenancy.TenantProvider();
        tenantProvider.SetTenantId(Guid.NewGuid());

        var cache = new DashboardCacheService(
            new MemoryCache(new MemoryCacheOptions()),
            tenantProvider,
            Options.Create(new DashboardCacheOptions()));

        return new Application.Features.Dashboard.Queries.GetCustomerAnalytics.GetCustomerAnalyticsDashboardQueryHandler(
            new DashboardDateRangeResolver(),
            new AnalyticsService(context),
            cache);
    }
}
