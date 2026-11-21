using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Dashboard.DTOs;
using BusinessOS.Application.Features.Dashboard.Models;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Dashboard.Services;

public sealed class AnalyticsService : IAnalyticsService
{
    private readonly IApplicationDbContext _context;

    public AnalyticsService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SalesAnalyticsResponse> GetSalesAnalyticsAsync(
        DashboardDateRange dateRange,
        CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var todayStart = utcNow.Date;
        var weekStart = utcNow.Date.AddDays(-(int)utcNow.DayOfWeek);
        var monthStart = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var yearStart = new DateTime(utcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var completedOrders = _context.Orders
            .AsNoTracking()
            .Where(x => x.Status == OrderStatusNames.Completed);

        // Single round-trip for period KPIs instead of four separate Sum queries.
        var periodSales = await completedOrders
            .Where(x => x.OrderDate >= yearStart)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Today = g.Sum(x => x.OrderDate >= todayStart ? x.GrandTotal : 0m),
                Weekly = g.Sum(x => x.OrderDate >= weekStart ? x.GrandTotal : 0m),
                Monthly = g.Sum(x => x.OrderDate >= monthStart ? x.GrandTotal : 0m),
                Yearly = g.Sum(x => x.GrandTotal)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var todaySales = periodSales?.Today ?? 0m;
        var weeklySales = periodSales?.Weekly ?? 0m;
        var monthlySales = periodSales?.Monthly ?? 0m;
        var yearlySales = periodSales?.Yearly ?? 0m;

        var rangeStats = await _context.Orders
            .AsNoTracking()
            .Where(x => x.OrderDate >= dateRange.StartDate && x.OrderDate <= dateRange.EndDate)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Completed = g.Sum(x => x.Status == OrderStatusNames.Completed ? 1 : 0),
                Cancelled = g.Sum(x => x.Status == OrderStatusNames.Cancelled ? 1 : 0),
                CompletedRevenue = g.Sum(x => x.Status == OrderStatusNames.Completed ? x.GrandTotal : 0m)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var completed = rangeStats?.Completed ?? 0;
        var cancelled = rangeStats?.Cancelled ?? 0;
        var completedRevenue = rangeStats?.CompletedRevenue ?? 0m;

        // Only completed rows needed for trend buckets — projected, not full entities.
        var completedRowsRaw = await _context.Orders
            .AsNoTracking()
            .Where(x =>
                x.OrderDate >= dateRange.StartDate &&
                x.OrderDate <= dateRange.EndDate &&
                x.Status == OrderStatusNames.Completed)
            .Select(x => new { x.OrderDate, x.GrandTotal })
            .ToListAsync(cancellationToken);
        var revenueTrends = BuildRevenueTrends(
            completedRowsRaw.Select(x => (x.OrderDate, x.GrandTotal)).ToList(),
            dateRange);

        return new SalesAnalyticsResponse
        {
            TodaySales = Math.Round(todaySales, 2),
            WeeklySales = Math.Round(weeklySales, 2),
            MonthlySales = Math.Round(monthlySales, 2),
            YearlySales = Math.Round(yearlySales, 2),
            AverageOrderValue = completed > 0
                ? Math.Round(completedRevenue / completed, 2)
                : 0,
            CompletedOrders = completed,
            CancelledOrders = cancelled,
            RevenueTrends = revenueTrends,
            DateRange = DashboardService.MapDateRange(dateRange)
        };
    }

    private static List<RevenueTrendPointDto> BuildRevenueTrends(
        IReadOnlyList<(DateTime OrderDate, decimal GrandTotal)> orders,
        DashboardDateRange dateRange)
    {
        var window = TrendSeries.ChartWindow(dateRange);
        var grain = TrendSeries.ForSales(dateRange);

        var lookup = orders
            .Where(x => x.OrderDate >= window.StartDate && x.OrderDate <= window.EndDate)
            .GroupBy(x => TrendSeries.Truncate(x.OrderDate, grain))
            .ToDictionary(g => g.Key, g => (Revenue: g.Sum(x => x.GrandTotal), Count: g.Count()));

        return TrendSeries.Buckets(window, grain)
            .Select(bucket =>
            {
                lookup.TryGetValue(bucket, out var hit);
                return new RevenueTrendPointDto
                {
                    Date = bucket,
                    Revenue = Math.Round(hit.Revenue, 2),
                    OrderCount = hit.Count
                };
            })
            .ToList();
    }

    public async Task<CustomerAnalyticsDashboardResponse> GetCustomerAnalyticsAsync(
        DashboardDateRange dateRange,
        CancellationToken cancellationToken = default)
    {
        var totalCustomers = await _context.Customers
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var newCustomers = await _context.Customers
            .AsNoTracking()
            .Where(x => x.CreatedAt >= dateRange.StartDate && x.CreatedAt <= dateRange.EndDate)
            .CountAsync(cancellationToken);

        var previousPeriodLength = dateRange.EndDate - dateRange.StartDate;
        var previousStart = dateRange.StartDate - previousPeriodLength;
        var previousNewCustomers = await _context.Customers
            .AsNoTracking()
            .Where(x => x.CreatedAt >= previousStart && x.CreatedAt < dateRange.StartDate)
            .CountAsync(cancellationToken);

        var customerGrowthRate = previousNewCustomers > 0
            ? Math.Round((decimal)(newCustomers - previousNewCustomers) / previousNewCustomers * 100, 2)
            : newCustomers > 0 ? 100 : 0;

        var orderAggregates = await _context.Orders
            .AsNoTracking()
            .Where(x =>
                x.OrderDate >= dateRange.StartDate &&
                x.OrderDate <= dateRange.EndDate &&
                x.Status == OrderStatusNames.Completed)
            .GroupBy(x => x.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                TotalOrders = g.Count(),
                TotalSpending = g.Sum(x => x.GrandTotal)
            })
            .ToListAsync(cancellationToken);

        var activeCustomers = orderAggregates.Count;
        var totalSpending = orderAggregates.Sum(x => x.TotalSpending);
        var averageSpending = activeCustomers > 0
            ? Math.Round(totalSpending / activeCustomers, 2)
            : 0;
        var lifetimeValue = totalCustomers > 0
            ? Math.Round(totalSpending / totalCustomers, 2)
            : 0;

        var topCustomerIds = orderAggregates
            .OrderByDescending(x => x.TotalSpending)
            .Take(10)
            .Select(x => x.CustomerId)
            .ToList();

        var customerDetails = await _context.Customers
        .AsNoTracking()
        .Where(x => topCustomerIds.Contains(x.Id))
        .Select(x => new { x.Id, x.FirstName, x.LastName, x.Email })
        .ToDictionaryAsync(x => x.Id, cancellationToken);

        var topCustomers = orderAggregates
            .OrderByDescending(x => x.TotalSpending)
            .Take(10)
            .Where(x => customerDetails.ContainsKey(x.CustomerId))
            .Select(x =>
            {
                var customer = customerDetails[x.CustomerId];

                return new TopCustomerDto
                {
                    CustomerId = x.CustomerId,
                    CustomerName = $"{customer.FirstName} {customer.LastName}".Trim(),
                    Email = customer.Email,
                    TotalOrders = x.TotalOrders,
                    TotalSpending = Math.Round(x.TotalSpending, 2)
                };
            })
            .ToList();

        return new CustomerAnalyticsDashboardResponse
        {
            TotalCustomers = totalCustomers,
            NewCustomers = newCustomers,
            ActiveCustomers = activeCustomers,
            CustomerLifetimeValue = lifetimeValue,
            AverageCustomerSpending = averageSpending,
            CustomerGrowthRate = customerGrowthRate,
            TopCustomers = topCustomers,
            DateRange = DashboardService.MapDateRange(dateRange)
        };
    }

