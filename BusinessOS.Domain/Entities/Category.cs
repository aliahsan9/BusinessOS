using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Product category used to organize a tenant's catalog.
/// </summary>
/// <remarks>
/// Tenant-scoped. Owns a collection of <see cref="Product"/> records assigned to this category.
/// </remarks>
public class Category : AuditableEntity
{
    /// <summary>
    /// Identifier of the tenant that owns this category. Required for multi-tenant isolation.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Display name of the category. Required and expected to be unique within the tenant.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Optional description explaining the purpose or contents of this category.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Collection of products assigned to this category.
    /// </summary>
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
