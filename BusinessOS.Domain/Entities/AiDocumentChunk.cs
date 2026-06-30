using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class AiDocumentChunk : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public AiDocument Document { get; set; } = default!;
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = default!;
    public string? EmbeddingJson { get; set; }
    public string? Keywords { get; set; }
    public string DocumentType { get; set; } = default!;
    public string CreatedByUserId { get; set; } = default!;
    public string? Tags { get; set; }
}
