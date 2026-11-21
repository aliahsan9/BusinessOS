using BusinessOS.API.Authorization;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Features.Support;

namespace BusinessOS.API.Endpoints;

public static class SupportEndpoints
{
    public static void MapSupportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/nalva/support").WithTags("Customer Support Agent").RequireAuthorization().RequireRateLimiting("api");

        group.MapGet("/overview", async (ISupportAgentService support, CancellationToken ct) =>
            Results.Ok(await support.GetOverviewAsync(ct)));

        group.MapGet("/channels", async (ISupportAgentService support, CancellationToken ct) =>
            Results.Ok(await support.ListChannelsAsync(ct)));

        group.MapGet("/tickets", async (string? status, string? priority, ISupportAgentService support, CancellationToken ct) =>
            Results.Ok(await support.ListTicketsAsync(status, priority, ct)));

        group.MapGet("/tickets/{id:guid}", async (Guid id, ISupportAgentService support, CancellationToken ct) =>
        {
            var ticket = await support.GetTicketAsync(id, ct);
            return ticket is null ? Results.NotFound() : Results.Ok(ticket);
        });

        group.MapPost("/inbound", async (SupportInboundMessageRequest request, ISupportAgentService support, CancellationToken ct) =>
            Results.Ok(await support.ReceiveInboundAsync(request, ct))).RequirePermission(PermissionCodes.NalvaManage);

        group.MapPut("/tickets/{id:guid}", async (Guid id, SupportTicketUpdateRequest request, ISupportAgentService support, CancellationToken ct) =>
            Results.Ok(await support.UpdateTicketAsync(id, request, ct))).RequirePermission(PermissionCodes.NalvaManage);

        group.MapPost("/tickets/{id:guid}/reply", async (Guid id, SupportReplyRequest request, ISupportAgentService support, CancellationToken ct) =>
            Results.Ok(await support.AddReplyAsync(id, request, ct))).RequirePermission(PermissionCodes.NalvaManage);

        group.MapPost("/tickets/{id:guid}/escalate", async (Guid id, SupportEscalateRequest request, ISupportAgentService support, CancellationToken ct) =>
            Results.Ok(await support.EscalateAsync(id, request, ct))).RequirePermission(PermissionCodes.NalvaManage);

        group.MapPost("/tickets/{id:guid}/resolve", async (Guid id, SupportResolveRequest request, ISupportAgentService support, CancellationToken ct) =>
            Results.Ok(await support.ResolveAsync(id, request, ct))).RequirePermission(PermissionCodes.NalvaManage);

        group.MapPost("/tickets/{id:guid}/reopen", async (Guid id, SupportEscalateRequest? request, ISupportAgentService support, CancellationToken ct) =>
            Results.Ok(await support.ReopenAsync(id, request?.Reason, ct))).RequirePermission(PermissionCodes.NalvaManage);

        group.MapPost("/tickets/{id:guid}/takeover", async (Guid id, SupportTakeoverRequest request, ISupportAgentService support, CancellationToken ct) =>
            Results.Ok(await support.TakeoverAsync(id, request, ct))).RequirePermission(PermissionCodes.NalvaManage);

        group.MapPost("/tickets/{id:guid}/follow-ups", async (Guid id, SupportFollowUpRequest request, ISupportAgentService support, CancellationToken ct) =>
            Results.Ok(await support.ScheduleFollowUpAsync(id, request, ct))).RequirePermission(PermissionCodes.NalvaManage);

        group.MapPost("/follow-ups/process-due", async (ISupportAgentService support, CancellationToken ct) =>
            Results.Ok(new { processed = await support.ProcessDueFollowUpsAsync(ct) })).RequirePermission(PermissionCodes.NalvaManage);
    }
}
