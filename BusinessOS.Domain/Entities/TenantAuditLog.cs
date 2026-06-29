using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class TenantAuditLog : BaseEntity
{
    public Guid TenantId { get; set; }
    public string? ActorUserId { get; set; }
    public string Action { get; set; } = default!;
    public string? EntityType { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = default!;
}
