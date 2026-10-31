using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Services;

namespace BusinessOS.Infrastructure.Services;

public sealed class AiAssistantService : IAiAssistantService
{
    private readonly IAiChatService _chatService;
    private readonly IAiCopilotOrchestrator _copilot;
    private readonly IAiMemoryService _memory;
    private readonly IAiInsightService _insights;
    private readonly IAiObservabilityService _observability;
    private readonly IAiAnalyticsQueryService _analytics;

    public AiAssistantService(
        IAiChatService chatService,
        IAiCopilotOrchestrator copilot,
        IAiMemoryService memory,
        IAiInsightService insights,
        IAiObservabilityService observability,
        IAiAnalyticsQueryService analytics)
    {
        _chatService = chatService;
        _copilot = copilot;
        _memory = memory;
        _insights = insights;
        _observability = observability;
        _analytics = analytics;
    }

    public Task<AiChatResponse> ChatAsync(AiChatRequest request, CancellationToken cancellationToken = default) =>
        _chatService.ChatAsync(request, cancellationToken);

    public Task<AiCopilotChatResponse> CopilotChatAsync(AiCopilotChatRequest request, CancellationToken cancellationToken = default) =>
        _copilot.ProcessAsync(request, cancellationToken);

    public IAsyncEnumerable<AiStreamChunkDto> CopilotStreamAsync(AiCopilotChatRequest request, CancellationToken cancellationToken = default) =>
        _copilot.ProcessStreamAsync(request, cancellationToken);

    public async Task<AiConversationSessionDto> CreateConversationAsync(CancellationToken cancellationToken = default)
    {
        var id = await _memory.CreateSessionAsync(cancellationToken: cancellationToken);
        return await _memory.GetSessionAsync(id, cancellationToken)
            ?? new AiConversationSessionDto { Id = id, Title = "New conversation", LastActivityAt = DateTime.UtcNow };
    }

    public Task<IReadOnlyList<AiConversationSessionDto>> ListConversationsAsync(int limit = 20, CancellationToken cancellationToken = default) =>
        _memory.ListSessionsAsync(limit, cancellationToken);

    public Task<IReadOnlyList<AiConversationSessionDto>> ListConversationsAsync(int skip, int limit, CancellationToken cancellationToken = default) =>
        _memory.ListSessionsAsync(skip, limit, cancellationToken);

    public Task<AiConversationSessionDto?> GetConversationSessionAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        _memory.GetSessionAsync(sessionId, cancellationToken);

    public Task<IReadOnlyList<AiConversationMessageDto>> GetConversationAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        _memory.GetSessionMessagesAsync(sessionId, cancellationToken);

    public Task<IReadOnlyList<AiConversationMessageDto>> GetConversationAsync(Guid sessionId, int skip, int take, CancellationToken cancellationToken = default) =>
        _memory.GetSessionMessagesAsync(sessionId, skip, take, cancellationToken);

    public Task<bool> DeleteConversationAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        _memory.DeleteSessionAsync(sessionId, cancellationToken);

    public Task<AiDashboardCopilotDto> GetDashboardCopilotAsync(CancellationToken cancellationToken = default) =>
        _insights.GetDashboardCopilotAsync(cancellationToken);

    public Task<IReadOnlyList<AiProactiveInsightDto>> GetInsightsAsync(CancellationToken cancellationToken = default) =>
        _insights.GetProactiveInsightsAsync(cancellationToken);

    public Task<AiDiagnosticsSummaryDto> GetDiagnosticsAsync(DateTime? since = null, CancellationToken cancellationToken = default) =>
        _observability.GetDiagnosticsAsync(since, cancellationToken);

    public Task<AiAnalyticsQueryResponse> RunAnalyticsQueryAsync(AiAnalyticsQueryRequest request, CancellationToken cancellationToken = default) =>
        _analytics.ExecuteAsync(request.QueryType, request.StartDate, request.EndDate, request.Period, request.Top, cancellationToken);
}
