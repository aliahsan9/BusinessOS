using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Records a user action performed within a tenant for activity feeds and audit trails.
/// </summary>
/// <remarks>
/// Tenant-scoped. Each row captures who did what to which entity, optionally with serialized metadata.
/// </remarks>
public class Activity : AuditableEntity
{
    /// <summary>
    /// Identifier of the tenant where the activity occurred. Required for multi-tenant isolation.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Identity user identifier of the user who performed the action.
    /// </summary>
    public string UserId { get; set; } = default!;

    /// <summary>
    /// Display name of the user at the time the activity was recorded.
    /// </summary>
    public string UserName { get; set; } = default!;

    /// <summary>
    /// Short description of the action performed (for example, Created, Updated, Deleted).
    /// </summary>
    public string Action { get; set; } = default!;

    /// <summary>
    /// Name of the affected entity type (for example, Customer, Order).
    /// </summary>
    public string EntityType { get; set; } = default!;

    /// <summary>
    /// Primary key of the affected entity instance.
    /// </summary>
    public Guid EntityId { get; set; }

    /// <summary>
    /// Human-readable name or label of the affected entity at the time of the activity.
    /// </summary>
    public string EntityName { get; set; } = default!;

    /// <summary>
    /// Optional JSON or serialized metadata with additional context about the activity.
    /// </summary>
    public string? Metadata { get; set; }
}
