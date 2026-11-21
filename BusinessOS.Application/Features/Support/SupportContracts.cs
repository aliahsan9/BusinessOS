using System.Text.Json.Nodes;

namespace BusinessOS.Application.Features.Support;

public static class SupportAgentKeys
{
    public const string Support = "support";
}

public sealed class SupportInboundMessageRequest
{
    public string Channel { get; set; } = "sandbox";
    public string Body { get; set; } = "";
    public string? ExternalThreadId { get; set; }
    public string? ExternalMessageId { get; set; }
    public string? IdempotencyKey { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string? Subject { get; set; }
    public Guid? OrderId { get; set; }
    public string? OrderNumber { get; set; }
    public bool ProcessWithAgent { get; set; } = true;
}

public sealed class SupportReplyRequest
{
    public string Body { get; set; } = "";
    public bool IsInternalNote { get; set; }
    public bool SendToCustomer { get; set; } = true;
    public string? Channel { get; set; }
}

public sealed class SupportEscalateRequest
{
    public string Reason { get; set; } = "";
    public string? Summary { get; set; }
    public string? RecommendedAction { get; set; }
    public string Priority { get; set; } = "High";
    public string? NotifyUserId { get; set; }
}

public sealed class SupportResolveRequest
{
    public string Resolution { get; set; } = "";
    public bool Close { get; set; }
    public bool ScheduleFollowUp { get; set; } = true;
    public int FollowUpHours { get; set; } = 24;
}

public sealed class SupportFollowUpRequest
{
    public string Kind { get; set; } = "CustomerCheckIn";
    public string MessageTemplate { get; set; } = "We wanted to check whether your issue was resolved. Reply anytime if you still need help.";
    public DateTime? DueAt { get; set; }
    public string? Channel { get; set; }
    public string? DedupKey { get; set; }
    public bool RespectBusinessHours { get; set; } = true;
}

public sealed class SupportTicketUpdateRequest
{
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string? Category { get; set; }
    public string? AssignedUserId { get; set; }
    public string? AssignedAgentKey { get; set; }
    public string? AiSummary { get; set; }
    public string? RecommendedAction { get; set; }
    public Guid? RelatedOrderId { get; set; }
}

public sealed class SupportTakeoverRequest
{
    public bool Enabled { get; set; } = true;
    public string? Note { get; set; }
}

public sealed record SupportChannelDto(
    string Channel,
    string DisplayName,
    string Status,
    bool AllowsInbound,
    bool AllowsOutbound,
    string Implementation,
    string? ExternalAccountId,
    DateTime? LastInboundAt,
    DateTime? LastOutboundAt,
    string? LastError);

public sealed record SupportMessageDto(
    Guid Id,
    Guid TicketId,
    string Channel,
    string Direction,
    string Body,
    string AuthorType,
    string? AuthorId,
    string DeliveryStatus,
    bool IsInternalNote,
    DateTime CreatedAt,
    DateTime? DeliveredAt,
    string? Error);

public sealed record SupportFollowUpDto(
    Guid Id,
    Guid TicketId,
    string Kind,
    string Status,
    DateTime DueAt,
    string Channel,
    string MessageTemplate,
    DateTime? SentAt,
    string? LastError);

public sealed record SupportTicketListItemDto(
    Guid Id,
    string TicketNumber,
    Guid? CustomerId,
    string? CustomerName,
    string Subject,
    string Category,
    string Priority,
    string Status,
    string Channel,
    string? AssignedAgentKey,
    string? AssignedUserId,
    string? AiSummary,
    string? RecommendedAction,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? EscalatedAt,
    DateTime? ResolvedAt,
    bool HumanTakeover,
    int MessageCount);

public sealed record SupportTicketDetailDto(
    Guid Id,
    string TicketNumber,
    Guid TenantId,
    Guid? CustomerId,
    string? CustomerName,
    string? CustomerEmail,
    string Subject,
    string Description,
    string Category,
    string Priority,
    string Status,
    string Channel,
    string? AssignedAgentKey,
    string? AssignedUserId,
    string? ExternalThreadId,
    string? AiSummary,
    string? RecommendedAction,
    string? Resolution,
    string? EscalationReason,
    string? EscalationSummary,
    Guid? RelatedOrderId,
    string? RelatedOrderNumber,
    bool HumanTakeover,
    bool MarketingConsentForHandoff,
    int ReopenCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? EscalatedAt,
    DateTime? ResolvedAt,
    DateTime? ClosedAt,
    IReadOnlyList<SupportMessageDto> Messages,
    IReadOnlyList<SupportFollowUpDto> FollowUps);

public sealed record SupportOverviewDto(
    int TotalTickets,
    int OpenTickets,
    int PendingTickets,
    int EscalatedTickets,
    int ResolvedTickets,
    double? AverageResolutionHours,
    int ActiveConversations,
    int PendingApprovals,
    int ConversationsHandled,
    int IssuesResolved,
    int Escalations,
    double? AverageResponseMinutes,
    double? ResolutionRate,
    int AiExecutions,
    decimal EstimatedAiCostUsd,
    bool CostIsEstimate,
    IReadOnlyList<SupportChannelDto> Channels);

public sealed record SupportProcessResult(
    Guid TicketId,
    string TicketNumber,
    string Status,
    string Summary,
    bool ResponseSent,
    string ResponseDeliveryStatus,
    bool Escalated,
    Guid? OutboundMessageId,
    JsonObject Data);

public interface ISupportChannelAdapter
{
    string Channel { get; }
    string DisplayName { get; }
    string Implementation { get; }
    Task<SupportChannelDto> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<(bool Performed, string DeliveryStatus, string? ExternalMessageId, string Message, string? Error)> SendAsync(
        SupportTicketDetailDto ticket,
        string body,
        CancellationToken cancellationToken = default);
}

public interface ISupportAgentService
{
    Task<SupportOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SupportTicketListItemDto>> ListTicketsAsync(string? status = null, string? priority = null, CancellationToken cancellationToken = default);
    Task<SupportTicketDetailDto?> GetTicketAsync(Guid ticketId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SupportChannelDto>> ListChannelsAsync(CancellationToken cancellationToken = default);
    Task<SupportProcessResult> ReceiveInboundAsync(SupportInboundMessageRequest request, CancellationToken cancellationToken = default);
    Task<SupportTicketDetailDto> UpdateTicketAsync(Guid ticketId, SupportTicketUpdateRequest request, CancellationToken cancellationToken = default);
    Task<SupportMessageDto> AddReplyAsync(Guid ticketId, SupportReplyRequest request, CancellationToken cancellationToken = default);
    Task<SupportTicketDetailDto> EscalateAsync(Guid ticketId, SupportEscalateRequest request, CancellationToken cancellationToken = default);
    Task<SupportTicketDetailDto> ResolveAsync(Guid ticketId, SupportResolveRequest request, CancellationToken cancellationToken = default);
    Task<SupportTicketDetailDto> ReopenAsync(Guid ticketId, string? note = null, CancellationToken cancellationToken = default);
    Task<SupportTicketDetailDto> TakeoverAsync(Guid ticketId, SupportTakeoverRequest request, CancellationToken cancellationToken = default);
    Task<SupportFollowUpDto> ScheduleFollowUpAsync(Guid ticketId, SupportFollowUpRequest request, CancellationToken cancellationToken = default);
    Task<int> ProcessDueFollowUpsAsync(CancellationToken cancellationToken = default);
    Task<JsonObject> SearchKnowledgeAsync(string? query, CancellationToken cancellationToken = default);
    Task<JsonObject> GetAuthorizedCustomerContextAsync(Guid? customerId, string? email, string? phone, Guid? orderId, string? orderNumber, CancellationToken cancellationToken = default);
}
