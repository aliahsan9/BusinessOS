using System.Text.Json.Nodes;

namespace BusinessOS.Application.Features.Integrations;

/// <summary>
/// Provider-specific execution behind <see cref="IIntegrationPlatform"/>.
/// Production DI must register real providers only — never mocked transports.
/// </summary>
public interface IIntegrationProvider
{
    string Key { get; }

    Task<IntegrationCallResult> ExecuteAsync(
        HttpClient http,
        string operation,
        string accessToken,
        string argumentsJson,
        string? metadataJson,
        CancellationToken cancellationToken = default);

    bool VerifyWebhookSignature(string secret, string body, string? signature);

    /// <summary>
    /// Optional provider-specific webhook body handling after signature + idempotency checks.
    /// Return null to use the platform default (outbox enqueue only).
    /// </summary>
    Task<IntegrationWebhookHandleResult?> HandleWebhookAsync(
        Guid tenantId,
        Guid connectionId,
        string body,
        string eventId,
        string? metadataJson,
        CancellationToken cancellationToken = default);
}

public sealed record IntegrationWebhookHandleResult(bool Handled, string Message, JsonObject? Data = null);

public interface IIntegrationProviderRegistry
{
    bool TryGet(string key, out IIntegrationProvider? provider);
    IReadOnlyList<IIntegrationProvider> All { get; }
}

public interface IIntegrationWebhookIdempotencyStore
{
    Task<IntegrationWebhookClaim> TryBeginAsync(string providerKey, string eventId, Guid? tenantId, CancellationToken cancellationToken = default);
    Task CompleteAsync(Guid receiptId, CancellationToken cancellationToken = default);
    Task DeadLetterAsync(Guid receiptId, string errorRedacted, CancellationToken cancellationToken = default);
}

public abstract record IntegrationWebhookClaim
{
    public sealed record Started(Guid ReceiptId) : IntegrationWebhookClaim;
    public sealed record Duplicate : IntegrationWebhookClaim;
    public sealed record Busy : IntegrationWebhookClaim;
}

public static class IntegrationConnectionStatuses
{
    public const string Connected = "Connected";
    public const string Disconnected = "Disconnected";
    public const string Revoked = "Revoked";
    public const string Error = "Error";
    public const string NeedsReauth = "NeedsReauth";
}
