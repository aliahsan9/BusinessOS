using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class TenantUsage : AuditableEntity
{
    public Guid TenantId { get; set; }
    public int UserCount { get; set; }
    public int CustomerCount { get; set; }
    public int ProjectCount { get; set; }
    public int TaskCount { get; set; }
    public long StorageUsedMb { get; set; }
    public int AiRequestsUsed { get; set; }
    public DateTime LastCalculatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = default!;
}
