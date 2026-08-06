using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Stores a single AI copilot prompt-response exchange for a tenant user.
/// </summary>
/// <remarks>
/// Tenant-scoped. May optionally belong to an <see cref="AiConversationSession"/> via <see cref="SessionId"/>.
/// Captures intent classification, tool usage, citations, and performance metrics for each turn.
/// </remarks>
public class AIConversation : AuditableEntity
{
    /// <summary>
    /// Identifier of the tenant that owns this conversation record. Required for multi-tenant isolation.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Identity user identifier of the user who submitted the prompt.
    /// </summary>
    public string UserId { get; set; } = default!;

    /// <summary>
    /// Optional foreign key to the parent <see cref="AiConversationSession"/>.
    /// </summary>
    public Guid? SessionId { get; set; }

    /// <summary>
    /// Navigation property to the parent conversation session, if any.
    /// </summary>
    public AiConversationSession? Session { get; set; }

    /// <summary>
    /// The user's input prompt sent to the AI copilot. Required.
    /// </summary>
    public string Prompt { get; set; } = default!;

    /// <summary>
    /// The AI-generated response returned for the prompt. Required.
    /// </summary>
    public string Response { get; set; } = default!;

    /// <summary>
    /// Optional classified intent label for the user's prompt (for example, query_orders).
    /// </summary>
    public string? Intent { get; set; }

    /// <summary>
    /// Optional JSON array describing tools invoked during response generation.
    /// </summary>
    public string? ToolsUsedJson { get; set; }

    /// <summary>
    /// Optional JSON array of document or source citations referenced in the response.
    /// </summary>
    public string? CitationsJson { get; set; }

    /// <summary>
    /// Optional total token count consumed for this exchange.
    /// </summary>
    public int? TokenUsage { get; set; }

    /// <summary>
    /// Optional wall-clock execution time in milliseconds for generating the response.
    /// </summary>
    public int? ExecutionTimeMs { get; set; }
}
