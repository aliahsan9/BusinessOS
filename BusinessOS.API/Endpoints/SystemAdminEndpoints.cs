using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Features.Billing.DTOs;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Application.Features.SystemAdmin.DTOs;
using BusinessOS.Application.Features.SystemAdmin.Services;
using BusinessOS.API.Authorization;

namespace BusinessOS.API.Endpoints;

public static class SystemAdminEndpoints
{
    public static void MapSystemAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/system")
            .WithTags("System Admin")
            .RequireAuthorization();

        group.MapGet("/health", GetHealth)
            .RequirePermission(PermissionCodes.SystemAdminView)
            .WithName("GetSystemHealth")
            .Produces<SystemHealthResponse>(StatusCodes.Status200OK);

        group.MapGet("/stats", GetStats)
            .RequirePermission(PermissionCodes.SystemAdminView)
            .WithName("GetSystemStats")
            .Produces<SystemStatsResponse>(StatusCodes.Status200OK);

        group.MapGet("/environment", GetEnvironmentInfo)
            .RequirePermission(PermissionCodes.SystemAdminView)
            .WithName("GetEnvironmentInfo")
            .Produces<EnvironmentInfoResponse>(StatusCodes.Status200OK);

        group.MapGet("/billing-metrics", GetBillingMetrics)
            .RequirePermission(PermissionCodes.SystemAdminView)
            .WithName("GetBillingMetrics")
            .Produces<BillingMetricsDto>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> GetHealth(
        ISystemAdminService systemAdminService,
        CancellationToken cancellationToken)
    {
        var result = await systemAdminService.GetHealthAsync(cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetStats(
        ISystemAdminService systemAdminService,
        CancellationToken cancellationToken)
    {
        var result = await systemAdminService.GetStatsAsync(cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetEnvironmentInfo(
        ISystemAdminService systemAdminService,
        CancellationToken cancellationToken)
    {
        var result = await systemAdminService.GetEnvironmentInfoAsync(cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetBillingMetrics(
        IBillingMetricsService billingMetricsService,
        CancellationToken cancellationToken) =>
        Results.Ok(await billingMetricsService.GetMetricsAsync(cancellationToken));
}
