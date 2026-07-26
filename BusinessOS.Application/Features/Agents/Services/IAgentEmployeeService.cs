using BusinessOS.Application.Features.Agents.DTOs;

namespace BusinessOS.Application.Features.Agents.Services;

/// <summary>
/// Primary facade for AI employee agent interactions (chat, voice, onboarding, workflows).
/// </summary>
public interface IAgentEmployeeService
{
    Task<AgentChatResponse> ChatAsync(
        AgentChatRequest request,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<AgentStreamChunkDto> ChatStreamAsync(
        AgentChatRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AgentEmployeeDto>> ListEmployeesAsync(
        CancellationToken cancellationToken = default);

    Task<VoicePreferenceDto> GetVoicePreferencesAsync(
        CancellationToken cancellationToken = default);

    Task<VoicePreferenceDto> SaveVoicePreferencesAsync(
        SaveVoicePreferenceRequest request,
        CancellationToken cancellationToken = default);

    Task<AgentOnboardingResponse> StartOnboardingWithAgentAsync(
        AgentOnboardingStartRequest request,
        CancellationToken cancellationToken = default);

    Task<AgentOnboardingResponse> ContinueOnboardingAsync(
        AgentOnboardingContinueRequest request,
        CancellationToken cancellationToken = default);

    Task<AgentWorkflowDto?> GetWorkflowAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AgentWorkflowSummaryDto>> ListRecentWorkflowsAsync(
        int limit = 20,
        CancellationToken cancellationToken = default);

    Task<AskSophiaSuggestionsDto> GetAskSophiaSuggestionsAsync(
        CancellationToken cancellationToken = default);
}
