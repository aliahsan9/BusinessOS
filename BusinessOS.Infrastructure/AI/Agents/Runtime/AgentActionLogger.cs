using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.Agents.Services;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.AI.Services;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.AI.Agents.Runtime;

public sealed class AgentActionLogger : IAgentActionLogger
{
    private readonly IAiObservabilityService _observability;
    private readonly ILogger<AgentActionLogger> _logger;

    public AgentActionLogger(
        IAiObservabilityService observability,
        ILogger<AgentActionLogger> logger)
    {
        _observability = observability;
        _logger = logger;
    }

    public async Task LogAsync(AgentActionLogEntry entry, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "AgentAction intent={Intent} tool={Tool} success={Success} ms={Ms} workflow={Workflow} step={Step} reason={Reason}",
            entry.Intent,
            entry.ToolName,
            entry.Success,
            entry.ExecutionTimeMs,
            entry.WorkflowId,
            entry.StepKey,
            entry.FailureReason);

        try
        {
            await _observability.LogAsync(
                entry.SessionId,
                entry.Intent,
                entry.ToolName,
                entry.ToolName is null ? [] : [entry.ToolName],
                [],
                (int)entry.ExecutionTimeMs,
                null,
                entry.Success,
                entry.FailureReason,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Agent action observability log failed");
        }
    }
}
