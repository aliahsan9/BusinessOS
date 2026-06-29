using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class Activity : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string UserId { get; set; } = default!;
    public string UserName { get; set; } = default!;
    public string Action { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public Guid EntityId { get; set; }
    public string EntityName { get; set; } = default!;
    public string? Metadata { get; set; }
}
