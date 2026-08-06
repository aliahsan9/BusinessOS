namespace BusinessOS.Domain.Common;

/// <summary>
/// Abstract base type for all domain entities, providing a globally unique identifier.
/// </summary>
/// <remarks>
/// Not tenant-scoped. Derived types add tenant isolation, auditing, or other cross-cutting concerns.
/// </remarks>
public abstract class BaseEntity
{
    /// <summary>
    /// Primary key for the entity. Generated as a new GUID on creation unless explicitly set.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
}
