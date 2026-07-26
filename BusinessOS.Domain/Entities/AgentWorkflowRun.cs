using BusinessOS.Domain.Common;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// A multi-step autonomous workflow executed by an AI employee agent.
/// </summary>
public class AgentWorkflowRun : AuditableEntity
{
    public Guid TenantId { get; set; }

    public string UserId { get; set; } = default!;

    public Guid? SessionId { get; set; }

    public string AgentKey { get; set; } = default!;

    public string Title { get; set; } = default!;

    public AgentWorkflowStatus Status { get; set; } = AgentWorkflowStatus.Pending;

    public int CurrentStepIndex { get; set; }

    /// <summary>Serialized progress snapshot for clients and resume logic.</summary>
    public string? ProgressJson { get; set; }

    public string? ResultSummary { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public ICollection<AgentWorkflowStep> Steps { get; set; } = new List<AgentWorkflowStep>();
}
