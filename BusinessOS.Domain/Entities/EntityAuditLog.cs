using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class EntityAuditLog : BaseEntity
{
    public Guid TenantId { get; set; }
    public string ChangedBy { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public string EntityId { get; set; } = default!;
    public string Action { get; set; } = default!;
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
