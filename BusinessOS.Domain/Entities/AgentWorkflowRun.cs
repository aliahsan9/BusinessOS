using BusinessOS.Domain.Common;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Represents a multi-step autonomous workflow executed by an AI employee agent within a tenant.
/// </summary>
/// <remarks>
/// Tenant-scoped. Owns an ordered collection of <see cref="AgentWorkflowStep"/> records.
/// May optionally link to an AI conversation session for context continuity.
/// </remarks>
public class AgentWorkflowRun : AuditableEntity
{
    /// <summary>
    /// Identifier of the tenant that owns this workflow run. Required for multi-tenant isolation.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Identity user identifier of the user who initiated the workflow.
    /// </summary>
    public string UserId { get; set; } = default!;

    /// <summary>
    /// Optional foreign key to the related <see cref="AiConversationSession"/> for conversational context.
    /// </summary>
    public Guid? SessionId { get; set; }

    /// <summary>
    /// Machine key of the AI agent executing this workflow. References <see cref="AgentProfile.Key"/>.
    /// </summary>
    public string AgentKey { get; set; } = default!;

    /// <summary>
    /// User-facing title describing the purpose or outcome of the workflow.
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// Current lifecycle status of the workflow run. Defaults to Pending.
    /// </summary>
    public AgentWorkflowStatus Status { get; set; } = AgentWorkflowStatus.Pending;

    /// <summary>
    /// Zero-based index of the step currently being executed within the workflow.
    /// </summary>
    public int CurrentStepIndex { get; set; }

    /// <summary>
    /// Serialized progress snapshot for client display and resume logic after interruption.
    /// </summary>
    public string? ProgressJson { get; set; }

    /// <summary>
    /// Human-readable summary of the workflow outcome when completed successfully.
    /// </summary>
    public string? ResultSummary { get; set; }

    /// <summary>
    /// Error message describing why the workflow failed, if applicable.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// UTC timestamp when the workflow run started. Defaults to the current UTC time on creation.
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// UTC timestamp when the workflow run completed, or null if still in progress.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Ordered collection of steps belonging to this workflow run.
    /// </summary>
    public ICollection<AgentWorkflowStep> Steps { get; set; } = new List<AgentWorkflowStep>();
}
