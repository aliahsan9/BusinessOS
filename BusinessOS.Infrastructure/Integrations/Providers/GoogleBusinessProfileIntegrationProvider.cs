using System.Text.Json.Nodes;
using BusinessOS.Application.Features.Integrations;

namespace BusinessOS.Infrastructure.Integrations.Providers;

/// <summary>
/// Google Business Profile operations via official Google APIs.
/// Review reply management is intentionally not exposed.
/// </summary>
public sealed class GoogleBusinessProfileIntegrationProvider : CatalogIntegrationProvider
{
    public GoogleBusinessProfileIntegrationProvider() : base("google-business-profile")
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
        if (string.Equals(operation, "createPost", StringComparison.OrdinalIgnoreCase))
        {
            var metadata = JsonNode.Parse(string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson) as JsonObject ?? new JsonObject();
            if (string.IsNullOrWhiteSpace(metadata["locationId"]?.GetValue<string>()))
            {
                return new IntegrationCallResult
                {
                    Performed = false,
                    Status = "configuration",
                    Message = "Store locationId metadata before creating a Google Business Profile post."
                };
            }
        }

        if (string.Equals(operation, "locations", StringComparison.OrdinalIgnoreCase)
            || string.Equals(operation, "location", StringComparison.OrdinalIgnoreCase)
            || string.Equals(operation, "performance", StringComparison.OrdinalIgnoreCase))
        {
            var metadata = JsonNode.Parse(string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson) as JsonObject ?? new JsonObject();
            var required = operation == "locations" ? "accountId" : "locationId";
            if (string.IsNullOrWhiteSpace(metadata[required]?.GetValue<string>()))
            {
                return new IntegrationCallResult
                {
                    Performed = false,
                    Status = "configuration",
                    Message = $"Store {required} metadata before calling {operation}."
                };
            }
        }

        return await base.ExecuteAsync(http, operation, accessToken, argumentsJson, metadataJson, cancellationToken);
    }
}
