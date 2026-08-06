using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Audit log entry capturing AI copilot interactions, tool usage, and outcomes for compliance and debugging.
/// </summary>
/// <remarks>
/// Tenant-scoped. Records each copilot invocation including intent, tools, retrieved documents, and success or failure.
/// </remarks>
public class AiCopilotAuditLog : AuditableEntity
{
    /// <summary>
    /// Identifier of the tenant where the copilot interaction occurred. Required for multi-tenant isolation.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Identity user identifier of the user who triggered the copilot interaction.
    /// </summary>
    public string UserId { get; set; } = default!;

    /// <summary>
    /// Optional foreign key to the related <see cref="AiConversationSession"/>.
    /// </summary>
    public Guid? SessionId { get; set; }

    /// <summary>
    /// Classified intent label for the user's request. Required.
    /// </summary>
    public string Intent { get; set; } = default!;

    /// <summary>
    /// Optional copy of the user's message that triggered the interaction.
    /// </summary>
    public string? UserMessage { get; set; }

    /// <summary>
    /// Optional JSON array describing tools invoked during the interaction.
    /// </summary>
    public string? ToolsUsedJson { get; set; }

    /// <summary>
    /// Optional JSON array of documents retrieved from the knowledge base during the interaction.
    /// </summary>
    public string? RetrievedDocumentsJson { get; set; }

    /// <summary>
    /// Wall-clock execution time in milliseconds for the copilot request.
    /// </summary>
    public int ExecutionTimeMs { get; set; }

    /// <summary>
    /// Optional total token count consumed during the interaction.
    /// </summary>
    public int? TokenUsage { get; set; }

    /// <summary>
    /// Indicates whether the copilot interaction completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message describing the failure, if <see cref="Success"/> is false.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
