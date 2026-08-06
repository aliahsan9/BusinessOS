using BusinessOS.Domain.Common;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// A single step within an <see cref="AgentWorkflowRun"/> multi-step agent workflow.
/// </summary>
/// <remarks>
/// Scoped to the parent workflow run's tenant. Steps are ordered by <see cref="SortOrder"/>.
/// </remarks>
public class AgentWorkflowStep : AuditableEntity
{
    /// <summary>
    /// Foreign key to the parent <see cref="AgentWorkflowRun"/>. Required.
    /// </summary>
    public Guid WorkflowRunId { get; set; }

    /// <summary>
    /// Stable machine key identifying this step within the workflow definition.
    /// </summary>
    public string StepKey { get; set; } = default!;

    /// <summary>
    /// User-facing title describing what this step does.
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// Current execution status of this step. Defaults to Pending.
    /// </summary>
    public AgentWorkflowStepStatus Status { get; set; } = AgentWorkflowStepStatus.Pending;

    /// <summary>
    /// Zero-based sort order of this step within the parent workflow run.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Optional status or result message produced during step execution.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// UTC timestamp when step execution started, or null if not yet started.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// UTC timestamp when step execution completed, or null if not yet completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Navigation property to the parent workflow run.
    /// </summary>
    public AgentWorkflowRun WorkflowRun { get; set; } = default!;
}
