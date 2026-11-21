namespace BusinessOS.Application.Features.Dashboard.DTOs;

/// <summary>Executive dashboard KPI snapshot.</summary>
public sealed class DashboardOverviewResponse
{
    /// <summary>Catalog product count (not period-filtered).</summary>
    public int TotalProducts { get; init; }
    /// <summary>Catalog category count (not period-filtered).</summary>
    public int TotalCategories { get; init; }
    /// <summary>Catalog customer count (not period-filtered).</summary>
    public int TotalCustomers { get; init; }
    /// <summary>Customers created within the selected period.</summary>
    public int NewCustomers { get; init; }
    /// <summary>Distinct customers with a completed order in the selected period.</summary>
    public int ActiveCustomers { get; init; }
    public int TotalOrders { get; init; }
    /// <summary>Completed-order revenue in the selected period (recognized sales).</summary>
    public decimal TotalRevenue { get; init; }
    public decimal PreviousRevenue { get; init; }
    /// <summary>Percent change vs previous equivalent period; null when previous is zero.</summary>
    public decimal? RevenueChangePercent { get; init; }
    public int PreviousOrders { get; init; }
    public decimal? OrdersChangePercent { get; init; }
    public decimal TotalInventoryValue { get; init; }
    public int TotalActiveUsers { get; init; }
    public int LowStockProducts { get; init; }
    public int OutOfStockProducts { get; init; }
    /// <summary>Tenant currency code (e.g. PKR, USD).</summary>
    public string Currency { get; init; } = "USD";
    public DashboardDateRangeInfo DateRange { get; init; } = default!;
    public DashboardDateRangeInfo PreviousDateRange { get; init; } = default!;
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
    /// <summary>UI alias for CustomerLifetimeValue.</summary>
    public decimal AverageLifetimeValue => CustomerLifetimeValue;
    public decimal AverageCustomerSpending { get; init; }
    /// <summary>UI alias for AverageCustomerSpending.</summary>
    public decimal AverageSpending => AverageCustomerSpending;
    public decimal CustomerGrowthRate { get; init; }
    public IReadOnlyList<TopCustomerDto> TopCustomers { get; init; } = [];
    public DashboardDateRangeInfo DateRange { get; init; } = default!;
}

public sealed class TopCustomerDto
{
    public Guid CustomerId { get; init; }
    public string CustomerName { get; init; } = default!;
    /// <summary>UI alias for CustomerName.</summary>
    public string FullName => CustomerName;
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
    /// <summary>UI alias for ProductRevenue.</summary>
    public IReadOnlyList<ProductPerformanceDto> HighestRevenueProducts => ProductRevenue;
    public IReadOnlyList<ProductPerformanceDto> ProductPerformanceRanking { get; init; } = [];
    /// <summary>UI alias for ProductPerformanceRanking.</summary>
    public IReadOnlyList<ProductPerformanceDto> ProductRankings => ProductPerformanceRanking;
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

    /// <summary>UI alias for QuantitySold (legacy frontend contract).</summary>
    public decimal TotalQuantitySold => QuantitySold;

    /// <summary>UI alias for Revenue (legacy frontend contract).</summary>
    public decimal TotalRevenue => Revenue;

    /// <summary>Legacy stock field; product analytics does not load inventory here.</summary>
    public int CurrentStock { get; init; }
}

public sealed class InventoryAnalyticsDashboardResponse
{
    public decimal InventoryValue { get; init; }
    /// <summary>UI alias for InventoryValue.</summary>
    public decimal TotalInventoryValue => InventoryValue;
    public decimal TotalStockQuantity { get; init; }
    public int LowStockProducts { get; init; }
    /// <summary>UI alias for LowStockProducts.</summary>
    public int LowStockCount => LowStockProducts;
    public int OutOfStockProducts { get; init; }
    /// <summary>UI alias for OutOfStockProducts.</summary>
    public int OutOfStockCount => OutOfStockProducts;
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
    public bool IsOutOfStock => string.Equals(StockStatus, "OutOfStock", StringComparison.OrdinalIgnoreCase)
        || CurrentStock <= 0;
    public bool IsLowStock => !IsOutOfStock && (
        string.Equals(StockStatus, "LowStock", StringComparison.OrdinalIgnoreCase)
        || (ReorderLevel > 0 && CurrentStock <= ReorderLevel));
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
    public decimal Inbound => TotalIn;
    public decimal Outbound => TotalOut;
}

public sealed class OrderAnalyticsResponse
{
    public IReadOnlyList<OrderStatusCountDto> OrdersByStatus { get; init; } = [];
    public IReadOnlyList<OrderPeriodCountDto> OrdersPerDay { get; init; } = [];
    public IReadOnlyList<OrderPeriodCountDto> OrdersPerMonth { get; init; } = [];
    public decimal OrderSuccessRate { get; init; }
    /// <summary>UI alias for OrderSuccessRate.</summary>
    public decimal SuccessRate => OrderSuccessRate;
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
