using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class AiDocument : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Title { get; set; } = default!;
    public string DocumentType { get; set; } = default!;
    public string? SourceEntityType { get; set; }
    public Guid? SourceEntityId { get; set; }
    public string Content { get; set; } = default!;
    public string? Tags { get; set; }
    public string CreatedByUserId { get; set; } = default!;
    public bool IsIndexed { get; set; }

    public ICollection<AiDocumentChunk> Chunks { get; set; } = [];
}
