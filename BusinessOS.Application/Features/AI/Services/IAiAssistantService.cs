using BusinessOS.Application.Features.AI.DTOs;

namespace BusinessOS.Application.Features.AI.Services;

public interface IAiAssistantService
{
    Task<AiChatResponse> ChatAsync(AiChatRequest request, CancellationToken cancellationToken = default);

    Task<AiCopilotChatResponse> CopilotChatAsync(
        AiCopilotChatRequest request,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<AiStreamChunkDto> CopilotStreamAsync(
        AiCopilotChatRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiConversationSessionDto>> ListConversationsAsync(
        int limit = 20,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiConversationMessageDto>> GetConversationAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<AiDashboardCopilotDto> GetDashboardCopilotAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiProactiveInsightDto>> GetInsightsAsync(CancellationToken cancellationToken = default);

    Task<AiDiagnosticsSummaryDto> GetDiagnosticsAsync(
        DateTime? since = null,
        CancellationToken cancellationToken = default);

    Task<AiAnalyticsQueryResponse> RunAnalyticsQueryAsync(
        AiAnalyticsQueryRequest request,
        CancellationToken cancellationToken = default);
}
