using BusinessOS.API.Authorization;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Features.Autopilot;

namespace BusinessOS.API.Endpoints;

public static class AutopilotEndpoints
{
    public static void MapAutopilotEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/nalva/autopilot")
            .WithTags("Business Autopilot")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        group.MapGet("/config", async (IBusinessAutopilotService service, CancellationToken ct) =>
            Results.Ok(await service.GetConfigAsync(ct)));

        group.MapPut("/config", async (AutopilotConfigWriteRequest request, IBusinessAutopilotService service, CancellationToken ct) =>
            Results.Ok(await service.SaveConfigAsync(request, ct)))
            .RequirePermission(PermissionCodes.NalvaManage);

        group.MapPost("/activate", async (AutopilotActivateRequest request, IBusinessAutopilotService service, CancellationToken ct) =>
            Results.Ok(await service.ActivateAsync(request, ct)))
            .RequirePermission(PermissionCodes.NalvaManage);

        group.MapPost("/emergency-stop", async (IBusinessAutopilotService service, CancellationToken ct) =>
            Results.Ok(await service.SetEmergencyStopAsync(true, ct)))
            .RequirePermission(PermissionCodes.NalvaManage);

        group.MapPost("/emergency-stop/clear", async (IBusinessAutopilotService service, CancellationToken ct) =>
            Results.Ok(await service.SetEmergencyStopAsync(false, ct)))
            .RequirePermission(PermissionCodes.NalvaManage);

        group.MapPost("/agents/pause-all", async (IBusinessAutopilotService service, CancellationToken ct) =>
            Results.Ok(await service.PauseAllAgentsAsync(ct)))
            .RequirePermission(PermissionCodes.NalvaManage);

        group.MapPost("/agents/{key}/pause", async (string key, IBusinessAutopilotService service, CancellationToken ct) =>
            Results.Ok(await service.PauseAgentAsync(key, ct)))
            .RequirePermission(PermissionCodes.NalvaManage);

        group.MapPost("/agents/{key}/resume", async (string key, IBusinessAutopilotService service, CancellationToken ct) =>
            Results.Ok(await service.ResumeAgentAsync(key, ct)))
            .RequirePermission(PermissionCodes.NalvaManage);

        group.MapPost("/agents/clear-pauses", async (IBusinessAutopilotService service, CancellationToken ct) =>
            Results.Ok(await service.ClearAgentPausesAsync(ct)))
            .RequirePermission(PermissionCodes.NalvaManage);

        group.MapPost("/scenarios/{key}/run", async (string key, bool? dryRun, IBusinessAutopilotService service, CancellationToken ct) =>
            Results.Ok(await service.RunScenarioAsync(key, dryRun ?? false, ct)))
            .RequirePermission(PermissionCodes.NalvaManage);

        group.MapGet("/ops", async (IBusinessAutopilotService service, CancellationToken ct) =>
            Results.Ok(await service.GetOpsSnapshotAsync(ct)));

        group.MapPost("/tasks/{id:guid}/retry", async (Guid id, IBusinessAutopilotService service, CancellationToken ct) =>
        {
            await service.RetryFailedTaskAsync(id, ct);
            return Results.Ok(new { retried = id });
        }).RequirePermission(PermissionCodes.NalvaManage);

        group.MapPost("/workflows/runs/{id:guid}/stop", async (Guid id, IBusinessAutopilotService service, CancellationToken ct) =>
        {
            await service.StopWorkflowRunAsync(id, ct);
            return Results.Ok(new { stopped = id });
        }).RequirePermission(PermissionCodes.NalvaManage);

        // Goals
        group.MapGet("/goals", async (IBusinessGoalService goals, CancellationToken ct) =>
            Results.Ok(await goals.ListAsync(ct)));
        group.MapGet("/goals/{id:guid}", async (Guid id, IBusinessGoalService goals, CancellationToken ct) =>
            Results.Ok(await goals.GetAsync(id, ct)));
        group.MapPost("/goals", async (BusinessGoalWriteRequest request, IBusinessGoalService goals, CancellationToken ct) =>
            Results.Ok(await goals.CreateAsync(request, ct)))
            .RequirePermission(PermissionCodes.NalvaManage);
        group.MapPut("/goals/{id:guid}", async (Guid id, BusinessGoalWriteRequest request, IBusinessGoalService goals, CancellationToken ct) =>
            Results.Ok(await goals.UpdateAsync(id, request, ct)))
            .RequirePermission(PermissionCodes.NalvaManage);
        group.MapPost("/goals/{id:guid}/refresh", async (Guid id, IBusinessGoalService goals, CancellationToken ct) =>
            Results.Ok(await goals.RefreshActualAsync(id, ct)))
            .RequirePermission(PermissionCodes.NalvaManage);
        group.MapDelete("/goals/{id:guid}", async (Guid id, IBusinessGoalService goals, CancellationToken ct) =>
        {
            await goals.DeleteAsync(id, ct);
            return Results.NoContent();
        }).RequirePermission(PermissionCodes.NalvaManage);

        // Business memory
        group.MapGet("/memory", async (string? kind, IBusinessMemoryService memory, CancellationToken ct) =>
            Results.Ok(await memory.ListAsync(kind, ct)));
        group.MapGet("/memory/search", async (string? q, int? take, IBusinessMemoryService memory, CancellationToken ct) =>
            Results.Ok(await memory.RetrieveForContextAsync(q ?? "", take ?? 8, ct)));
        group.MapPost("/memory", async (BusinessMemoryWriteRequest request, IBusinessMemoryService memory, CancellationToken ct) =>
            Results.Ok(await memory.UpsertAsync(request, ct)))
            .RequirePermission(PermissionCodes.NalvaManage);
        group.MapPut("/memory/{id:guid}", async (Guid id, BusinessMemoryWriteRequest request, IBusinessMemoryService memory, CancellationToken ct) =>
            Results.Ok(await memory.CorrectAsync(id, request, ct)))
            .RequirePermission(PermissionCodes.NalvaManage);
        group.MapDelete("/memory/{id:guid}", async (Guid id, IBusinessMemoryService memory, CancellationToken ct) =>
        {
            await memory.DeleteAsync(id, ct);
            return Results.NoContent();
        }).RequirePermission(PermissionCodes.NalvaManage);
    }
}
