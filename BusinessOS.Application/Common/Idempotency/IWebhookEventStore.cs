using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Common.Idempotency;

public abstract record WebhookClaim
{
    public sealed record Started(Guid EventRecordId) : WebhookClaim;

    public sealed record Duplicate : WebhookClaim;

    public sealed record Busy : WebhookClaim;
}

public interface IWebhookEventStore
{
    Task<WebhookClaim> TryBeginAsync(
        PaymentProviderType provider,
        string eventId,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(Guid eventRecordId, Guid? tenantId, CancellationToken cancellationToken = default);

    Task AbandonAsync(Guid eventRecordId, CancellationToken cancellationToken = default);
}
