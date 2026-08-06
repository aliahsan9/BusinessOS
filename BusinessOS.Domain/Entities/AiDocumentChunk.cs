using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// A searchable text segment of an <see cref="AiDocument"/> with optional vector embedding data.
/// </summary>
/// <remarks>
/// Tenant-scoped. Belongs to a parent <see cref="AiDocument"/> via <see cref="DocumentId"/>.
/// Chunks are ordered by <see cref="ChunkIndex"/> within the parent document.
/// </remarks>
public class AiDocumentChunk : AuditableEntity
{
    /// <summary>
    /// Identifier of the tenant that owns this chunk. Required for multi-tenant isolation.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Foreign key to the parent <see cref="AiDocument"/>. Required.
    /// </summary>
    public Guid DocumentId { get; set; }

    /// <summary>
    /// Navigation property to the parent document.
    /// </summary>
    public AiDocument Document { get; set; } = default!;

    /// <summary>
    /// Zero-based index of this chunk within the parent document. Determines chunk ordering.
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// Text content of this chunk segment. Required.
    /// </summary>
    public string Content { get; set; } = default!;

    /// <summary>
    /// Optional serialized vector embedding for semantic search (stored as JSON).
    /// </summary>
    public string? EmbeddingJson { get; set; }

    /// <summary>
    /// Optional extracted keywords associated with this chunk for keyword-based retrieval.
    /// </summary>
    public string? Keywords { get; set; }

    /// <summary>
    /// Document type copied from the parent for denormalized filtering. Required.
    /// </summary>
    public string DocumentType { get; set; } = default!;

    /// <summary>
    /// Identity user identifier of the user who created the parent document. Required.
    /// </summary>
    public string CreatedByUserId { get; set; } = default!;

    /// <summary>
    /// Optional tags copied or derived from the parent document for filtering.
    /// </summary>
    public string? Tags { get; set; }
}
