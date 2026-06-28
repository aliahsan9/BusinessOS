using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Features.Dashboard.DTOs;
using BusinessOS.Application.Features.Dashboard.Queries.GetChartData;
using BusinessOS.Application.Features.Dashboard.Queries.GetCustomerAnalytics;
using BusinessOS.Application.Features.Dashboard.Queries.GetDashboardOverview;
using BusinessOS.Application.Features.Dashboard.Queries.GetInventoryAnalytics;
using BusinessOS.Application.Features.Dashboard.Queries.GetOrderAnalytics;
using BusinessOS.Application.Features.Dashboard.Queries.GetProductAnalytics;
using BusinessOS.Application.Features.Dashboard.Queries.GetSalesAnalytics;
using BusinessOS.API.Authorization;
using MediatR;

namespace BusinessOS.API.Endpoints;

/// <summary>
/// Executive dashboard, analytics, and chart reporting endpoints.
/// </summary>
public static class DashboardEndpoints
{
    /// <summary>
    /// Maps dashboard endpoints under <c>/api/dashboard</c>.
    /// </summary>
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard")
            .WithTags("Dashboard")
            .RequireAuthorization();

        group.MapGet("/overview", GetOverview)
            .RequirePermission(PermissionCodes.OrderView)
            .WithName("GetDashboardOverview")
            .WithSummary("Get executive dashboard overview")
            .WithDescription(
                "Returns high-level KPIs: products, categories, customers, orders, revenue, inventory value, " +
                "active users, and stock alerts. Supports ?period=today|week|month|year|all|custom with optional startDate/endDate.")
            .Produces<DashboardOverviewResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/sales", GetSales)
            .RequirePermission(PermissionCodes.OrderView)
            .WithName("GetSalesAnalytics")
            .WithSummary("Get sales analytics")
            .WithDescription(
                "Returns today/weekly/monthly/yearly sales, revenue trends, average order value, and order completion metrics.")
            .Produces<SalesAnalyticsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/customers", GetCustomers)
            .RequirePermission(PermissionCodes.CustomerView)
            .WithName("GetCustomerAnalyticsDashboard")
            .WithSummary("Get customer analytics")
            .WithDescription(
                "Returns customer totals, growth, lifetime value, average spending, and top customers.")
            .Produces<CustomerAnalyticsDashboardResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/products", GetProducts)
            .RequirePermission(PermissionCodes.ProductView)
            .WithName("GetProductAnalyticsDashboard")
            .WithSummary("Get product analytics")
            .WithDescription(
                "Returns best/worst selling products, revenue ranking, and performance metrics. Use ?top=10 or ?top=20.")
            .Produces<ProductAnalyticsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/inventory", GetInventory)
            .RequirePermission(PermissionCodes.InventoryView)
            .WithName("GetInventoryAnalyticsDashboard")
            .WithSummary("Get inventory analytics")
            .WithDescription(
                "Returns inventory value, stock levels, low/out-of-stock counts, reorder recommendations, and movement trends.")
            .Produces<InventoryAnalyticsDashboardResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/orders", GetOrders)
            .RequirePermission(PermissionCodes.OrderView)
            .WithName("GetOrderAnalytics")
            .WithSummary("Get order analytics")
            .WithDescription(
                "Returns orders by status, daily/monthly order counts, success rate, and cancellation rate.")
            .Produces<OrderAnalyticsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        var charts = group.MapGroup("/charts");

        charts.MapGet("/revenue", GetRevenueChart)
            .RequirePermission(PermissionCodes.OrderView)
            .WithName("GetRevenueChart")
            .WithSummary("Get revenue chart data")
            .WithDescription("Returns line/bar chart datasets for revenue and order trends.")
            .Produces<ChartDataResponse>(StatusCodes.Status200OK);

        charts.MapGet("/orders", GetOrdersChart)
            .RequirePermission(PermissionCodes.OrderView)
            .WithName("GetOrdersChart")
            .WithSummary("Get orders chart data")
            .WithDescription("Returns bar/doughnut chart datasets for order status distribution.")
            .Produces<ChartDataResponse>(StatusCodes.Status200OK);

        charts.MapGet("/customers", GetCustomersChart)
            .RequirePermission(PermissionCodes.CustomerView)
            .WithName("GetCustomersChart")
            .WithSummary("Get customers chart data")
            .WithDescription("Returns line/bar chart datasets for customer growth and top spenders.")
            .Produces<ChartDataResponse>(StatusCodes.Status200OK);

        charts.MapGet("/products", GetProductsChart)
            .RequirePermission(PermissionCodes.ProductView)
            .WithName("GetProductsChart")
            .WithSummary("Get products chart data")
            .WithDescription("Returns bar/line chart datasets for top product revenue and quantity sold.")
            .Produces<ChartDataResponse>(StatusCodes.Status200OK);

        charts.MapGet("/inventory", GetInventoryChart)
            .RequirePermission(PermissionCodes.InventoryView)
            .WithName("GetInventoryChart")
            .WithSummary("Get inventory chart data")
            .WithDescription("Returns pie/line chart datasets for stock status and movement trends.")
            .Produces<ChartDataResponse>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> GetOverview(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetDashboardOverviewQuery(startDate, endDate, period),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetSales(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetSalesAnalyticsQuery(startDate, endDate, period),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetCustomers(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetCustomerAnalyticsDashboardQuery(startDate, endDate, period),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetProducts(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        int top = 10,
        ISender sender = default!,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetProductAnalyticsDashboardQuery(startDate, endDate, period, top),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetInventory(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetInventoryAnalyticsDashboardQuery(startDate, endDate, period),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetOrders(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetOrderAnalyticsQuery(startDate, endDate, period),
            cancellationToken);

        return Results.Ok(result);
    }

    private static Task<IResult> GetRevenueChart(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        int top = 10,
        ISender sender = default!,
        CancellationToken cancellationToken = default) =>
        GetChart("revenue", startDate, endDate, period, top, sender, cancellationToken);

    private static Task<IResult> GetOrdersChart(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        int top = 10,
        ISender sender = default!,
        CancellationToken cancellationToken = default) =>
        GetChart("orders", startDate, endDate, period, top, sender, cancellationToken);

    private static Task<IResult> GetCustomersChart(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        int top = 10,
        ISender sender = default!,
        CancellationToken cancellationToken = default) =>
        GetChart("customers", startDate, endDate, period, top, sender, cancellationToken);

    private static Task<IResult> GetProductsChart(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        int top = 10,
        ISender sender = default!,
        CancellationToken cancellationToken = default) =>
        GetChart("products", startDate, endDate, period, top, sender, cancellationToken);

    private static Task<IResult> GetInventoryChart(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        int top = 10,
        ISender sender = default!,
        CancellationToken cancellationToken = default) =>
        GetChart("inventory", startDate, endDate, period, top, sender, cancellationToken);

    private static async Task<IResult> GetChart(
        string chartType,
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        int top,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetChartDataQuery(chartType, startDate, endDate, period, top),
            cancellationToken);

        return Results.Ok(result);
    }
}
