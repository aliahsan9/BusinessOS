using BusinessOS.Application.Features.Dashboard.DTOs;
using BusinessOS.Application.Features.Dashboard.Models;

namespace BusinessOS.Application.Features.Dashboard.Services;

public interface IDashboardService
{
    Task<DashboardOverviewResponse> GetOverviewAsync(
        DashboardDateRange dateRange,
        CancellationToken cancellationToken = default);
}

public interface IAnalyticsService
{
    Task<SalesAnalyticsResponse> GetSalesAnalyticsAsync(
        DashboardDateRange dateRange,
        CancellationToken cancellationToken = default);

    Task<CustomerAnalyticsDashboardResponse> GetCustomerAnalyticsAsync(
        DashboardDateRange dateRange,
        CancellationToken cancellationToken = default);

    Task<ProductAnalyticsResponse> GetProductAnalyticsAsync(
        DashboardDateRange dateRange,
        int topLimit,
        CancellationToken cancellationToken = default);

    Task<InventoryAnalyticsDashboardResponse> GetInventoryAnalyticsAsync(
        DashboardDateRange dateRange,
        CancellationToken cancellationToken = default);

    Task<OrderAnalyticsResponse> GetOrderAnalyticsAsync(
        DashboardDateRange dateRange,
        CancellationToken cancellationToken = default);
}

public interface IReportingService
{
    Task<ChartDataResponse> GetChartDataAsync(
        string chartType,
        DashboardDateRange dateRange,
        int topLimit = 10,
        CancellationToken cancellationToken = default);
}

public interface IDashboardCacheService
{
    Task<T> GetOrCreateAsync<T>(
        string cacheKey,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default);
}
