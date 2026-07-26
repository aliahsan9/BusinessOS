using BusinessOS.Application.Features.Agents.DTOs;

namespace BusinessOS.Application.Features.Agents.Services;

/// <summary>
/// Conversational onboarding flow driven by an AI employee agent.
/// </summary>
public interface IAgentOnboardingOrchestrator
{
    Task<AgentOnboardingResponse> StartAsync(
        AgentOnboardingStartRequest request,
        CancellationToken cancellationToken = default);

    Task<AgentOnboardingResponse> ContinueAsync(
        AgentOnboardingContinueRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current onboarding conversation state for the user, if any.
    /// </summary>
    Task<AgentOnboardingResponse?> GetCurrentStateAsync(
        Guid? sessionId = null,
        CancellationToken cancellationToken = default);
}
