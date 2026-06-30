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
    bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory);
    Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default);
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
    Task<Guid> GetOrCreateSessionAsync(AiChatRequest request, Guid? sessionId = null, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid sessionId, AiChatRequest request, AiCopilotIntent intent, string userMessage, string assistantReply, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiConversationSessionDto>> ListSessionsAsync(int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiConversationMessageDto>> GetSessionMessagesAsync(Guid sessionId, CancellationToken cancellationToken = default);
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
