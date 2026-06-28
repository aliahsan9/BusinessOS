using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class RbacAuditLog : BaseEntity
{
    public string ActorUserId { get; set; } = default!;

    public string Action { get; set; } = default!;

    public string EntityType { get; set; } = default!;

    public string EntityId { get; set; } = default!;

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
