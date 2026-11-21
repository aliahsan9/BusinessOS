using BusinessOS.Application.Features.Activities.DTOs;
using BusinessOS.Application.Features.Experience;
using BusinessOS.Application.Features.Finance.DTOs;

namespace BusinessOS.Application.Features.Dashboard.DTOs;

/// <summary>
/// Consolidated owner-home payload. Prefer this over fanning out many dashboard calls.
/// Revenue is always completed-order recognized sales unless a field name says otherwise.
/// </summary>
public sealed class OwnerDashboardSummaryResponse
{
    public string Currency { get; init; } = "USD";
    public DashboardDateRangeInfo DateRange { get; init; } = default!;
    public DashboardDateRangeInfo PreviousDateRange { get; init; } = default!;

    public DashboardOverviewResponse Overview { get; init; } = default!;
    public SalesAnalyticsResponse? Sales { get; init; }
    public CustomerAnalyticsDashboardResponse? Customers { get; init; }
    public ProductAnalyticsResponse? Products { get; init; }
    public InventoryAnalyticsDashboardResponse? Inventory { get; init; }
    public OrderAnalyticsResponse? Orders { get; init; }
    public ChartDataResponse? RevenueChart { get; init; }
    public FinancialDashboardResponse? Finance { get; init; }

    public IReadOnlyList<BusinessInsightDto> Insights { get; init; } = [];
    public IReadOnlyList<DashboardPriorityDto> Priorities { get; init; } = [];
    public IReadOnlyList<ActivityResponse> RecentActivity { get; init; } = [];
}

public sealed class DashboardPriorityDto
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    /// <summary>high | medium | low</summary>
    public string Priority { get; init; } = "medium";
    /// <summary>recommendation | task</summary>
    public string Source { get; init; } = "recommendation";
    public string? EntityLabel { get; init; }
    public string? Route { get; init; }
    public string? ActionLabel { get; init; }
    public DateTime? DueAt { get; init; }
}
