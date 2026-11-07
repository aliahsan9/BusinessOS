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
        var (labels, counts) = await BuildCustomerGrowthSeriesAsync(dateRange, cancellationToken);

        return new ChartDataResponse
        {
            ChartType = "line",
            Title = "Customer Growth",
            Labels = labels,
            Datasets =
            [
                new ChartDatasetDto
                {
                    Label = "New Customers",
                    Data = counts,
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

    /// <summary>
    /// New customers over the active window. Short histories stay daily so quiet
    /// days remain visible; longer histories roll up to months.
    /// </summary>
    private async Task<(List<string> Labels, List<decimal> Counts)> BuildCustomerGrowthSeriesAsync(
        DashboardDateRange dateRange,
        CancellationToken cancellationToken)
    {
        var span = dateRange.EndDate - dateRange.StartDate;
        if (span <= TimeSpan.FromHours(36))
            return await BuildCustomerBucketsAsync(dateRange, TrendSeries.Grain.Hour, cancellationToken);
        if (span <= TimeSpan.FromDays(62) && !string.Equals(dateRange.Period, DateRangePeriods.All, StringComparison.OrdinalIgnoreCase))
            return await BuildCustomerBucketsAsync(dateRange, TrendSeries.Grain.Day, cancellationToken);
        if (span <= TimeSpan.FromDays(400) && !string.Equals(dateRange.Period, DateRangePeriods.All, StringComparison.OrdinalIgnoreCase))
            return await BuildCustomerBucketsAsync(dateRange, TrendSeries.Grain.Month, cancellationToken);

        var created = await _context.Customers
            .AsNoTracking()
            .Where(x => x.CreatedAt >= dateRange.StartDate && x.CreatedAt <= dateRange.EndDate)
            .GroupBy(x => new { x.CreatedAt.Year, x.CreatedAt.Month, x.CreatedAt.Day })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Count = g.Count() })
            .ToListAsync(cancellationToken);

        if (created.Count == 0)
            return ([], []);

        var points = created
            .Select(x => (
                Date: new DateTime(x.Year, x.Month, x.Day, 0, 0, 0, DateTimeKind.Utc),
                x.Count))
            .OrderBy(x => x.Date)
            .ToList();

        var first = points[0].Date;
        var last = points[^1].Date.Date;
        var rangeEnd = DateTime.SpecifyKind(dateRange.EndDate.Date, DateTimeKind.Utc);
        if (rangeEnd > last)
            last = rangeEnd;

        var labels = new List<string>();
        var counts = new List<decimal>();
        var spanDays = (last - first).TotalDays;

        if (spanDays <= 400)
        {
            var byDay = points.ToDictionary(x => x.Date.Date, x => x.Count);
            for (var day = first.Date; day <= last && labels.Count < 400; day = day.AddDays(1))
            {
                labels.Add(day.ToString("yyyy-MM-dd"));
                counts.Add(byDay.GetValueOrDefault(DateTime.SpecifyKind(day, DateTimeKind.Utc), 0));
            }

            return (labels, counts);
        }

        var byMonth = points
            .GroupBy(x => new DateTime(x.Date.Year, x.Date.Month, 1, 0, 0, 0, DateTimeKind.Utc))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Count));
        var month = new DateTime(first.Year, first.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endMonth = new DateTime(last.Year, last.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        while (month <= endMonth && labels.Count < 36)
        {
            labels.Add(month.ToString("yyyy-MM"));
            counts.Add(byMonth.GetValueOrDefault(month, 0));
            month = month.AddMonths(1);
        }

        return (labels, counts);
    }

    private async Task<(List<string> Labels, List<decimal> Counts)> BuildCustomerBucketsAsync(
        DashboardDateRange dateRange,
        TrendSeries.Grain grain,
        CancellationToken cancellationToken)
    {
        var stamps = await _context.Customers
            .AsNoTracking()
            .Where(x => x.CreatedAt >= dateRange.StartDate && x.CreatedAt <= dateRange.EndDate)
            .Select(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var lookup = stamps
            .GroupBy(x => TrendSeries.Truncate(x, grain))
            .ToDictionary(g => g.Key, g => g.Count());

        var labels = new List<string>();
        var counts = new List<decimal>();
        foreach (var bucket in TrendSeries.Buckets(dateRange, grain))
        {
            labels.Add(TrendSeries.Format(bucket, grain));
            counts.Add(lookup.GetValueOrDefault(bucket));
        }

        return (labels, counts);
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
