using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class StoredFile : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string OwnerUserId { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public long SizeBytes { get; set; }
    public string StoragePath { get; set; } = default!;
}
