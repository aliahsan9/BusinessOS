using BusinessOS.API.Authorization;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Features.Marketing;

namespace BusinessOS.API.Endpoints;

public static class MarketingEndpoints
{
    public static void MapMarketingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/marketing").WithTags("Marketing").RequireAuthorization().RequireRateLimiting("api");

        group.MapGet("/overview", async (IMarketingOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.GetOverviewAsync(ct))).RequirePermission(PermissionCodes.NalvaView);

        group.MapGet("/brand", async (IMarketingOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.GetOrSeedBrandProfileAsync(ct))).RequirePermission(PermissionCodes.NalvaView);
        group.MapPut("/brand", async (UpsertBrandProfileRequest request, IMarketingOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.UpsertBrandProfileAsync(request, ct))).RequirePermission(PermissionCodes.NalvaManage);

        group.MapGet("/campaigns", async (string? status, IMarketingOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.ListCampaignsAsync(status, ct))).RequirePermission(PermissionCodes.NalvaView);
        group.MapGet("/campaigns/{id:guid}", async (Guid id, IMarketingOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.GetCampaignAsync(id, ct))).RequirePermission(PermissionCodes.NalvaView);
        group.MapPost("/campaigns", async (CreateCampaignRequest request, IMarketingOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.CreateCampaignAsync(request, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapPost("/campaigns/strategy", async (GenerateStrategyRequest request, IMarketingOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.GenerateStrategyAsync(request, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapPut("/campaigns/{id:guid}/strategy", async (Guid id, UpdateStrategyRequest request, IMarketingOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateStrategyAsync(id, request, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapPost("/campaigns/{id:guid}/strategy/approve", async (Guid id, IMarketingOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.ApproveStrategyAsync(id, ct))).RequirePermission(PermissionCodes.NalvaApprove);
        group.MapPost("/campaigns/{id:guid}/pause", async (Guid id, IMarketingOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.PauseResumeCampaignAsync(id, true, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapPost("/campaigns/{id:guid}/resume", async (Guid id, IMarketingOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.PauseResumeCampaignAsync(id, false, ct))).RequirePermission(PermissionCodes.NalvaManage);

        group.MapGet("/content", async (Guid? campaignId, string? status, IMarketingOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.ListContentAsync(campaignId, status, ct))).RequirePermission(PermissionCodes.NalvaView);
        group.MapGet("/content/{id:guid}", async (Guid id, IMarketingOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.GetContentAsync(id, ct))).RequirePermission(PermissionCodes.NalvaView);
        group.MapPost("/content", async (GenerateContentItemRequest request, IMarketingOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.GenerateContentItemAsync(request, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapPut("/content/{id:guid}", async (Guid id, UpdateContentItemRequest request, IMarketingOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateContentItemAsync(id, request, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapPost("/content/calendar", async (GenerateContentCalendarRequest request, IMarketingOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.GenerateContentCalendarAsync(request, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapGet("/calendar", async (DateTime from, DateTime to, string? view, IMarketingOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.GetCalendarAsync(from, to, view, ct))).RequirePermission(PermissionCodes.NalvaView);

        group.MapPost("/content/{id:guid}/submit", async (Guid id, IMarketingOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.SubmitForReviewAsync(id, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapPost("/content/{id:guid}/approve", async (Guid id, string? fingerprint, IMarketingOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.ApproveContentAsync(id, fingerprint, ct))).RequirePermission(PermissionCodes.NalvaApprove);
        group.MapPost("/content/{id:guid}/reject", async (Guid id, RejectContentRequest? request, IMarketingOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.RejectContentAsync(id, request ?? new RejectContentRequest(), ct))).RequirePermission(PermissionCodes.NalvaApprove);
        group.MapPost("/content/{id:guid}/schedule", async (Guid id, ScheduleContentRequest request, IMarketingOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.ScheduleApprovedContentAsync(id, request, ct))).RequirePermission(PermissionCodes.NalvaManage);

        group.MapGet("/assets", async (Guid? contentItemId, Guid? campaignId, IMarketingOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.ListAssetsAsync(contentItemId, campaignId, ct))).RequirePermission(PermissionCodes.NalvaView);
        group.MapPost("/assets/graphic", async (GenerateGraphicRequest request, IMarketingOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.GenerateGraphicAsync(request, ct))).RequirePermission(PermissionCodes.NalvaManage);

        group.MapGet("/research", async (string? kind, IMarketingOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.ListResearchAsync(kind, ct))).RequirePermission(PermissionCodes.NalvaView);
        group.MapPost("/research", async (CreateResearchReportRequest request, IMarketingOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.CreateResearchReportAsync(request, ct))).RequirePermission(PermissionCodes.NalvaManage);

        group.MapGet("/accounts", async (IMarketingOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.GetConnectedMarketingAccountsAsync(ct))).RequirePermission(PermissionCodes.NalvaView);
        group.MapPost("/analytics/sync", async (SyncAnalyticsRequest? request, IMarketingOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.SyncAnalyticsAsync(request ?? new SyncAnalyticsRequest(), ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapGet("/reports/performance", async (Guid? campaignId, IMarketingOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.GeneratePerformanceReportAsync(campaignId, ct))).RequirePermission(PermissionCodes.NalvaView);

        group.MapGet("/policy", async (IMarketingOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.GetPolicyAsync(ct))).RequirePermission(PermissionCodes.NalvaView);
        group.MapPut("/policy", async (MarketingPolicyWriteRequest request, IMarketingOperationsService service, CancellationToken ct) =>
            Results.Ok(await service.SavePolicyAsync(request, ct))).RequirePermission(PermissionCodes.NalvaManage);
    }
}
