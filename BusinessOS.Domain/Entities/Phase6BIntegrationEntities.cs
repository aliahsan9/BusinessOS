using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Idempotent receipt for company-integration webhooks (Gmail push, WhatsApp, Meta, Stripe Connect).
/// Distinct from SaaS billing <see cref="ProcessedWebhookEvent"/>.
/// </summary>
public class IntegrationWebhookReceipt : BaseEntity
{
    public string ProviderKey { get; set; } = default!;
    public string EventId { get; set; } = default!;
    public Guid? TenantId { get; set; }
    public Guid? ConnectionId { get; set; }
    public string State { get; set; } = IntegrationWebhookStates.Processing;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public string? ErrorRedacted { get; set; }
}

public static class IntegrationWebhookStates
{
    public const string Processing = "Processing";
    public const string Processed = "Processed";
    public const string DeadLetter = "DeadLetter";
    public const string Failed = "Failed";
}

public class IntegrationDeadLetter : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid? ConnectionId { get; set; }
    public string ProviderKey { get; set; } = default!;
    public string Operation { get; set; } = default!;
    public string? EventId { get; set; }
    public string ErrorRedacted { get; set; } = default!;
    public string? PayloadRedacted { get; set; }
    public int Attempts { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public string Status { get; set; } = "Open";
}

public class IntegrationConversation : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string ProviderKey { get; set; } = default!;
    public Guid? ConnectionId { get; set; }
    public string ExternalThreadId { get; set; } = default!;
    public string? CustomerExternalId { get; set; }
    public string? CustomerDisplay { get; set; }
    public string Channel { get; set; } = default!;
    public DateTime? LastInboundAt { get; set; }
    public DateTime? LastOutboundAt { get; set; }
    public DateTime? CustomerCareWindowEndsAt { get; set; }
    public string? MetadataJson { get; set; }
}

public class IntegrationConversationMessage : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid ConversationId { get; set; }
    public string Direction { get; set; } = default!;
    public string MessageType { get; set; } = "text";
    public string? ExternalMessageId { get; set; }
    public string Body { get; set; } = "";
    public string? TemplateName { get; set; }
    public bool RequiresApproval { get; set; }
    public bool Approved { get; set; }
    public string DeliveryStatus { get; set; } = "Received";
    public string? IdempotencyKey { get; set; }
}

public class CompanyPaymentRecord : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string ProviderKey { get; set; } = "stripe";
    public Guid? ConnectionId { get; set; }
    public string ExternalPaymentId { get; set; } = default!;
    public string? ExternalCustomerId { get; set; }
    public string Status { get; set; } = "unknown";
    public long? AmountMinor { get; set; }
    public string? Currency { get; set; }
    public string? IdempotencyKey { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? LastEventId { get; set; }
    public string? MetadataJson { get; set; }
}

public static class IntegrationMessageDirections
{
    public const string Inbound = "Inbound";
    public const string Outbound = "Outbound";
}
