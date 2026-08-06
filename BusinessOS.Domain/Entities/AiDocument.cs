using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Source document indexed for AI retrieval-augmented generation (RAG) within a tenant knowledge base.
/// </summary>
/// <remarks>
/// Tenant-scoped. Owns a collection of <see cref="AiDocumentChunk"/> records used for vector search.
/// May optionally reference a source business entity via <see cref="SourceEntityType"/> and <see cref="SourceEntityId"/>.
/// </remarks>
public class AiDocument : AuditableEntity
{
    /// <summary>
    /// Identifier of the tenant that owns this document. Required for multi-tenant isolation.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Human-readable title of the document. Required.
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// Category or type label for the document (for example, policy, faq, invoice). Required.
    /// </summary>
    public string DocumentType { get; set; } = default!;

    /// <summary>
    /// Optional name of the source business entity type when this document was derived from another entity.
    /// </summary>
    public string? SourceEntityType { get; set; }

    /// <summary>
    /// Optional primary key of the source business entity when this document was derived from another entity.
    /// </summary>
    public Guid? SourceEntityId { get; set; }

    /// <summary>
    /// Full text content of the document used for chunking and indexing. Required.
    /// </summary>
    public string Content { get; set; } = default!;

    /// <summary>
    /// Optional comma-separated or serialized tags for filtering and organization.
    /// </summary>
    public string? Tags { get; set; }

    /// <summary>
    /// Identity user identifier of the user who created or uploaded the document. Required.
    /// </summary>
    public string CreatedByUserId { get; set; } = default!;

    /// <summary>
    /// Indicates whether the document has been chunked and indexed for vector search.
    /// </summary>
    public bool IsIndexed { get; set; }

    /// <summary>
    /// Collection of text chunks derived from this document for embedding and retrieval.
    /// </summary>
    public ICollection<AiDocumentChunk> Chunks { get; set; } = [];
}
