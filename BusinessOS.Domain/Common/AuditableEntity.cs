namespace BusinessOS.Domain.Common;

/// <summary>
/// Abstract base type for entities that track creation, modification, and soft-delete state.
/// </summary>
/// <remarks>
/// Inherits <see cref="BaseEntity"/>. Tenant scope is defined by each derived entity (typically via TenantId).
/// Soft-deleted records remain in storage with <see cref="IsDeleted"/> set to true.
/// </remarks>
public abstract class AuditableEntity : BaseEntity
{
    /// <summary>
    /// UTC timestamp when the entity was first created. Defaults to the current UTC time on insert.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// UTC timestamp of the most recent update, or null if the entity has never been modified after creation.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Indicates whether the entity has been soft-deleted. Defaults to false for active records.
    /// </summary>
    public bool IsDeleted { get; set; } = false;
}
