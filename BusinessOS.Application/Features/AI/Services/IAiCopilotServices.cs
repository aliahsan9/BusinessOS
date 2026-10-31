using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;

namespace BusinessOS.Application.Features.AI.Services;

public interface IAiCopilotOrchestrator
{
    Task<AiCopilotChatResponse> ProcessAsync(
        AiCopilotChatRequest request,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<AiStreamChunkDto> ProcessStreamAsync(
        AiCopilotChatRequest request,
        CancellationToken cancellationToken = default);
}

public interface IAiIntentDetector
{
    AiIntentDetectionResult Detect(string message, AiPageContextDto page, AiMemoryStateDto memory);
}

public interface IAiPermissionValidator
{
    AiPermissionCheckResult ValidateIntent(AiCopilotIntent intent, IReadOnlyList<AiToolName> tools);

    AiPermissionCheckResult ValidateTool(AiToolName tool);
}

public interface IAiTool
{
    AiToolName ToolName { get; }
    string Description { get; }
    IReadOnlyList<string> RequiredPermissions { get; }

    /// <summary>
    /// JSON schema description of accepted arguments for structured extraction.
    /// Null means the tool parses the raw message itself (legacy).
    /// </summary>
    string? ParameterSchemaJson { get; }

    bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory);

    Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute with pre-extracted JSON args. Default implementations fall back to <see cref="ExecuteAsync"/>.
    /// </summary>
    Task<AiToolResult> ExecuteWithArgsAsync(
        AiCopilotExecutionContext context,
        System.Text.Json.JsonElement args,
        CancellationToken cancellationToken = default);
}

public interface IAiToolRegistry
{
    IReadOnlyList<IAiTool> AllTools { get; }
    IReadOnlyList<IAiTool> SelectTools(AiIntentDetectionResult intent, string message, AiPageContextDto page, AiMemoryStateDto memory);
}

public interface IAiAnalyticsQueryService
{
    Task<AiAnalyticsQueryResponse> ExecuteAsync(
        string queryType,
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        int? top,
        CancellationToken cancellationToken = default);

    IReadOnlyList<string> SupportedQueryTypes { get; }
}

public interface IAiMemoryService
{
    Task<AiMemoryStateDto> LoadAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<ConversationContext> GetContextAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task<Guid> GetOrCreateSessionAsync(AiChatRequest request, Guid? sessionId = null, CancellationToken cancellationToken = default);
    Task<Guid> CreateSessionAsync(string? title = null, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid sessionId, AiChatRequest request, AiCopilotIntent intent, string userMessage, string assistantReply, CancellationToken cancellationToken = default);
    Task CompleteTurnAsync(
        Guid sessionId,
        AiChatRequest request,
        AiCopilotIntent intent,
        string userMessage,
        string assistantReply,
        IReadOnlyList<string>? toolsUsed = null,
        IReadOnlyList<AiCitationDto>? citations = null,
        int? executionMs = null,
        int? tokenUsage = null,
        string? clientRequestId = null,
        IReadOnlyList<AiToolResult>? toolResults = null,
        CancellationToken cancellationToken = default,
        bool updatePendingAction = false,
        string? pendingActionJson = null);
    Task SetPendingActionAsync(Guid sessionId, string? pendingActionJson, CancellationToken cancellationToken = default);
    Task AppendMessageAsync(Guid conversationId, ChatMessageInput message, CancellationToken cancellationToken = default);
    Task UpdateSummaryAsync(Guid conversationId, string summary, int summarizedThroughSequence, CancellationToken cancellationToken = default);
    Task<AiConversationTurnLookupDto?> FindTurnByClientRequestIdAsync(Guid sessionId, string clientRequestId, CancellationToken cancellationToken = default);
    Task<AiConversationSessionDto?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiConversationSessionDto>> ListSessionsAsync(int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiConversationSessionDto>> ListSessionsAsync(int skip, int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiConversationMessageDto>> GetSessionMessagesAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiConversationMessageDto>> GetSessionMessagesAsync(Guid sessionId, int skip, int take, CancellationToken cancellationToken = default);
    Task<bool> DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

public sealed class AiConversationTurnLookupDto
{
    public string UserMessage { get; init; } = default!;
    public string AssistantReply { get; init; } = default!;
}

public interface IAiConversationSummarizer
{
    Task TrySummarizeAsync(Guid conversationId, CancellationToken cancellationToken = default);
}

public interface IAiVectorRagService
{
    Task IndexDocumentAsync(
        string title,
        string documentType,
        string content,
        string? tags,
        string? sourceEntityType,
        Guid? sourceEntityId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiCitationDto>> SearchAsync(
        string query,
        string? documentType,
        int top,
        CancellationToken cancellationToken = default);
}

public interface IAiObservabilityService
{
    Task LogAsync(
        Guid? sessionId,
        AiCopilotIntent intent,
        string? userMessage,
        IReadOnlyList<string> toolsUsed,
        IReadOnlyList<AiCitationDto> citations,
        int executionTimeMs,
        int? tokenUsage,
        bool success,
        string? errorMessage,
        CancellationToken cancellationToken = default);

    Task<AiDiagnosticsSummaryDto> GetDiagnosticsAsync(
        DateTime? since,
        CancellationToken cancellationToken = default);
}

public interface IAiInsightService
{
    Task<IReadOnlyList<AiProactiveInsightDto>> GetProactiveInsightsAsync(CancellationToken cancellationToken = default);
    Task<AiDashboardCopilotDto> GetDashboardCopilotAsync(CancellationToken cancellationToken = default);
}
