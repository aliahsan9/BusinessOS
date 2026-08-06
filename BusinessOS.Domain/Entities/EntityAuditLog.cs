using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Immutable audit trail entry recording field-level changes to business entities within a tenant.
/// </summary>
/// <remarks>
/// Tenant-scoped. Does not inherit soft-delete auditing from <see cref="AuditableEntity"/>.
/// Stores serialized old and new values for change comparison and compliance review.
/// </remarks>
public class EntityAuditLog : BaseEntity
{
    /// <summary>
    /// Identifier of the tenant where the change occurred. Required for multi-tenant isolation.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Identity user identifier or name of the user who made the change. Required.
    /// </summary>
    public string ChangedBy { get; set; } = default!;

    /// <summary>
    /// Name of the entity type that was changed (for example, Customer, Product). Required.
    /// </summary>
    public string EntityType { get; set; } = default!;

    /// <summary>
    /// String representation of the changed entity's primary key. Required.
    /// </summary>
    public string EntityId { get; set; } = default!;

    /// <summary>
    /// Action performed on the entity (for example, Created, Updated, Deleted). Required.
    /// </summary>
    public string Action { get; set; } = default!;

    /// <summary>
    /// Optional serialized snapshot of field values before the change.
    /// </summary>
    public string? OldValues { get; set; }

    /// <summary>
    /// Optional serialized snapshot of field values after the change.
    /// </summary>
    public string? NewValues { get; set; }

    /// <summary>
    /// UTC timestamp when the change was recorded. Defaults to the current UTC time on insert.
    /// </summary>
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
