using BusinessOS.API.Authorization;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Features.Integrations;

namespace BusinessOS.API.Endpoints;

public static class IntegrationEndpoints
{
    public static void MapIntegrationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/integrations").WithTags("Integrations").RequireAuthorization().RequireRateLimiting("api");
        group.MapGet("/marketplace", async (IIntegrationPlatform platform, CancellationToken ct) => Results.Ok(await platform.GetMarketplaceAsync(ct)));
        group.MapGet("/connections", async (IIntegrationPlatform platform, CancellationToken ct) => Results.Ok(await platform.GetConnectionsAsync(ct)));
        group.MapGet("/health", async (IIntegrationPlatform platform, CancellationToken ct) => Results.Ok(await platform.GetHealthAsync(ct)));
        group.MapPost("/{provider}/authorize", async (string provider, IntegrationAuthorizeRequest request, IIntegrationPlatform platform, CancellationToken ct) =>
            Results.Ok(await platform.BeginAuthorizeAsync(provider, request, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapPost("/{provider}/callback", async (string provider, IntegrationCallbackRequest request, IIntegrationPlatform platform, CancellationToken ct) =>
            Results.Ok(await platform.CompleteAuthorizeAsync(provider, request, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapPost("/smtp/configure", async (IntegrationSmtpConfigureRequest request, IIntegrationPlatform platform, CancellationToken ct) =>
            Results.Ok(await platform.ConfigureSmtpAsync(request, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapPost("/connections/{id:guid}/disconnect", async (Guid id, IIntegrationPlatform platform, CancellationToken ct) =>
            Results.Ok(await platform.DisconnectAsync(id, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapPost("/connections/{id:guid}/test", async (Guid id, IIntegrationPlatform platform, CancellationToken ct) =>
            Results.Ok(await platform.TestAsync(id, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapPost("/connections/{id:guid}/webhook-secret", async (Guid id, IntegrationWebhookSecretRequest request, IIntegrationPlatform platform, CancellationToken ct) =>
            Results.Ok(await platform.SetWebhookSecretAsync(id, request, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapPut("/connections/{id:guid}/metadata", async (Guid id, IntegrationConnectionMetadataRequest request, IIntegrationPlatform platform, CancellationToken ct) =>
            Results.Ok(await platform.UpdateConnectionMetadataAsync(id, request, ct))).RequirePermission(PermissionCodes.NalvaManage);
        group.MapGet("/logs", async (Guid? connectionId, IIntegrationPlatform platform, CancellationToken ct) =>
            Results.Ok(await platform.GetLogsAsync(connectionId, ct)));
        // Meta WhatsApp / Instagram hub verification (GET challenge).
        group.MapGet("/webhooks/{provider}/{connectionId:guid}", async (string provider, Guid connectionId, HttpRequest request, IIntegrationPlatform platform, CancellationToken ct) =>
        {
            var result = await platform.VerifyWebhookSubscriptionAsync(
                provider,
                connectionId,
                request.Query["hub.mode"].FirstOrDefault(),
                request.Query["hub.verify_token"].FirstOrDefault(),
                request.Query["hub.challenge"].FirstOrDefault(),
                ct);
            return result.StatusCode == 200 && result.Challenge is not null
                ? Results.Content(result.Challenge, "text/plain")
                : Results.Json(new { message = "Webhook verification failed." }, statusCode: result.StatusCode);
        }).AllowAnonymous();

        group.MapPost("/webhooks/{provider}/{connectionId:guid}", async (string provider, Guid connectionId, HttpRequest request, IIntegrationPlatform platform, CancellationToken ct) =>
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync(ct);
            var signature = request.Headers["X-Hub-Signature-256"].FirstOrDefault()
                ?? request.Headers["Stripe-Signature"].FirstOrDefault()
                ?? request.Headers["X-Shopify-Hmac-Sha256"].FirstOrDefault()
                ?? request.Headers["X-CompanyOS-Signature"].FirstOrDefault();
            var eventId = request.Headers["X-GitHub-Delivery"].FirstOrDefault()
                ?? request.Headers["X-Event-Id"].FirstOrDefault()
                ?? request.Headers["X-Request-Id"].FirstOrDefault();
            var result = await platform.ReceiveWebhookAsync(provider, connectionId, body, signature, eventId, ct);
            return Results.Json(new { message = result.Message }, statusCode: result.StatusCode);
        }).AllowAnonymous();
    }
}
