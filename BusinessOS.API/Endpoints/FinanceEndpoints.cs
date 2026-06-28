using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Features.Finance.DTOs;
using BusinessOS.Application.Features.Finance.Services;
using BusinessOS.API.Authorization;

namespace BusinessOS.API.Endpoints;

public static class FinanceEndpoints
{
    public static void MapFinanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/finance")
            .WithTags("Finance")
            .RequireAuthorization();

        group.MapGet("/dashboard", GetDashboard)
            .RequirePermission(PermissionCodes.FinanceView)
            .WithName("GetFinanceDashboard")
            .Produces<FinancialDashboardResponse>(StatusCodes.Status200OK);

        group.MapGet("/profit-loss", GetProfitLoss)
            .RequirePermission(PermissionCodes.FinanceView)
            .WithName("GetProfitLoss")
            .Produces<ProfitLossResponse>(StatusCodes.Status200OK);

        group.MapGet("/revenue-breakdown", GetRevenueBreakdown)
            .RequirePermission(PermissionCodes.FinanceView)
            .WithName("GetRevenueBreakdown")
            .Produces<RevenueBreakdown>(StatusCodes.Status200OK);

        group.MapGet("/expense-breakdown", GetExpenseBreakdown)
            .RequirePermission(PermissionCodes.FinanceView)
            .WithName("GetExpenseBreakdown")
            .Produces<ExpenseBreakdown>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> GetDashboard(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        IFinanceService financeService,
        CancellationToken cancellationToken)
    {
        var result = await financeService.GetDashboardAsync(startDate, endDate, period, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetProfitLoss(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        string? groupBy,
        IFinanceService financeService,
        CancellationToken cancellationToken)
    {
        var result = await financeService.GetProfitLossAsync(
            startDate, endDate, period, groupBy, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetRevenueBreakdown(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        IFinanceService financeService,
        CancellationToken cancellationToken)
    {
        var result = await financeService.GetRevenueBreakdownAsync(
            startDate, endDate, period, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetExpenseBreakdown(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        IFinanceService financeService,
        CancellationToken cancellationToken)
    {
        var result = await financeService.GetExpenseBreakdownAsync(
            startDate, endDate, period, cancellationToken);
        return Results.Ok(result);
    }
}
