using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Activities.Services;
using BusinessOS.Application.Features.Dashboard.DTOs;
using BusinessOS.Application.Features.Dashboard.Services;
using BusinessOS.Application.Features.Dashboard.Validators;
using BusinessOS.Application.Features.Experience;
using BusinessOS.Application.Features.Finance.Services;
using MediatR;

namespace BusinessOS.Application.Features.Dashboard.Queries.GetOwnerDashboardSummary;

public sealed record GetOwnerDashboardSummaryQuery(
    DateTime? StartDate,
    DateTime? EndDate,
    string? Period,
    int Top = 10) : DashboardDateRangeQuery(StartDate, EndDate, Period),
    IRequest<OwnerDashboardSummaryResponse>;

public sealed class GetOwnerDashboardSummaryQueryHandler
    : IRequestHandler<GetOwnerDashboardSummaryQuery, OwnerDashboardSummaryResponse>
{
    private readonly IDashboardDateRangeResolver _dateRangeResolver;
    private readonly IDashboardService _dashboardService;
    private readonly IAnalyticsService _analyticsService;
    private readonly IReportingService _reportingService;
    private readonly IFinanceService _financeService;
    private readonly IExperienceService _experienceService;
    private readonly IActivityService _activityService;
    private readonly ICurrentUserService _user;
    private readonly IDashboardCacheService _cacheService;

    public GetOwnerDashboardSummaryQueryHandler(
        IDashboardDateRangeResolver dateRangeResolver,
        IDashboardService dashboardService,
        IAnalyticsService analyticsService,
        IReportingService reportingService,
        IFinanceService financeService,
        IExperienceService experienceService,
        IActivityService activityService,
        ICurrentUserService user,
        IDashboardCacheService cacheService)
    {
        _dateRangeResolver = dateRangeResolver;
        _dashboardService = dashboardService;
        _analyticsService = analyticsService;
        _reportingService = reportingService;
        _financeService = financeService;
        _experienceService = experienceService;
        _activityService = activityService;
        _user = user;
        _cacheService = cacheService;
    }

    public Task<OwnerDashboardSummaryResponse> Handle(
        GetOwnerDashboardSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var dateRange = _dateRangeResolver.Resolve(request.StartDate, request.EndDate, request.Period);
        var top = request.Top is 20 ? 20 : 10;

        return _cacheService.GetOrCreateAsync(
            $"owner-summary:{dateRange.CacheKeySuffix}:top{top}",
            ct => BuildAsync(dateRange, top, ct),
            cancellationToken);
    }

    private async Task<OwnerDashboardSummaryResponse> BuildAsync(
        Models.DashboardDateRange dateRange,
        int top,
        CancellationToken cancellationToken)
    {
        var overview = await _dashboardService.GetOverviewAsync(dateRange, cancellationToken);

        SalesAnalyticsResponse? sales = null;
        CustomerAnalyticsDashboardResponse? customers = null;
        ProductAnalyticsResponse? products = null;
        InventoryAnalyticsDashboardResponse? inventory = null;
        OrderAnalyticsResponse? orders = null;
        ChartDataResponse? revenueChart = null;
        Finance.DTOs.FinancialDashboardResponse? finance = null;

        if (_user.HasPermission(PermissionCodes.OrderView))
        {
            sales = await _analyticsService.GetSalesAnalyticsAsync(dateRange, cancellationToken);
            orders = await _analyticsService.GetOrderAnalyticsAsync(dateRange, cancellationToken);
            revenueChart = await _reportingService.GetChartDataAsync(ChartTypes.Revenue, dateRange, top, cancellationToken);
        }

        if (_user.HasPermission(PermissionCodes.CustomerView))
            customers = await _analyticsService.GetCustomerAnalyticsAsync(dateRange, cancellationToken);

        if (_user.HasPermission(PermissionCodes.ProductView))
            products = await _analyticsService.GetProductAnalyticsAsync(dateRange, top, cancellationToken);

        if (_user.HasPermission(PermissionCodes.InventoryView))
            inventory = await _analyticsService.GetInventoryAnalyticsAsync(dateRange, cancellationToken);

        if (_user.HasPermission(PermissionCodes.FinanceView))
        {
            finance = await _financeService.GetDashboardAsync(
                dateRange.StartDate, dateRange.EndDate, dateRange.Period, cancellationToken);
        }

        IReadOnlyList<BusinessInsightDto> insights = [];
        try
        {
            var experience = await _experienceService.GetDashboardAsync(cancellationToken);
            insights = experience.Insights;
        }
        catch
        {
            // Insights are additive; summary still succeeds without them.
        }

        IReadOnlyList<Activities.DTOs.ActivityResponse> activity = [];
        if (_user.HasPermission(PermissionCodes.ActivityView))
        {
            try
            {
                activity = DeduplicateActivity(
                    await _activityService.GetRecentAsync(12, cancellationToken));
            }
            catch
            {
                activity = [];
            }
        }

        var priorities = BuildPriorities(overview, orders, finance, insights);

        return new OwnerDashboardSummaryResponse
        {
            Currency = overview.Currency,
            DateRange = overview.DateRange,
            PreviousDateRange = overview.PreviousDateRange,
            Overview = overview,
            Sales = sales,
            Customers = customers,
            Products = products,
            Inventory = inventory,
            Orders = orders,
            RevenueChart = revenueChart,
            Finance = finance,
            Insights = insights,
            Priorities = priorities,
            RecentActivity = activity
        };
    }

    private static IReadOnlyList<DashboardPriorityDto> BuildPriorities(
        DashboardOverviewResponse overview,
        OrderAnalyticsResponse? orders,
        Finance.DTOs.FinancialDashboardResponse? finance,
        IReadOnlyList<BusinessInsightDto> insights)
    {
        var list = new List<DashboardPriorityDto>();

        if (overview.LowStockProducts > 0)
        {
            list.Add(new DashboardPriorityDto
            {
                Id = "low-stock",
                Title = "Reorder low-stock products",
                Description = overview.LowStockProducts == 1
                    ? "1 product is at or below its reorder level."
                    : $"{overview.LowStockProducts} products are at or below their reorder levels.",
                Priority = "high",
                Source = "recommendation",
                EntityLabel = "Inventory",
                Route = "/inventory",
                ActionLabel = "View products"
            });
        }

        if (overview.OutOfStockProducts > 0)
        {
            list.Add(new DashboardPriorityDto
            {
                Id = "out-of-stock",
                Title = "Restock out-of-stock items",
                Description = overview.OutOfStockProducts == 1
                    ? "1 product has zero stock."
                    : $"{overview.OutOfStockProducts} products have zero stock.",
                Priority = "high",
                Source = "recommendation",
                EntityLabel = "Inventory",
                Route = "/inventory",
                ActionLabel = "View inventory"
            });
        }

        var pending = orders?.OrdersByStatus
            ?.FirstOrDefault(x => string.Equals(x.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            ?.Count ?? 0;
        if (pending > 0)
        {
            list.Add(new DashboardPriorityDto
            {
                Id = "pending-orders",
                Title = "Review pending orders",
                Description = pending == 1
                    ? "1 order is waiting to be processed."
                    : $"{pending} orders are waiting to be processed.",
                Priority = "medium",
                Source = "recommendation",
                EntityLabel = "Orders",
                Route = "/orders",
                ActionLabel = "Open orders"
            });
        }

        if (finance is { OutstandingInvoices: > 0 })
        {
            list.Add(new DashboardPriorityDto
            {
                Id = "outstanding",
                Title = "Follow up on outstanding payments",
                Description = $"Receivables totaling {finance.OutstandingInvoices:0.##} {overview.Currency} remain unpaid.",
                Priority = "high",
                Source = "recommendation",
                EntityLabel = "Finance",
                Route = "/invoices",
                ActionLabel = "View invoices"
            });
        }

        // Avoid duplicating insight-driven priorities already covered above.
        foreach (var insight in insights)
        {
            if (list.Count >= 6) break;
            if (insight.Code is "inventory-alert" or "outstanding-payments" or "top-product-stock")
                continue;
            if (list.Any(x => x.Id == insight.Code)) continue;
            if (string.IsNullOrWhiteSpace(insight.RecommendedAction)) continue;

            list.Add(new DashboardPriorityDto
            {
                Id = insight.Code,
                Title = string.IsNullOrWhiteSpace(insight.Title) ? insight.RecommendedAction! : insight.Title,
                Description = insight.Message,
                Priority = insight.Severity is "critical" or "warning" ? "high" : "medium",
                Source = "recommendation",
                Route = insight.Route,
                ActionLabel = insight.RecommendedAction
            });
        }

        return list;
    }

    private static IReadOnlyList<Activities.DTOs.ActivityResponse> DeduplicateActivity(
        IReadOnlyList<Activities.DTOs.ActivityResponse> items)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<Activities.DTOs.ActivityResponse>();
        foreach (var item in items)
        {
            var key = $"{item.EntityType}|{item.EntityId}|{item.Action}|{item.Description}";
            if (!seen.Add(key)) continue;
            result.Add(item);
            if (result.Count >= 8) break;
        }
        return result;
    }
}
