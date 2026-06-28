using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Dashboard.DTOs;
using BusinessOS.Application.Features.Dashboard.Models;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Dashboard.Services;

public sealed class ReportingService : IReportingService
{
    private readonly IApplicationDbContext _context;
    private readonly IAnalyticsService _analyticsService;

    public ReportingService(
        IApplicationDbContext context,
        IAnalyticsService analyticsService)
    {
        _context = context;
        _analyticsService = analyticsService;
    }

    public async Task<ChartDataResponse> GetChartDataAsync(
        string chartType,
        DashboardDateRange dateRange,
        int topLimit = 10,
        CancellationToken cancellationToken = default)
    {
        return chartType.ToLowerInvariant() switch
        {
            ChartTypes.Revenue => await BuildRevenueChartAsync(dateRange, cancellationToken),
            ChartTypes.Orders => await BuildOrdersChartAsync(dateRange, cancellationToken),
            ChartTypes.Customers => await BuildCustomersChartAsync(dateRange, cancellationToken),
            ChartTypes.Products => await BuildProductsChartAsync(dateRange, topLimit, cancellationToken),
            ChartTypes.Inventory => await BuildInventoryChartAsync(dateRange, cancellationToken),
            _ => throw new Application.Common.Exceptions.BadRequestException(
                $"Unsupported chart type '{chartType}'. Valid values: revenue, orders, customers, products, inventory.")
        };
    }

    private async Task<ChartDataResponse> BuildRevenueChartAsync(
        DashboardDateRange dateRange,
        CancellationToken cancellationToken)
    {
        var sales = await _analyticsService.GetSalesAnalyticsAsync(dateRange, cancellationToken);

        return new ChartDataResponse
        {
            ChartType = "line",
            Title = "Revenue Trend",
            Labels = sales.RevenueTrends.Select(x => x.Date.ToString("yyyy-MM-dd")).ToList(),
            Datasets =
            [
                new ChartDatasetDto
                {
                    Label = "Revenue",
                    Data = sales.RevenueTrends.Select(x => x.Revenue).ToList(),
                    ChartStyle = "line"
                },
                new ChartDatasetDto
                {
                    Label = "Orders",
                    Data = sales.RevenueTrends.Select(x => (decimal)x.OrderCount).ToList(),
                    ChartStyle = "bar"
                }
            ],
            DateRange = DashboardService.MapDateRange(dateRange)
        };
    }

    private async Task<ChartDataResponse> BuildOrdersChartAsync(
        DashboardDateRange dateRange,
        CancellationToken cancellationToken)
    {
        var orders = await _analyticsService.GetOrderAnalyticsAsync(dateRange, cancellationToken);

        return new ChartDataResponse
        {
            ChartType = "bar",
            Title = "Orders By Status",
            Labels = orders.OrdersByStatus.Select(x => x.Status).ToList(),
            Datasets =
            [
                new ChartDatasetDto
                {
                    Label = "Order Count",
                    Data = orders.OrdersByStatus.Select(x => (decimal)x.Count).ToList(),
                    ChartStyle = "bar"
                },
                new ChartDatasetDto
                {
                    Label = "Percentage",
                    Data = orders.OrdersByStatus.Select(x => x.Percentage).ToList(),
                    ChartStyle = "doughnut"
                }
            ],
            DateRange = DashboardService.MapDateRange(dateRange)
        };
    }

    private async Task<ChartDataResponse> BuildCustomersChartAsync(
        DashboardDateRange dateRange,
        CancellationToken cancellationToken)
    {
        var customers = await _analyticsService.GetCustomerAnalyticsAsync(dateRange, cancellationToken);

        var newCustomersByMonth = await _context.Customers
            .AsNoTracking()
            .Where(x => x.CreatedAt >= dateRange.StartDate && x.CreatedAt <= dateRange.EndDate)
            .GroupBy(x => new { x.CreatedAt.Year, x.CreatedAt.Month })
            .Select(g => new
            {
                Period = new DateTime(g.Key.Year, g.Key.Month, 1),
                Count = g.Count()
            })
            .OrderBy(x => x.Period)
            .ToListAsync(cancellationToken);

        return new ChartDataResponse
        {
            ChartType = "line",
            Title = "Customer Growth",
            Labels = newCustomersByMonth.Select(x => x.Period.ToString("yyyy-MM")).ToList(),
            Datasets =
            [
                new ChartDatasetDto
                {
                    Label = "New Customers",
                    Data = newCustomersByMonth.Select(x => (decimal)x.Count).ToList(),
                    ChartStyle = "line"
                },
                new ChartDatasetDto
                {
                    Label = "Top Customer Spending",
                    Data = customers.TopCustomers.Select(x => x.TotalSpending).ToList(),
                    ChartStyle = "bar"
                }
            ],
            DateRange = DashboardService.MapDateRange(dateRange)
        };
    }

    private async Task<ChartDataResponse> BuildProductsChartAsync(
        DashboardDateRange dateRange,
        int topLimit,
        CancellationToken cancellationToken)
    {
        var products = await _analyticsService.GetProductAnalyticsAsync(
            dateRange,
            topLimit,
            cancellationToken);

        return new ChartDataResponse
        {
            ChartType = "bar",
            Title = "Top Product Revenue",
            Labels = products.ProductRevenue.Select(x => x.ProductName).ToList(),
            Datasets =
            [
                new ChartDatasetDto
                {
                    Label = "Revenue",
                    Data = products.ProductRevenue.Select(x => x.Revenue).ToList(),
                    ChartStyle = "bar"
                },
                new ChartDatasetDto
                {
                    Label = "Quantity Sold",
                    Data = products.ProductRevenue.Select(x => x.QuantitySold).ToList(),
                    ChartStyle = "line"
                }
            ],
            DateRange = DashboardService.MapDateRange(dateRange)
        };
    }

    private async Task<ChartDataResponse> BuildInventoryChartAsync(
        DashboardDateRange dateRange,
        CancellationToken cancellationToken)
    {
        var inventory = await _analyticsService.GetInventoryAnalyticsAsync(dateRange, cancellationToken);

        var statusCounts = inventory.StockLevels
            .GroupBy(x => x.StockStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToList();

        return new ChartDataResponse
        {
            ChartType = "pie",
            Title = "Inventory Stock Status",
            Labels = statusCounts.Select(x => x.Status).ToList(),
            Datasets =
            [
                new ChartDatasetDto
                {
                    Label = "Products",
                    Data = statusCounts.Select(x => (decimal)x.Count).ToList(),
                    ChartStyle = "pie"
                },
                new ChartDatasetDto
                {
                    Label = "Stock In",
                    Data = inventory.StockMovementTrends.Select(x => x.TotalIn).ToList(),
                    ChartStyle = "line"
                },
                new ChartDatasetDto
                {
                    Label = "Stock Out",
                    Data = inventory.StockMovementTrends.Select(x => x.TotalOut).ToList(),
                    ChartStyle = "line"
                }
            ],
            DateRange = DashboardService.MapDateRange(dateRange)
        };
    }
}
