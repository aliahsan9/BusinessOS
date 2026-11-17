using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Append-only platform administration history. SubjectTenantId names the affected company when there is one.
/// </summary>
public class PlatformAuditLog : BaseEntity
{
    public string ActorUserId { get; set; } = default!;
    public string Action { get; set; } = default!;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public Guid? SubjectTenantId { get; set; }
    public string? MetadataJson { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
