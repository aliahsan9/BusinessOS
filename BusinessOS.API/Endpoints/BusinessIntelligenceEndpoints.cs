using BusinessOS.API.Authorization;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Features.BusinessIntelligence;

namespace BusinessOS.API.Endpoints;

public static class BusinessIntelligenceEndpoints
{
    public static void MapBusinessIntelligenceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/nalva/intelligence")
            .WithTags("Business Intelligence")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        group.MapGet("/dashboard", async (string? period, IBusinessIntelligenceService bi, CancellationToken ct) =>
            Results.Ok(await bi.GetExecutiveDashboardAsync(period, ct)))
            .RequirePermission(PermissionCodes.NalvaView)
            .WithName("GetExecutiveIntelligenceDashboard")
            .WithSummary("Executive intelligence dashboard from recorded tenant data");

        group.MapGet("/health", async (DateTime? startDate, DateTime? endDate, string? period, IBusinessIntelligenceService bi, CancellationToken ct) =>
            Results.Ok(await bi.GetHealthOverviewAsync(startDate, endDate, period, ct)))
            .RequirePermission(PermissionCodes.NalvaView);

        group.MapGet("/revenue-forecast", async (int? horizonMonths, IBusinessIntelligenceService bi, CancellationToken ct) =>
            Results.Ok(await bi.ForecastRevenueAsync(horizonMonths ?? 1, ct)))
            .RequirePermission(PermissionCodes.NalvaView);

        group.MapGet("/period-comparison", async (DateTime? startDate, DateTime? endDate, string? period, IBusinessIntelligenceService bi, CancellationToken ct) =>
            Results.Ok(await bi.ComparePeriodsAsync(startDate, endDate, period, ct)))
            .RequirePermission(PermissionCodes.NalvaView);

        group.MapGet("/risks", async (IBusinessIntelligenceService bi, CancellationToken ct) =>
            Results.Ok(await bi.GetRisksAsync(ct)))
            .RequirePermission(PermissionCodes.NalvaView);

        group.MapGet("/recommendations", async (IBusinessIntelligenceService bi, CancellationToken ct) =>
            Results.Ok(await bi.GetRecommendationsAsync(ct)))
            .RequirePermission(PermissionCodes.NalvaView);

        group.MapGet("/sales", async (DateTime? startDate, DateTime? endDate, string? period, ISalesIntelligenceService sales, CancellationToken ct) =>
            Results.Ok(await sales.GetIntelligenceAsync(startDate, endDate, period, ct)))
            .RequirePermission(PermissionCodes.NalvaView);

        group.MapGet("/sales/products", async (ISalesIntelligenceService sales, CancellationToken ct) =>
            Results.Ok(await sales.GetProductPerformanceAsync(ct)))
            .RequirePermission(PermissionCodes.NalvaView);

        group.MapGet("/sales/declining", async (ISalesIntelligenceService sales, CancellationToken ct) =>
            Results.Ok(await sales.GetDecliningProductsAsync(ct)))
            .RequirePermission(PermissionCodes.NalvaView);

        group.MapGet("/sales/conversion", async (ISalesIntelligenceService sales, CancellationToken ct) =>
            Results.Ok(await sales.GetConversionAsync(ct)))
            .RequirePermission(PermissionCodes.NalvaView);

        group.MapGet("/customers", async (ICustomerIntelligenceService customers, CancellationToken ct) =>
            Results.Ok(await customers.GetIntelligenceAsync(ct)))
            .RequirePermission(PermissionCodes.NalvaView);

        group.MapGet("/customers/churn", async (ICustomerIntelligenceService customers, CancellationToken ct) =>
            Results.Ok(await customers.GetChurnRisksAsync(ct)))
            .RequirePermission(PermissionCodes.NalvaView);

        group.MapGet("/customers/segments", async (ICustomerIntelligenceService customers, CancellationToken ct) =>
            Results.Ok(await customers.GetBehavioralSegmentsAsync(ct)))
            .RequirePermission(PermissionCodes.NalvaView);

        group.MapGet("/prediction-accuracy", async (IPredictionAccuracyService accuracy, CancellationToken ct) =>
            Results.Ok(await accuracy.GetAccuracySummaryAsync(ct)))
            .RequirePermission(PermissionCodes.NalvaView);

        group.MapGet("/ask", async (string? q, IExecutiveIntelligenceService exec, CancellationToken ct) =>
            Results.Ok(await exec.AnswerAsync(q ?? "How is my business performing?", ct)))
            .RequirePermission(PermissionCodes.NalvaView)
            .WithName("AskExecutiveIntelligence")
            .WithSummary("Data-backed decision brief for common owner questions");

        group.MapPost("/ask", async (ExecutiveAskRequest request, IExecutiveIntelligenceService exec, CancellationToken ct) =>
            Results.Ok(await exec.AnswerAsync(request.Question ?? "", ct)))
            .RequirePermission(PermissionCodes.NalvaView);

        group.MapPost("/tasks", async (CreateRecommendedTaskRequest request, IExecutiveIntelligenceService exec, CancellationToken ct) =>
            Results.Ok(await exec.CreateRecommendedTaskAsync(request, ct)))
            .RequirePermission(PermissionCodes.NalvaManage);
    }
}

public sealed class ExecutiveAskRequest
{
    public string? Question { get; set; }
}
