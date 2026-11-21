using System.Security.Cryptography;
using System.Text;
using BusinessOS.Application.Features.Integrations;

namespace BusinessOS.Infrastructure.Integrations.Providers;

/// <summary>
/// Default provider that executes catalog HTTP operations via <see cref="IntegrationApi"/>.
/// </summary>
public class CatalogIntegrationProvider : IIntegrationProvider
{
    public CatalogIntegrationProvider(string key) => Key = key;

    public string Key { get; }

    public virtual async Task<IntegrationCallResult> ExecuteAsync(
        HttpClient http,
        string operation,
        string accessToken,
        string argumentsJson,
        string? metadataJson,
        CancellationToken cancellationToken = default)
    {
        if (!IntegrationProviderCatalog.TryGet(Key, out var definition) || definition is null)
        {
            return new IntegrationCallResult
            {
                Performed = false,
                Status = "unknown_provider",
                Message = "That provider is not registered."
            };
        }

        return await IntegrationApi.ExecuteAsync(http, definition, operation, accessToken, argumentsJson, metadataJson, cancellationToken);
    }

    public virtual bool VerifyWebhookSignature(string secret, string body, string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
            return false;
        if (Key == "stripe")
            return StripeSignature(secret, body, signature);
        var supplied = signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase) ? signature[7..] : signature;
        byte[] expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body));
        byte[] candidate;
        try
        {
            candidate = Key == "shopify" ? Convert.FromBase64String(supplied.Trim()) : Convert.FromHexString(supplied.Trim());
        }
        catch (FormatException)
        {
            return false;
        }
        return candidate.Length == expected.Length && CryptographicOperations.FixedTimeEquals(expected, candidate);
    }

    public virtual Task<IntegrationWebhookHandleResult?> HandleWebhookAsync(
        Guid tenantId,
        Guid connectionId,
        string body,
        string eventId,
        string? metadataJson,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IntegrationWebhookHandleResult?>(null);

    private static bool StripeSignature(string secret, string body, string signature)
    {
        var parts = signature.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var timestamp = parts.Select(part => part.Split('=', 2)).FirstOrDefault(part => part.Length == 2 && part[0] == "t") is { } time ? time[1] : null;
        var v1 = parts.Select(part => part.Split('=', 2)).Where(part => part.Length == 2 && part[0] == "v1").Select(part => part[1]).ToList();
        if (timestamp is null || v1.Count == 0)
            return false;
        var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(timestamp + "." + body))).ToLowerInvariant();
        return v1.Any(value => value.Length == expected.Length && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(value.ToLowerInvariant())));
    }
}
