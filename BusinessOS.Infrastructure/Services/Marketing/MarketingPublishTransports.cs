using System.Text.Json.Nodes;
using BusinessOS.Application.Features.Integrations;
using BusinessOS.Application.Features.Marketing;

namespace BusinessOS.Infrastructure.Services.Marketing;

public sealed class SandboxMarketingPublishTransport : IMarketingPublishTransport
{
    public string ProviderKey => "sandbox";
    public bool IsSandbox => true;

    public Task<MarketingPublishTransportResult> PublishAsync(MarketingPublishTransportRequest request, CancellationToken cancellationToken = default)
    {
        var id = $"sandbox-{Guid.NewGuid():N}";
        return Task.FromResult(new MarketingPublishTransportResult(true, id, null, "Sandbox"));
    }
}

public sealed class MetaMarketingPublishTransport : IMarketingPublishTransport
{
    private readonly IIntegrationPlatform _integrations;

    public MetaMarketingPublishTransport(IIntegrationPlatform integrations) => _integrations = integrations;

    public string ProviderKey => "meta";
    public bool IsSandbox => false;

    public async Task<MarketingPublishTransportResult> PublishAsync(MarketingPublishTransportRequest request, CancellationToken cancellationToken = default)
    {
        var args = new JsonObject
        {
            ["message"] = request.Message,
            ["caption"] = request.Caption,
            ["idempotencyKey"] = request.IdempotencyKey
        };
        if (!string.IsNullOrWhiteSpace(request.Link))
            args["link"] = request.Link;

        var result = await _integrations.ExecuteAsync("meta", "publish", args.ToJsonString(), true, cancellationToken);
        if (!result.Performed)
            return new MarketingPublishTransportResult(false, null, result.Message, "Meta");

        string? externalId = null;
        if (result.Data?["response"] is JsonObject response)
            externalId = response["id"]?.GetValue<string>() ?? response["post_id"]?.GetValue<string>();

        return new MarketingPublishTransportResult(true, externalId ?? $"meta-{request.JobId:N}", null, "Meta");
    }
}
