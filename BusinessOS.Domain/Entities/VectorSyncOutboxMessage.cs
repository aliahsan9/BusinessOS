using BusinessOS.Domain.Common;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

public class VectorSyncOutboxMessage : BaseEntity
{
    public Guid TenantId { get; set; }
    public string EntityType { get; set; } = default!;
    public Guid EntityId { get; set; }
    public VectorSyncOperation Operation { get; set; }
    public string? PayloadJson { get; set; }
    public VectorSyncStatus Status { get; set; } = VectorSyncStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
}
