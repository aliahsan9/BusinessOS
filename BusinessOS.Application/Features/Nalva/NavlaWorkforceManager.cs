namespace BusinessOS.Application.Features.Nalva;

/// <summary>
/// Navla-as-workforce-manager contracts (Phase 8B).
/// Chat and Command Center use this to plan/start multi-agent work without inventing outcomes.
/// </summary>
public interface INavlaWorkforceManager
{
    bool LooksLikeWorkforceDelegation(string message);
    Task<NavlaWorkforceDelegationResult> DelegateAsync(
        NavlaWorkforceDelegationRequest request,
        CancellationToken cancellationToken = default);
    Task<NalvaApplyWorkforcePackResult> ActivateCoreEmployeesAsync(
        CancellationToken cancellationToken = default);
}

public sealed class NavlaWorkforceDelegationRequest
{
    public string Message { get; set; } = "";
    public bool StartImmediately { get; set; } = true;
    public bool ActivateMissingAgents { get; set; } = true;
    public string? IdempotencyKey { get; set; }
    public string? ArgumentsJson { get; set; }
}

public sealed record NavlaWorkforceDelegationResult(
    bool Handled,
    string Summary,
    string? WorkflowKey,
    string? WorkflowName,
    double Confidence,
    IReadOnlyList<string> RequiredAgents,
    IReadOnlyList<string> MissingAgents,
    IReadOnlyList<string> ClarifyingQuestions,
    bool Started,
    Guid? WorkflowRunId,
    string? RunStatus,
    string? BlockReason,
    bool SimulatedLabel);
