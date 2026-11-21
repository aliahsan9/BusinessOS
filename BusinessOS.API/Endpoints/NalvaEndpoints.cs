using BusinessOS.API.Authorization;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Features.Nalva;

namespace BusinessOS.API.Endpoints;

public static class NalvaEndpoints
{
    public static void MapNalvaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/nalva").WithTags("Nalva.ai").RequireAuthorization().RequireRateLimiting("api");

        group.MapGet("/overview", async (INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.GetOverviewAsync(ct)));
        group.MapGet("/marketplace", async (INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.GetMarketplaceAsync(ct)));
        group.MapGet("/agents", async (INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.GetMyAgentsAsync(ct)));
        group.MapGet("/agents/{key}", async (string key, INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.GetAgentAsync(key, ct)));
        group.MapPost("/agents/{key}/activate", async (string key, INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.ActivateAsync(key, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapPost("/agents/{key}/deactivate", async (string key, INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.DeactivateAsync(key, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapPut("/agents/{key}", async (string key, NalvaConfigureAgentRequest request, INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.ConfigureAsync(key, request, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapPost("/agents/custom", async (NalvaCustomAgentRequest request, INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.CreateCustomAsync(request, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapPost("/agents/{key}/run", async (string key, NalvaRunRequest request, INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.RunAsync(key, request, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapGet("/tasks", async (string? agentKey, string? status, INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.GetTasksAsync(agentKey, status, ct)));
        group.MapPost("/agents/{key}/tasks", async (string key, NalvaRunRequest request, INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.EnqueueAsync(key, request, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapPost("/tasks/{id:guid}/cancel", async (Guid id, INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.CancelTaskAsync(id, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapPost("/tasks/{id:guid}/pause", async (Guid id, INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.PauseTaskAsync(id, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapPost("/tasks/{id:guid}/resume", async (Guid id, INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.ResumeTaskAsync(id, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapPost("/tasks/{id:guid}/retry", async (Guid id, INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.RetryTaskAsync(id, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapGet("/workflows", async (INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.GetWorkflowsAsync(ct)));
        group.MapPost("/workflows", async (NalvaWorkflowWriteRequest request, INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.SaveWorkflowAsync(request, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapPost("/workflows/{id:guid}/activate", async (Guid id, INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.SetWorkflowActiveAsync(id, true, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapPost("/workflows/{id:guid}/deactivate", async (Guid id, INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.SetWorkflowActiveAsync(id, false, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapPost("/workflows/{id:guid}/run", async (Guid id, NalvaEventRequest? request, INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.RunWorkflowAsync(id, request?.ArgumentsJson, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapPost("/workflows/{id:guid}/test", async (Guid id, NalvaEventRequest? request, INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.TestWorkflowAsync(id, request?.ArgumentsJson, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapGet("/workflows/runs", async (INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.GetWorkflowRunsAsync(ct)));
        group.MapPost("/events", async (NalvaEventRequest request, INalvaWorkforceService service, CancellationToken ct) => Results.Ok(new { started = await service.PublishEventAsync(request, ct) })).RequirePermission(PermissionCodes.NalvaManage);
        group.MapGet("/approvals", async (string? status, INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.GetApprovalsAsync(status, ct)));
        group.MapPost("/approvals/{id:guid}/approve", async (Guid id, NalvaApprovalDecisionRequest? request, INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.DecideApprovalAsync(id, "approve", request?.Note, ct))).RequirePermission(PermissionCodes.NalvaApprove);
        group.MapPost("/approvals/{id:guid}/reject", async (Guid id, NalvaApprovalDecisionRequest? request, INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.DecideApprovalAsync(id, "reject", request?.Note, ct))).RequirePermission(PermissionCodes.NalvaApprove);
        group.MapPost("/approvals/{id:guid}/changes", async (Guid id, NalvaApprovalDecisionRequest? request, INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.DecideApprovalAsync(id, "changes", request?.Note, ct))).RequirePermission(PermissionCodes.NalvaApprove);
        group.MapGet("/knowledge", async (string? agentKey, INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.GetKnowledgeAsync(agentKey, ct)));
        group.MapPost("/knowledge", async (NalvaKnowledgeWriteRequest request, INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.AddKnowledgeAsync(request, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapDelete("/knowledge/{id:guid}", async (Guid id, INalvaWorkforceService service, CancellationToken ct) => { await service.DeleteKnowledgeAsync(id, ct); return Results.NoContent(); }).RequirePermission(PermissionCodes.NalvaManage);
        group.MapGet("/agents/{key}/memory", async (string key, INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.GetMemoryAsync(key, ct)));
        group.MapDelete("/memory/{id:guid}", async (Guid id, INalvaWorkforceService service, CancellationToken ct) => { await service.DeleteMemoryAsync(id, ct); return Results.NoContent(); }).RequirePermission(PermissionCodes.NalvaManage);
        group.MapGet("/executions", async (string? agentKey, string? status, DateTime? from, DateTime? to, INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.GetExecutionsAsync(agentKey, status, from, to, ct)));
        group.MapGet("/analytics", async (string? agentKey, string? category, DateTime? from, DateTime? to, INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.GetAnalyticsAsync(agentKey, category, from, to, ct)));
        group.MapGet("/outcomes", async (string? agentKey, DateTime? from, DateTime? to, INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.GetOutcomesAsync(agentKey, from, to, ct)));
        group.MapGet("/workforce/recommendation", async (INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.GetWorkforceRecommendationAsync(ct)));
        group.MapPost("/workforce/apply", async (NalvaApplyWorkforcePackRequest? request, INalvaWorkforceService service, CancellationToken ct) =>
            Results.Ok(await service.ApplyWorkforcePackAsync(request ?? new NalvaApplyWorkforcePackRequest(), ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapGet("/settings", async (INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.GetSettingsAsync(ct)));
        group.MapPut("/settings", async (NalvaSettingsWriteRequest request, INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.SaveSettingsAsync(request, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapGet("/models", async (INalvaWorkforceService service, CancellationToken ct) => Results.Ok(await service.GetModelsAsync(ct)));
    }
}
