using BusinessOS.Application.Features.Dashboard.DTOs;

namespace BusinessOS.Application.Features.Analytics.DTOs;

/// <summary>KPI metric with period-over-period growth.</summary>
public sealed class AnalyticsMetricDto
{
    public decimal Value { get; init; }
    public decimal PreviousValue { get; init; }
    public decimal GrowthPercentage { get; init; }
}

/// <summary>Executive analytics overview with 8 KPI cards.</summary>
public sealed class AnalyticsOverviewResponse
{
    public AnalyticsMetricDto TotalCustomers { get; init; } = default!;
    public AnalyticsMetricDto ActiveProjects { get; init; } = default!;
    public AnalyticsMetricDto TotalTasks { get; init; } = default!;
    public AnalyticsMetricDto CompletedTasks { get; init; } = default!;
    public AnalyticsMetricDto TotalRevenue { get; init; } = default!;
    public AnalyticsMetricDto TotalExpenses { get; init; } = default!;
    public AnalyticsMetricDto NetProfit { get; init; } = default!;
    public AnalyticsMetricDto TotalInvoices { get; init; } = default!;
    public DashboardDateRangeInfo DateRange { get; init; } = default!;
}

/// <summary>Top customer ranked by invoice revenue.</summary>
public sealed class AnalyticsTopCustomerDto
{
    public Guid CustomerId { get; init; }
    public string CustomerName { get; init; } = default!;
    public decimal RevenueGenerated { get; init; }
    public int ProjectsCount { get; init; }
    public int InvoicesCount { get; init; }
}

public sealed class AnalyticsTopCustomersResponse
{
    public IReadOnlyList<AnalyticsTopCustomerDto> Customers { get; init; } = [];
    public DashboardDateRangeInfo DateRange { get; init; } = default!;
}

/// <summary>Recent business activity feed item.</summary>
public sealed class AnalyticsRecentActivityDto
{
    public Guid Id { get; init; }
    public string ActivityType { get; init; } = default!;
    public string Title { get; init; } = default!;
    public string Description { get; init; } = default!;
    public DateTime OccurredAt { get; init; }
}

public sealed class AnalyticsRecentActivityResponse
{
    public IReadOnlyList<AnalyticsRecentActivityDto> Activities { get; init; } = [];
    public DashboardDateRangeInfo DateRange { get; init; } = default!;
}

/// <summary>Task distribution (order line items mapped as tasks).</summary>
public sealed class AnalyticsTaskBreakdownDto
{
    public int Total { get; init; }
    public int Completed { get; init; }
    public int Pending { get; init; }
    public int Overdue { get; init; }
}

public sealed class AnalyticsTaskAnalyticsResponse
{
    public AnalyticsTaskBreakdownDto Breakdown { get; init; } = default!;
    public ChartDataResponse Chart { get; init; } = default!;
    public DashboardDateRangeInfo DateRange { get; init; } = default!;
}

/// <summary>Project (order) status distribution.</summary>
public sealed class AnalyticsProjectStatusDto
{
    public int Active { get; init; }
    public int Completed { get; init; }
    public int OnHold { get; init; }
    public int Cancelled { get; init; }
}

public sealed class AnalyticsProjectAnalyticsResponse
{
    public AnalyticsProjectStatusDto StatusBreakdown { get; init; } = default!;
    public ChartDataResponse Chart { get; init; } = default!;
    public DashboardDateRangeInfo DateRange { get; init; } = default!;
}
