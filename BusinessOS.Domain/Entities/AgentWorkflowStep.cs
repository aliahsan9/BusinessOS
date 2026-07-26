using BusinessOS.Domain.Common;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// A single step within an <see cref="AgentWorkflowRun"/>.
/// </summary>
public class AgentWorkflowStep : AuditableEntity
{
    public Guid WorkflowRunId { get; set; }

    public string StepKey { get; set; } = default!;

    public string Title { get; set; } = default!;

    public AgentWorkflowStepStatus Status { get; set; } = AgentWorkflowStepStatus.Pending;

    public int SortOrder { get; set; }

    public string? Message { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public AgentWorkflowRun WorkflowRun { get; set; } = default!;
}
