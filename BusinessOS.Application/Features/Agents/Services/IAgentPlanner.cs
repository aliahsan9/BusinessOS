using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;

namespace BusinessOS.Application.Features.Agents.Services;

/// <summary>
/// Plans multi-step autonomous workflows from detected intent and conversation context.
/// </summary>
public interface IAgentPlanner
{
    /// <summary>
    /// Returns true when the intent (or message) should be executed as a multi-step workflow.
    /// </summary>
    bool RequiresWorkflow(
        AiCopilotIntent intent,
        string message,
        AiMemoryStateDto memory);

    /// <summary>
    /// Builds an ordered plan of steps for the given intent and agent.
    /// </summary>
    AgentWorkflowPlanDto Plan(
        string agentKey,
        AiCopilotIntent intent,
        string message,
        AiPageContextDto page,
        AiMemoryStateDto memory);

    /// <summary>
    /// Plans a conversational onboarding workflow for the given agent.
    /// </summary>
    AgentWorkflowPlanDto PlanOnboarding(string agentKey, string? language);
}
