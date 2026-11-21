using System.Text.Json.Nodes;
using BusinessOS.Application.Features.Integrations;

namespace BusinessOS.Infrastructure.Integrations.Providers;

public sealed class MetaIntegrationProvider : CatalogIntegrationProvider
{
    public MetaIntegrationProvider() : base("meta")
    {
    }

    public override async Task<IntegrationCallResult> ExecuteAsync(
        HttpClient http,
        string operation,
        string accessToken,
        string argumentsJson,
        string? metadataJson,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(operation, "publishInstagram", StringComparison.OrdinalIgnoreCase))
        {
            var metadata = JsonNode.Parse(string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson) as JsonObject ?? new JsonObject();
            var ig = metadata["instagramBusinessAccountId"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(ig))
            {
                return new IntegrationCallResult
                {
                    Performed = false,
                    Status = "configuration",
                    Message = "Instagram publishing requires connection metadata instagramBusinessAccountId and instagram_content_publish scope. Nothing was published."
                };
            }

            var args = JsonNode.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson) as JsonObject ?? new JsonObject();
            if (string.IsNullOrWhiteSpace(args["imageUrl"]?.GetValue<string>() ?? args["mediaUrl"]?.GetValue<string>()))
            {
                return new IntegrationCallResult
                {
                    Performed = false,
                    Status = "validation",
                    Message = "Instagram Content Publishing API requires an imageUrl. Carousel/video publishing is not enabled."
                };
            }

            var create = await base.ExecuteAsync(http, "publishInstagram", accessToken, argumentsJson, metadataJson, cancellationToken);
            if (!create.Performed)
                return create;

            var creationId = create.Data["response"]?["id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(creationId))
            {
                return new IntegrationCallResult
                {
                    Performed = false,
                    Status = "provider_error",
                    Message = "Instagram media container was not created.",
                    Data = create.Data
                };
            }

            var publishBody = new JsonObject { ["creation_id"] = creationId };
            return await IntegrationHttpExecutor.SendAsync(
                http,
                "meta",
                "publishInstagram",
                HttpMethod.Post,
                "https://graph.facebook.com/v21.0/" + Uri.EscapeDataString(ig) + "/media_publish",
                accessToken,
                publishBody,
                cancellationToken);
        }

        if (string.Equals(operation, "publish", StringComparison.OrdinalIgnoreCase))
        {
            var metadata = JsonNode.Parse(string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson) as JsonObject ?? new JsonObject();
            if (string.IsNullOrWhiteSpace(metadata["pageId"]?.GetValue<string>()))
            {
                return new IntegrationCallResult
                {
                    Performed = false,
                    Status = "configuration",
                    Message = "Facebook Page publish requires connection metadata pageId and pages_manage_posts scope."
                };
            }
        }

        return await base.ExecuteAsync(http, operation, accessToken, argumentsJson, metadataJson, cancellationToken);
    }
}
