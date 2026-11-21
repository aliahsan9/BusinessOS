namespace BusinessOS.Application.Features.Nalva;

/// <summary>Cross-tenant recovery for stalled autonomous work after process crashes/restarts.</summary>
public interface INalvaReliabilityService
{
    /// <summary>Requeues or dead-letters stalled Running tasks/events/executions and expires approvals.</summary>
    Task<NalvaRecoveryResult> RecoverStalledWorkAsync(CancellationToken cancellationToken = default);
}

public sealed record NalvaRecoveryResult(
    int TasksRecovered,
    int ExecutionsClosed,
    int EventsRecovered,
    int ApprovalsExpired);
