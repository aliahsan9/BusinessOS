using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class Role : BaseEntity
{
    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// System roles are shared across companies and immutable via tenant APIs.
    /// Custom roles always belong to exactly one company.
    /// </summary>
    public bool IsSystem { get; set; }

    /// <summary>
    /// Null for shared system roles. Required for company-specific custom roles.
    /// </summary>
    public Guid? TenantId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant? Tenant { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
