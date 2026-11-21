using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public static class SupportTicketStatuses
{
    public const string New = "New";
    public const string Open = "Open";
    public const string InProgress = "InProgress";
    public const string WaitingForCustomer = "WaitingForCustomer";
    public const string Escalated = "Escalated";
    public const string Resolved = "Resolved";
    public const string Closed = "Closed";

    public static readonly IReadOnlyList<string> All =
    [
        New, Open, InProgress, WaitingForCustomer, Escalated, Resolved, Closed
    ];
}

public static class SupportPriorities
{
    public const string Low = "Low";
    public const string Normal = "Normal";
    public const string High = "High";
    public const string Urgent = "Urgent";
}

public static class SupportChannels
{
    public const string WebsiteChat = "website-chat";
    public const string Email = "email";
    public const string WhatsApp = "whatsapp";
    public const string Messenger = "messenger";
    public const string Sandbox = "sandbox";
}

public static class SupportMessageDirections
{
    public const string Inbound = "Inbound";
    public const string Outbound = "Outbound";
    public const string Internal = "Internal";
}

public static class SupportDeliveryStatuses
{
    public const string Pending = "Pending";
    public const string Sent = "Sent";
    public const string Delivered = "Delivered";
    public const string Failed = "Failed";
    public const string Skipped = "Skipped";
    public const string Simulated = "Simulated";
}

public static class SupportChannelConnectionStatuses
{
    public const string Connected = "Connected";
    public const string Disconnected = "Disconnected";
    public const string Sandbox = "Sandbox";
    public const string Unavailable = "Unavailable";
}

public static class SupportFollowUpStatuses
{
    public const string Scheduled = "Scheduled";
    public const string Sent = "Sent";
    public const string Cancelled = "Cancelled";
    public const string Failed = "Failed";
    public const string Skipped = "Skipped";
}

/// <summary>
/// Persisted customer-support ticket owned by a tenant.
/// </summary>
public class SupportTicket : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string TicketNumber { get; set; } = default!;
    public Guid? CustomerId { get; set; }
    public string Subject { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string Category { get; set; } = "General";
    public string Priority { get; set; } = SupportPriorities.Normal;
    public string Status { get; set; } = SupportTicketStatuses.New;
    public string Channel { get; set; } = SupportChannels.Sandbox;
    public string? AssignedAgentKey { get; set; }
    public string? AssignedUserId { get; set; }
    public string? ExternalThreadId { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? AiSummary { get; set; }
    public string? RecommendedAction { get; set; }
    public string? Resolution { get; set; }
    public string? EscalationReason { get; set; }
    public string? EscalationSummary { get; set; }
    public DateTime? EscalatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? LastCustomerMessageAt { get; set; }
    public DateTime? LastAgentMessageAt { get; set; }
    public Guid? RelatedOrderId { get; set; }
    public Guid? ExecutionId { get; set; }
    public bool HumanTakeover { get; set; }
    public bool MarketingConsentForHandoff { get; set; }
    public int ReopenCount { get; set; }

    public ICollection<SupportMessage> Messages { get; set; } = new List<SupportMessage>();
    public ICollection<SupportFollowUpJob> FollowUps { get; set; } = new List<SupportFollowUpJob>();
}

public class SupportMessage : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid TicketId { get; set; }
    public string Channel { get; set; } = SupportChannels.Sandbox;
    public string Direction { get; set; } = SupportMessageDirections.Inbound;
    public string Body { get; set; } = default!;
    public string AuthorType { get; set; } = "Customer";
    public string? AuthorId { get; set; }
    public string DeliveryStatus { get; set; } = SupportDeliveryStatuses.Pending;
    public string? ExternalMessageId { get; set; }
    public string? IdempotencyKey { get; set; }
    public bool IsInternalNote { get; set; }
    public string? Error { get; set; }
    public DateTime? DeliveredAt { get; set; }

    public SupportTicket Ticket { get; set; } = default!;
}

public class SupportFollowUpJob : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid TicketId { get; set; }
    public string Kind { get; set; } = "CustomerCheckIn";
    public string Status { get; set; } = SupportFollowUpStatuses.Scheduled;
    public DateTime DueAt { get; set; }
    public string Channel { get; set; } = SupportChannels.Sandbox;
    public string MessageTemplate { get; set; } = default!;
    public string? DedupKey { get; set; }
    public DateTime? SentAt { get; set; }
    public string? LastError { get; set; }
    public bool RespectBusinessHours { get; set; } = true;
    public bool OptOutHonored { get; set; }

    public SupportTicket Ticket { get; set; } = default!;
}

public class SupportChannelAccount : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Channel { get; set; } = default!;
    public string Status { get; set; } = SupportChannelConnectionStatuses.Disconnected;
    public string DisplayName { get; set; } = default!;
    public string? ExternalAccountId { get; set; }
    public string? MetadataJson { get; set; }
    public bool AllowsOutbound { get; set; }
    public bool AllowsInbound { get; set; } = true;
    public DateTime? LastInboundAt { get; set; }
    public DateTime? LastOutboundAt { get; set; }
    public string? LastError { get; set; }
}
