using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Features.Analytics.DTOs;
using BusinessOS.Application.Features.Analytics.Services;
using BusinessOS.Application.Features.Dashboard.DTOs;
using BusinessOS.API.Authorization;

namespace BusinessOS.API.Endpoints;

/// <summary>
/// Advanced business analytics endpoints for the dedicated analytics dashboard.
/// </summary>
public static class AnalyticsEndpoints
{
    public static void MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/analytics")
            .WithTags("Analytics")
            .RequireAuthorization();

        group.MapGet("/overview", GetOverview)
            .RequirePermission(PermissionCodes.ReportView)
            .WithName("GetAnalyticsOverview")
            .WithSummary("Get analytics KPI overview")
            .Produces<AnalyticsOverviewResponse>(StatusCodes.Status200OK);

        group.MapGet("/revenue", GetRevenue)
            .RequirePermission(PermissionCodes.ReportView)
            .WithName("GetAnalyticsRevenue")
            .WithSummary("Get monthly revenue chart data")
            .Produces<ChartDataResponse>(StatusCodes.Status200OK);

        group.MapGet("/expenses", GetExpenses)
            .RequirePermission(PermissionCodes.ReportView)
            .WithName("GetAnalyticsExpenses")
            .WithSummary("Get monthly expense chart data")
            .Produces<ChartDataResponse>(StatusCodes.Status200OK);

        group.MapGet("/profit", GetProfit)
            .RequirePermission(PermissionCodes.ReportView)
            .WithName("GetAnalyticsProfit")
            .WithSummary("Get profit chart data")
            .Produces<ChartDataResponse>(StatusCodes.Status200OK);

        group.MapGet("/customers", GetCustomers)
            .RequirePermission(PermissionCodes.ReportView)
            .WithName("GetAnalyticsCustomers")
            .WithSummary("Get customer growth chart data")
            .Produces<ChartDataResponse>(StatusCodes.Status200OK);

        group.MapGet("/projects", GetProjects)
            .RequirePermission(PermissionCodes.ReportView)
            .WithName("GetAnalyticsProjects")
            .WithSummary("Get project status analytics")
            .Produces<AnalyticsProjectAnalyticsResponse>(StatusCodes.Status200OK);

        group.MapGet("/tasks", GetTasks)
            .RequirePermission(PermissionCodes.ReportView)
            .WithName("GetAnalyticsTasks")
            .WithSummary("Get task analytics")
            .Produces<AnalyticsTaskAnalyticsResponse>(StatusCodes.Status200OK);

        group.MapGet("/top-customers", GetTopCustomers)
            .RequirePermission(PermissionCodes.ReportView)
            .WithName("GetAnalyticsTopCustomers")
            .WithSummary("Get top customers by revenue")
            .Produces<AnalyticsTopCustomersResponse>(StatusCodes.Status200OK);

        group.MapGet("/recent-activity", GetRecentActivity)
            .RequirePermission(PermissionCodes.ReportView)
            .WithName("GetAnalyticsRecentActivity")
            .WithSummary("Get recent business activity feed")
            .Produces<AnalyticsRecentActivityResponse>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> GetOverview(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        IAnalyticsModuleService analyticsService,
        CancellationToken cancellationToken)
    {
        var result = await analyticsService.GetOverviewAsync(startDate, endDate, period, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetRevenue(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        IAnalyticsModuleService analyticsService,
        CancellationToken cancellationToken)
    {
        var result = await analyticsService.GetRevenueChartAsync(startDate, endDate, period, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetExpenses(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        IAnalyticsModuleService analyticsService,
        CancellationToken cancellationToken)
    {
        var result = await analyticsService.GetExpenseChartAsync(startDate, endDate, period, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetProfit(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        IAnalyticsModuleService analyticsService,
        CancellationToken cancellationToken)
    {
        var result = await analyticsService.GetProfitChartAsync(startDate, endDate, period, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetCustomers(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        IAnalyticsModuleService analyticsService,
        CancellationToken cancellationToken)
    {
        var result = await analyticsService.GetCustomerGrowthChartAsync(startDate, endDate, period, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetProjects(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        IAnalyticsModuleService analyticsService,
        CancellationToken cancellationToken)
    {
        var result = await analyticsService.GetProjectAnalyticsAsync(startDate, endDate, period, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetTasks(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        IAnalyticsModuleService analyticsService,
        CancellationToken cancellationToken)
    {
        var result = await analyticsService.GetTaskAnalyticsAsync(startDate, endDate, period, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetTopCustomers(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        int top = 10,
        IAnalyticsModuleService analyticsService = default!,
        CancellationToken cancellationToken = default)
    {
        var result = await analyticsService.GetTopCustomersAsync(
            startDate, endDate, period, top, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetRecentActivity(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        int limit = 20,
        IAnalyticsModuleService analyticsService = default!,
        CancellationToken cancellationToken = default)
    {
        var result = await analyticsService.GetRecentActivityAsync(
            startDate, endDate, period, limit, cancellationToken);
        return Results.Ok(result);
    }
}
