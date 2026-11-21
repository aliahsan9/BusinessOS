using BusinessOS.API.Authorization;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Features.ProcurementAgent;

namespace BusinessOS.API.Endpoints;

public static class ProcurementAgentEndpoints
{
    public static void MapProcurementAgentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/nalva/procurement")
            .WithTags("Inventory & Procurement Agent")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        group.MapGet("/overview", async (IInventoryProcurementAgentService service, CancellationToken ct) =>
            Results.Ok(await service.GetOverviewAsync(ct)));

        group.MapGet("/risks", async (IInventoryProcurementAgentService service, CancellationToken ct) =>
            Results.Ok(await service.GetInventoryRisksAsync(ct)));

        group.MapGet("/demand", async (Guid? productId, IInventoryProcurementAgentService service, CancellationToken ct) =>
            Results.Ok(await service.AnalyzeDemandAsync(productId, ct)));

        group.MapGet("/recommendations", async (IInventoryProcurementAgentService service, CancellationToken ct) =>
            Results.Ok(await service.GetRecommendationsAsync(ct)));

        group.MapGet("/suppliers/compare", async (Guid productId, decimal quantity, IInventoryProcurementAgentService service, CancellationToken ct) =>
            Results.Ok(await service.CompareSuppliersAsync(productId, quantity <= 0 ? 1 : quantity, ct)));

        group.MapPost("/prepare", async (PreparePurchaseOrderRequest request, IInventoryProcurementAgentService service, CancellationToken ct) =>
            Results.Ok(await service.PreparePurchaseOrderAsync(request, ct)))
            .RequirePermission(PermissionCodes.PurchaseOrderCreate);

        group.MapPost("/submit/{recommendationId:guid}", async (
            Guid recommendationId,
            SubmitPreparedPurchaseRequest? request,
            IInventoryProcurementAgentService service,
            CancellationToken ct) =>
            Results.Ok(await service.SubmitPreparedPurchaseAsync(
                recommendationId,
                request?.IdempotencyKey,
                request?.Approved == true,
                request?.ApprovalFingerprint,
                ct)))
            .RequirePermission(PermissionCodes.NalvaApprove);

        group.MapGet("/deliveries", async (IInventoryProcurementAgentService service, CancellationToken ct) =>
            Results.Ok(await service.GetDeliveriesAsync(ct)));

        group.MapPost("/receive", async (ReceiveVerificationRequest request, IInventoryProcurementAgentService service, CancellationToken ct) =>
            Results.Ok(await service.ReceiveVerifiedAsync(request, ct)))
            .RequirePermission(PermissionCodes.PurchaseOrderUpdate);

        group.MapGet("/activity", async (IInventoryProcurementAgentService service, CancellationToken ct) =>
            Results.Ok(await service.GetActivityAsync(ct)));

        group.MapGet("/automation", async (IInventoryProcurementAgentService service, CancellationToken ct) =>
            Results.Ok(await service.GetAutomationSettingsAsync(ct)));

        group.MapPut("/automation", async (ProcurementAutomationSettingsWriteRequest request, IInventoryProcurementAgentService service, CancellationToken ct) =>
            Results.Ok(await service.SaveAutomationSettingsAsync(request, ct)))
            .RequirePermission(PermissionCodes.NalvaManage);

        group.MapPost("/monitor/run", async (IInventoryProcurementAgentService service, CancellationToken ct) =>
        {
            await service.RunScheduledMonitorAsync(ct);
            return Results.Ok(new { ran = true });
        }).RequirePermission(PermissionCodes.NalvaManage);
    }
}

public sealed class SubmitPreparedPurchaseRequest
{
    public bool Approved { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? ApprovalFingerprint { get; set; }
}
