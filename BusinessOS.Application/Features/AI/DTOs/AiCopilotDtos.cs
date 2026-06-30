using BusinessOS.Application.Features.AI.Enums;

namespace BusinessOS.Application.Features.AI.DTOs;

public record AiCopilotChatRequest(
    string Message,
    string? CurrentPage = null,
    string? SearchQuery = null,
    Guid? CustomerId = null,
    Guid? OrderId = null,
    Guid? InvoiceId = null,
    Guid? ProjectId = null,
    Guid? SessionId = null,
    bool Stream = false);

public sealed class AiCopilotChatResponse
{
    public string Reply { get; init; } = default!;
    public Guid? SessionId { get; init; }
    public AiCopilotIntent Intent { get; init; }
    public IReadOnlyList<string> ToolsUsed { get; init; } = [];
    public IReadOnlyList<AiCitationDto> Citations { get; init; } = [];
    public IReadOnlyList<AiSuggestionDto> Suggestions { get; init; } = [];
    public IReadOnlyList<AiQuickActionDto> QuickActions { get; init; } = [];
    public IReadOnlyList<AiSearchResultDto> SearchResults { get; init; } = [];
    public AiRetrievedSourcesDto Sources { get; init; } = new();
    public AiActionResultDto? ActionResult { get; init; }
    public AiCopilotDiagnosticsDto? Diagnostics { get; init; }
    public bool PermissionDenied { get; init; }
}

public sealed class AiCitationDto
{
    public string Title { get; init; } = default!;
    public string DocumentType { get; init; } = default!;
    public string? SourceId { get; init; }
    public string? Excerpt { get; init; }
    public double Score { get; init; }
}

public sealed class AiCopilotDiagnosticsDto
{
    public string Intent { get; init; } = default!;
    public IReadOnlyList<string> ToolsUsed { get; init; } = [];
    public int ExecutionTimeMs { get; init; }
    public int? TokenUsage { get; init; }
    public int RetrievedDocuments { get; init; }
    public bool UsedLlm { get; init; }
}

public sealed class AiConversationSessionDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = default!;
    public DateTime LastActivityAt { get; init; }
    public int MessageCount { get; init; }
}

public sealed class AiConversationMessageDto
{
    public Guid Id { get; init; }
    public string Prompt { get; init; } = default!;
    public string Response { get; init; } = default!;
    public string? Intent { get; init; }
    public IReadOnlyList<string> ToolsUsed { get; init; } = [];
    public IReadOnlyList<AiCitationDto> Citations { get; init; } = [];
    public DateTime CreatedAt { get; init; }
}

public sealed class AiProactiveInsightDto
{
    public string Type { get; init; } = default!;
    public string Severity { get; init; } = default!;
    public string Title { get; init; } = default!;
    public string Message { get; init; } = default!;
    public string? ActionRoute { get; init; }
    public string? ActionLabel { get; init; }
}

public sealed class AiDashboardCopilotDto
{
    public string Summary { get; init; } = default!;
    public IReadOnlyList<AiProactiveInsightDto> Insights { get; init; } = [];
    public IReadOnlyList<AiSuggestionDto> FocusAreas { get; init; } = [];
}

public sealed class AiDiagnosticsSummaryDto
{
    public int TotalRequests { get; init; }
    public int SuccessfulRequests { get; init; }
    public int FailedRequests { get; init; }
    public double AverageExecutionTimeMs { get; init; }
    public int TotalTokenUsage { get; init; }
    public IReadOnlyList<AiDiagnosticsIntentBreakdownDto> IntentBreakdown { get; init; } = [];
    public IReadOnlyList<AiDiagnosticsToolBreakdownDto> ToolBreakdown { get; init; } = [];
    public IReadOnlyList<AiCopilotAuditEntryDto> RecentLogs { get; init; } = [];
}

public sealed class AiDiagnosticsIntentBreakdownDto
{
    public string Intent { get; init; } = default!;
    public int Count { get; init; }
}

public sealed class AiDiagnosticsToolBreakdownDto
{
    public string Tool { get; init; } = default!;
    public int Count { get; init; }
}

public sealed class AiCopilotAuditEntryDto
{
    public Guid Id { get; init; }
    public string Intent { get; init; } = default!;
    public string? UserMessage { get; init; }
    public IReadOnlyList<string> ToolsUsed { get; init; } = [];
    public int ExecutionTimeMs { get; init; }
    public int? TokenUsage { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class AiAnalyticsQueryRequest
{
    public string QueryType { get; init; } = default!;
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string? Period { get; init; }
    public int? Top { get; init; }
}

public sealed class AiAnalyticsQueryResponse
{
    public string QueryType { get; init; } = default!;
    public object Data { get; init; } = default!;
    public string PeriodLabel { get; init; } = default!;
}

public sealed class AiCopilotExecutionContext
{
    public AiChatRequest Request { get; init; } = default!;
    public AiPageContextDto Page { get; init; } = default!;
    public AiCopilotIntent Intent { get; init; }
    public Guid SessionId { get; init; }
    public AiMemoryStateDto Memory { get; init; } = new();
    public string Message { get; init; } = default!;
}

public sealed class AiMemoryStateDto
{
    public Guid? SelectedCustomerId { get; init; }
    public string? SelectedCustomerName { get; init; }
    public Guid? SelectedProjectId { get; init; }
    public Guid? SelectedOrderId { get; init; }
    public Guid? SelectedInvoiceId { get; init; }
    public string? LastIntent { get; init; }
    public string? LastAnalyticsQuery { get; init; }
    public IReadOnlyList<AiMemoryTurnDto> RecentTurns { get; init; } = [];
}

public sealed class AiMemoryTurnDto
{
    public string Role { get; init; } = default!;
    public string Content { get; init; } = default!;
}

public sealed class AiToolResult
{
    public string ToolName { get; init; } = default!;
    public bool Success { get; init; } = true;
    public object? Data { get; init; }
    public string Summary { get; init; } = default!;
    public AiActionResultDto? ActionResult { get; init; }
    public IReadOnlyList<AiCitationDto> Citations { get; init; } = [];
}

public sealed class AiIntentDetectionResult
{
    public AiCopilotIntent Intent { get; init; }
    public double Confidence { get; init; }
    public IReadOnlyList<AiToolName> SuggestedTools { get; init; } = [];
}

public sealed class AiPermissionCheckResult
{
    public bool Allowed { get; init; }
    public string? DenialReason { get; init; }
    public IReadOnlyList<string> MissingPermissions { get; init; } = [];
}

public sealed class AiStreamChunkDto
{
    public string Type { get; init; } = "token";
    public string? Content { get; init; }
    public AiCopilotChatResponse? FinalResponse { get; init; }
}
