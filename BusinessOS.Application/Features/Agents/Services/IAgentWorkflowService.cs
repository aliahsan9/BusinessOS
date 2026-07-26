using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Features.Agents.Services;

/// <summary>
/// Creates and updates AI employee workflow runs and step progress.
/// </summary>
public interface IAgentWorkflowService
{
    Task<AgentWorkflowDto> CreateFromPlanAsync(
        AgentWorkflowPlanDto plan,
        string userId,
        Guid? sessionId = null,
        CancellationToken cancellationToken = default);

    Task<AgentWorkflowDto?> GetAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AgentWorkflowSummaryDto>> ListRecentAsync(
        string userId,
        int limit = 20,
        CancellationToken cancellationToken = default);

    Task<AgentWorkflowDto> StartAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default);

    Task<AgentWorkflowStepDto> BeginStepAsync(
        Guid workflowId,
        string stepKey,
        string? message = null,
        CancellationToken cancellationToken = default);

    Task<AgentWorkflowStepDto> CompleteStepAsync(
        Guid workflowId,
        string stepKey,
        string? message = null,
        CancellationToken cancellationToken = default);

    Task<AgentWorkflowStepDto> FailStepAsync(
        Guid workflowId,
        string stepKey,
        string errorMessage,
        CancellationToken cancellationToken = default);

    Task<AgentWorkflowStepDto> SkipStepAsync(
        Guid workflowId,
        string stepKey,
        string? message = null,
        CancellationToken cancellationToken = default);

    Task<AgentWorkflowDto> CompleteAsync(
        Guid workflowId,
        string? resultSummary = null,
        CancellationToken cancellationToken = default);

    Task<AgentWorkflowDto> FailAsync(
        Guid workflowId,
        string errorMessage,
        CancellationToken cancellationToken = default);

    Task<AgentWorkflowDto> CancelAsync(
        Guid workflowId,
        string? reason = null,
        CancellationToken cancellationToken = default);

    Task UpdateProgressJsonAsync(
        Guid workflowId,
        string progressJson,
        CancellationToken cancellationToken = default);

    Task SetStatusAsync(
        Guid workflowId,
        AgentWorkflowStatus status,
        CancellationToken cancellationToken = default);
}
