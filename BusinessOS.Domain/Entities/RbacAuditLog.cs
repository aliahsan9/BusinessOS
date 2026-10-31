using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class RbacAuditLog : BaseEntity
{
    /// <summary>
    /// Owning tenant. Null is reserved for platform events that are not tenant data.
    /// </summary>
    public Guid? TenantId { get; set; }

    public string ActorUserId { get; set; } = default!;

    public string Action { get; set; } = default!;

    public string EntityType { get; set; } = default!;

    public string EntityId { get; set; } = default!;

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
