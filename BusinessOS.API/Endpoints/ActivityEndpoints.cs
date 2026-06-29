using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Activities.DTOs;
using BusinessOS.Application.Features.Activities.Services;
using BusinessOS.API.Authorization;

namespace BusinessOS.API.Endpoints;

public static class ActivityEndpoints
{
    public static void MapActivityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/activity")
            .WithTags("Activity")
            .RequireAuthorization();

        group.MapGet("", GetActivities)
            .RequirePermission(PermissionCodes.ActivityView)
            .WithName("GetActivities")
            .Produces<PagedResult<ActivityResponse>>(StatusCodes.Status200OK);

        group.MapGet("/recent", GetRecentActivities)
            .RequirePermission(PermissionCodes.ActivityView)
            .WithName("GetRecentActivities")
            .Produces<IReadOnlyList<ActivityResponse>>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> GetActivities(
        string? search,
        string? action,
        string? entityType,
        DateTime? dateFrom,
        DateTime? dateTo,
        int page,
        int pageSize,
        IActivityService activityService,
        CancellationToken cancellationToken)
    {
        var query = new ActivityQueryParams(
            page <= 0 ? 1 : page,
            pageSize <= 0 ? 20 : pageSize,
            search,
            action,
            entityType,
            dateFrom,
            dateTo);

        var result = await activityService.GetActivitiesAsync(query, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetRecentActivities(
        int limit,
        IActivityService activityService,
        CancellationToken cancellationToken)
    {
        var result = await activityService.GetRecentAsync(limit <= 0 ? 10 : limit, cancellationToken);
        return Results.Ok(result);
    }
}
