using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Persists an AI copilot conversation session with page context and selected entity references.
/// </summary>
/// <remarks>
/// Tenant-scoped. Owns a collection of <see cref="AIConversation"/> message records.
/// Stores optional UI context (current page, selected customer, project, order, or invoice) and session memory.
/// </remarks>
public class AiConversationSession : AuditableEntity
{
    /// <summary>
    /// Identifier of the tenant that owns this conversation session. Required for multi-tenant isolation.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Identity user identifier of the user participating in this session.
    /// </summary>
    public string UserId { get; set; } = default!;

    /// <summary>
    /// User-facing title for the conversation. Defaults to "New conversation".
    /// </summary>
    public string Title { get; set; } = "New conversation";

    /// <summary>
    /// Optional route or page identifier indicating where the user was when the session was active.
    /// </summary>
    public string? CurrentPage { get; set; }

    /// <summary>
    /// Optional foreign key to a <see cref="Customer"/> selected as session context.
    /// </summary>
    public Guid? SelectedCustomerId { get; set; }

    /// <summary>
    /// Optional foreign key to a <see cref="Project"/> selected as session context.
    /// </summary>
    public Guid? SelectedProjectId { get; set; }

    /// <summary>
    /// Optional foreign key to an <see cref="Order"/> selected as session context.
    /// </summary>
    public Guid? SelectedOrderId { get; set; }

    /// <summary>
    /// Optional foreign key to an <see cref="Invoice"/> selected as session context.
    /// </summary>
    public Guid? SelectedInvoiceId { get; set; }

    /// <summary>
    /// Optional serialized session memory or state used to maintain conversational context across turns.
    /// </summary>
    public string? MemoryJson { get; set; }

    /// <summary>
    /// UTC timestamp of the most recent activity in this session. Defaults to the current UTC time on creation.
    /// </summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Indicates whether this session is currently active. Defaults to true.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Collection of individual prompt-response exchanges in this session.
    /// </summary>
    public ICollection<AIConversation> Messages { get; set; } = [];
}