    public async Task<ProductAnalyticsResponse> GetProductAnalyticsAsync(
        DashboardDateRange dateRange,
        int topLimit,
        CancellationToken cancellationToken = default)
    {
        var productStats = await _context.OrderItems
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                x.Order.OrderDate >= dateRange.StartDate &&
                x.Order.OrderDate <= dateRange.EndDate &&
                x.Order.Status == OrderStatusNames.Completed)
            .GroupBy(x => new { x.ProductId, ProductName = x.Product!.Name, Sku = x.Product.SKU })
            .Select(g => new ProductPerformanceDto
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.ProductName,
                Sku = g.Key.Sku,
                QuantitySold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.Total),
                OrderCount = g.Select(x => x.OrderId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);

        var ranked = productStats
            .OrderByDescending(x => x.Revenue)
            .Select((x, index) => MapProductPerformance(x, index + 1))
            .ToList();

        var bestSelling = ranked
            .OrderByDescending(x => x.QuantitySold)
            .Take(topLimit)
            .Select((x, index) => MapProductPerformance(x, index + 1))
            .ToList();

        var worstSelling = ranked
            .Where(x => x.QuantitySold > 0)
            .OrderBy(x => x.QuantitySold)
            .Take(topLimit)
            .Select((x, index) => MapProductPerformance(x, index + 1))
            .ToList();

