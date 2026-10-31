using BusinessOS.Domain.Common;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Provider event receipt. Uniqueness is (Provider, EventId), independent of tenant filters,
/// because callbacks arrive before a tenant context exists.
/// </summary>
public class ProcessedWebhookEvent : BaseEntity
{
    public PaymentProviderType Provider { get; set; }

    public string EventId { get; set; } = default!;

    public Guid? TenantId { get; set; }

    public WebhookEventState State { get; set; } = WebhookEventState.Processing;

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedAt { get; set; }
}
