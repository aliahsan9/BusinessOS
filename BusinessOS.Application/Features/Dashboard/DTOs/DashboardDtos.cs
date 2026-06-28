namespace BusinessOS.Application.Features.Dashboard.DTOs;

/// <summary>Executive dashboard KPI snapshot.</summary>
public sealed class DashboardOverviewResponse
{
    public int TotalProducts { get; init; }
    public int TotalCategories { get; init; }
    public int TotalCustomers { get; init; }
    public int TotalOrders { get; init; }
    public decimal TotalRevenue { get; init; }
    public decimal TotalInventoryValue { get; init; }
    public int TotalActiveUsers { get; init; }
    public int LowStockProducts { get; init; }
    public int OutOfStockProducts { get; init; }
    public DashboardDateRangeInfo DateRange { get; init; } = default!;
}

public sealed class DashboardDateRangeInfo
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public string Period { get; init; } = default!;
}

public sealed class SalesAnalyticsResponse
{
    public decimal TodaySales { get; init; }
    public decimal WeeklySales { get; init; }
    public decimal MonthlySales { get; init; }
    public decimal YearlySales { get; init; }
    public decimal AverageOrderValue { get; init; }
    public int CompletedOrders { get; init; }
    public int CancelledOrders { get; init; }
    public IReadOnlyList<RevenueTrendPointDto> RevenueTrends { get; init; } = [];
    public DashboardDateRangeInfo DateRange { get; init; } = default!;
}

public sealed class RevenueTrendPointDto
{
    public DateTime Date { get; init; }
    public decimal Revenue { get; init; }
    public int OrderCount { get; init; }
}

public sealed class CustomerAnalyticsDashboardResponse
{
    public int TotalCustomers { get; init; }
    public int NewCustomers { get; init; }
    public int ActiveCustomers { get; init; }
    public decimal CustomerLifetimeValue { get; init; }
    public decimal AverageCustomerSpending { get; init; }
    public decimal CustomerGrowthRate { get; init; }
    public IReadOnlyList<TopCustomerDto> TopCustomers { get; init; } = [];
    public DashboardDateRangeInfo DateRange { get; init; } = default!;
}

public sealed class TopCustomerDto
{
    public Guid CustomerId { get; init; }
    public string CustomerName { get; init; } = default!;
    public string Email { get; init; } = default!;
    public int TotalOrders { get; init; }
    public decimal TotalSpending { get; init; }
}

public sealed class ProductAnalyticsResponse
{
    public IReadOnlyList<ProductPerformanceDto> BestSellingProducts { get; init; } = [];
    public IReadOnlyList<ProductPerformanceDto> WorstSellingProducts { get; init; } = [];
    public IReadOnlyList<ProductPerformanceDto> MostOrderedProducts { get; init; } = [];
    public IReadOnlyList<ProductPerformanceDto> ProductRevenue { get; init; } = [];
    public IReadOnlyList<ProductPerformanceDto> ProductPerformanceRanking { get; init; } = [];
    public int TopLimit { get; init; }
    public DashboardDateRangeInfo DateRange { get; init; } = default!;
}

public sealed class ProductPerformanceDto
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = default!;
    public string Sku { get; init; } = default!;
    public decimal QuantitySold { get; init; }
    public decimal Revenue { get; init; }
    public int OrderCount { get; init; }
    public int Rank { get; init; }
}

public sealed class InventoryAnalyticsDashboardResponse
{
    public decimal InventoryValue { get; init; }
    public decimal TotalStockQuantity { get; init; }
    public int LowStockProducts { get; init; }
    public int OutOfStockProducts { get; init; }
    public IReadOnlyList<InventoryStockLevelDto> StockLevels { get; init; } = [];
    public IReadOnlyList<ReorderRecommendationDto> ReorderRecommendations { get; init; } = [];
    public IReadOnlyList<StockMovementTrendPointDto> StockMovementTrends { get; init; } = [];
    public DashboardDateRangeInfo DateRange { get; init; } = default!;
}

public sealed class InventoryStockLevelDto
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = default!;
    public decimal CurrentStock { get; init; }
    public decimal ReorderLevel { get; init; }
    public string StockStatus { get; init; } = default!;
}

public sealed class ReorderRecommendationDto
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = default!;
    public decimal CurrentStock { get; init; }
    public decimal ReorderLevel { get; init; }
    public decimal SuggestedReorderQuantity { get; init; }
}

public sealed class StockMovementTrendPointDto
{
    public DateTime Date { get; init; }
    public decimal TotalIn { get; init; }
    public decimal TotalOut { get; init; }
}

public sealed class OrderAnalyticsResponse
{
    public IReadOnlyList<OrderStatusCountDto> OrdersByStatus { get; init; } = [];
    public IReadOnlyList<OrderPeriodCountDto> OrdersPerDay { get; init; } = [];
    public IReadOnlyList<OrderPeriodCountDto> OrdersPerMonth { get; init; } = [];
    public decimal OrderSuccessRate { get; init; }
    public decimal CancellationRate { get; init; }
    public DashboardDateRangeInfo DateRange { get; init; } = default!;
}

public sealed class OrderStatusCountDto
{
    public string Status { get; init; } = default!;
    public int Count { get; init; }
    public decimal Percentage { get; init; }
}

public sealed class OrderPeriodCountDto
{
    public DateTime Period { get; init; }
    public int Count { get; init; }
}