        var mostOrdered = ranked
            .OrderByDescending(x => x.OrderCount)
            .Take(topLimit)
            .Select((x, index) => MapProductPerformance(x, index + 1))
            .ToList();

        var productRevenue = ranked
            .OrderByDescending(x => x.Revenue)
            .Take(topLimit)
            .Select((x, index) => MapProductPerformance(x, index + 1))
            .ToList();

        return new ProductAnalyticsResponse
        {
            BestSellingProducts = bestSelling,
            WorstSellingProducts = worstSelling,
            MostOrderedProducts = mostOrdered,
            ProductRevenue = productRevenue,
            ProductPerformanceRanking = ranked.Take(topLimit).ToList(),
            TopLimit = topLimit,
            DateRange = DashboardService.MapDateRange(dateRange)
        };
    }

    public async Task<InventoryAnalyticsDashboardResponse> GetInventoryAnalyticsAsync(
        DashboardDateRange dateRange,
        CancellationToken cancellationToken = default)
    {
        var inventoryTotals = await _context.Inventories
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                InventoryValue = g.Sum(x => x.CurrentStock * x.Product.CostPrice),
                TotalStockQuantity = g.Sum(x => x.CurrentStock),
                LowStockProducts = g.Sum(x => x.CurrentStock > 0 && x.CurrentStock <= x.ReorderLevel ? 1 : 0),
                OutOfStockProducts = g.Sum(x => x.CurrentStock <= 0 ? 1 : 0)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var inventoryValue = inventoryTotals?.InventoryValue ?? 0m;
        var totalStockQuantity = inventoryTotals?.TotalStockQuantity ?? 0m;

        // Cap stock-level payload for large catalogs; KPIs above remain exact.
        var stockLevels = await _context.Inventories
            .AsNoTracking()
            .OrderBy(x => x.CurrentStock)
            .Take(500)
            .Select(x => new InventoryStockLevelDto
            {
                ProductId = x.ProductId,
                ProductName = x.Product.Name,
                CurrentStock = x.CurrentStock,
                ReorderLevel = x.ReorderLevel,
                StockStatus = x.CurrentStock <= 0
                    ? "OutOfStock"
                    : x.CurrentStock <= x.ReorderLevel
                        ? "LowStock"
                        : "InStock"
            })
            .ToListAsync(cancellationToken);

        var reorderRecommendations = await _context.Inventories
            .AsNoTracking()
            .Where(x => x.CurrentStock <= x.ReorderLevel)
            .Select(x => new ReorderRecommendationDto
            {
                ProductId = x.ProductId,
                ProductName = x.Product.Name,
                CurrentStock = x.CurrentStock,
                ReorderLevel = x.ReorderLevel,
                SuggestedReorderQuantity = Math.Max(x.MaximumStockLevel - x.CurrentStock, x.ReorderLevel)
            })
            .OrderBy(x => x.CurrentStock)
            .Take(20)
            .ToListAsync(cancellationToken);

        var stockMovementTrends = await _context.StockTransactions
            .AsNoTracking()
            .Where(x => x.CreatedAt >= dateRange.StartDate && x.CreatedAt <= dateRange.EndDate)
            .GroupBy(x => x.CreatedAt.Date)
            .Select(g => new StockMovementTrendPointDto
            {
                Date = g.Key,
                TotalIn = g.Where(x =>
                        x.TransactionType == StockTransactionTypeNames.Purchase ||
                        x.TransactionType == StockTransactionTypeNames.Return)
                    .Sum(x => x.Quantity),
                TotalOut = g.Where(x =>
                        x.TransactionType == StockTransactionTypeNames.Sale ||
                        x.TransactionType == StockTransactionTypeNames.Damage)
                    .Sum(x => x.Quantity)
            })
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken);

        return new InventoryAnalyticsDashboardResponse
        {
            InventoryValue = Math.Round(inventoryValue, 2),
            TotalStockQuantity = totalStockQuantity,
            LowStockProducts = inventoryTotals?.LowStockProducts ?? 0,
            OutOfStockProducts = inventoryTotals?.OutOfStockProducts ?? 0,
            StockLevels = stockLevels,
            ReorderRecommendations = reorderRecommendations,
            StockMovementTrends = stockMovementTrends,
            DateRange = DashboardService.MapDateRange(dateRange)
        };
    }

    public async Task<OrderAnalyticsResponse> GetOrderAnalyticsAsync(
        DashboardDateRange dateRange,
        CancellationToken cancellationToken = default)
    {
        var ordersByStatusRaw = await _context.Orders
            .AsNoTracking()
            .Where(x => x.OrderDate >= dateRange.StartDate && x.OrderDate <= dateRange.EndDate)
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var totalOrders = ordersByStatusRaw.Sum(x => x.Count);
        var completed = ordersByStatusRaw
            .Where(x => x.Status.Equals(OrderStatusNames.Completed, StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.Count);
        var cancelled = ordersByStatusRaw
            .Where(x => x.Status.Equals(OrderStatusNames.Cancelled, StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.Count);

        var ordersByStatus = ordersByStatusRaw
            .Select(g => new OrderStatusCountDto
            {
                Status = g.Status,
                Count = g.Count,
                Percentage = totalOrders > 0
                    ? Math.Round((decimal)g.Count / totalOrders * 100, 2)
                    : 0
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        var ordersPerDay = await _context.Orders
            .AsNoTracking()
            .Where(x => x.OrderDate >= dateRange.StartDate && x.OrderDate <= dateRange.EndDate)
            .GroupBy(x => x.OrderDate.Date)
            .Select(g => new OrderPeriodCountDto
            {
                Period = g.Key,
                Count = g.Count()
            })
            .OrderBy(x => x.Period)
            .ToListAsync(cancellationToken);

        // Group by Year/Month (translatable); build Period DateTime after materialization.
        var ordersPerMonthRaw = await _context.Orders
            .AsNoTracking()
            .Where(x => x.OrderDate >= dateRange.StartDate && x.OrderDate <= dateRange.EndDate)
            .GroupBy(x => new { x.OrderDate.Year, x.OrderDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToListAsync(cancellationToken);

        var ordersPerMonth = ordersPerMonthRaw
            .Select(g => new OrderPeriodCountDto
            {
                Period = new DateTime(g.Year, g.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                Count = g.Count
            })
            .ToList();

        return new OrderAnalyticsResponse
        {
            OrdersByStatus = ordersByStatus,
            OrdersPerDay = ordersPerDay,
            OrdersPerMonth = ordersPerMonth,
            OrderSuccessRate = totalOrders > 0
                ? Math.Round((decimal)completed / totalOrders * 100, 2)
                : 0,
            CancellationRate = totalOrders > 0
                ? Math.Round((decimal)cancelled / totalOrders * 100, 2)
                : 0,
            DateRange = DashboardService.MapDateRange(dateRange)
        };
    }

    private static ProductPerformanceDto MapProductPerformance(ProductPerformanceDto source, int rank) =>
        new()
        {
            ProductId = source.ProductId,
            ProductName = source.ProductName,
            Sku = source.Sku,
            QuantitySold = source.QuantitySold,
            Revenue = Math.Round(source.Revenue, 2),
            OrderCount = source.OrderCount,
            Rank = rank
        };
}
