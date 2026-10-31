using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// A single role-based conversational message belonging to an <see cref="AiConversationSession"/>.
/// SQL Server is the durable source of truth for chat history.
/// </summary>
public class AiMessage : AuditableEntity
{
    public Guid TenantId { get; set; }

    public string UserId { get; set; } = default!;

    /// <summary>
    /// Parent conversation session identifier (also exposed as ConversationId in the API).
    /// </summary>
    public Guid ConversationId { get; set; }

    public AiConversationSession Conversation { get; set; } = default!;

    /// <summary>
    /// Message role: system, user, assistant, or tool. See <see cref="AiMessageRoles"/>.
    /// </summary>
    public string Role { get; set; } = default!;

    public string Content { get; set; } = default!;

    public int SequenceNumber { get; set; }

    public int? TokenCount { get; set; }

    /// <summary>
    /// Optional non-sensitive metadata (intent, tool name, client request id). Never store secrets.
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// Optional link to the prompt/response turn in <see cref="AIConversation"/>.
    /// </summary>
    public Guid? TurnId { get; set; }
}
