using BusinessOS.Domain.Common;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Tenant-scoped record of a logical write operation identified by a client idempotency key.
/// Completed financial records are retained so a later cleanup cannot make the same operation run again.
/// </summary>
public class IdempotencyRecord : BaseEntity
{
    public Guid TenantId { get; set; }

    public string Operation { get; set; } = default!;

    public string Key { get; set; } = default!;

    public string RequestHash { get; set; } = default!;

    public IdempotencyState State { get; set; } = IdempotencyState.Processing;

    public int? ResponseStatusCode { get; set; }

    public string? ResponseBody { get; set; }

    public Guid? ResourceId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Informational retention timestamp. Completed records are not deleted when this passes.
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}
