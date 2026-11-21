using BusinessOS.API.Authorization;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Features.SalesAgent;

namespace BusinessOS.API.Endpoints;

public static class SalesAgentEndpoints
{
    public static void MapSalesAgentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/nalva/sales-agent").WithTags("Sales Agent").RequireAuthorization().RequireRateLimiting("api");

        group.MapGet("/command-center", async (ISalesAgentService sales, CancellationToken ct) =>
            Results.Ok(await sales.GetCommandCenterAsync(ct)));

        group.MapGet("/settings", async (ISalesAgentService sales, CancellationToken ct) =>
            Results.Ok(await sales.GetSettingsAsync(ct)));

        group.MapPut("/settings", async (SalesAgentSettingsWriteRequest request, ISalesAgentService sales, CancellationToken ct) =>
            Results.Ok(await sales.SaveSettingsAsync(request, ct))).RequirePermission(PermissionCodes.NalvaManage);

        group.MapPost("/objectives", async (AssignSalesObjectiveRequest request, ISalesAgentService sales, CancellationToken ct) =>
            Results.Ok(await sales.AssignObjectiveAsync(request, ct))).RequirePermission(PermissionCodes.NalvaManage);

        group.MapPost("/pause", async (ISalesAgentService sales, CancellationToken ct) =>
            Results.Ok(await sales.PauseAsync(ct))).RequirePermission(PermissionCodes.NalvaManage);

        group.MapPost("/resume", async (ISalesAgentService sales, CancellationToken ct) =>
            Results.Ok(await sales.ResumeAsync(ct))).RequirePermission(PermissionCodes.NalvaManage);

        group.MapGet("/research-providers", async (ISalesAgentService sales, CancellationToken ct) =>
            Results.Ok(await sales.GetResearchProvidersAsync(ct)));

        group.MapPost("/prospects/import", async (ImportAuthorizedProspectsRequest request, ISalesAgentService sales, CancellationToken ct) =>
            Results.Ok(await sales.ImportAuthorizedProspectsAsync(request, ct))).RequirePermission(PermissionCodes.NalvaManage);

        group.MapGet("/reports/campaign", async (Guid? objectiveId, Guid? campaignId, ISalesAgentService sales, CancellationToken ct) =>
            Results.Ok(await sales.GetCampaignReportAsync(objectiveId, campaignId, ct)));

        group.MapPost("/replies/simulate", async (SimulateProspectReplyRequest request, ISalesAgentService sales, CancellationToken ct) =>
            Results.Ok(await sales.SimulateProspectReplyAsync(request, ct))).RequirePermission(PermissionCodes.NalvaManage);
    }
}
