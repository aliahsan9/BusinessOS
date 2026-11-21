namespace BusinessOS.Domain.Entities;

public class RolePermission
{
    public Guid RoleId { get; set; }

    public Guid PermissionId { get; set; }

    /// <summary>
    /// Null for permissions on shared system roles.
    /// Matches the owning custom role's company for tenant-scoped assignments.
    /// </summary>
    public Guid? TenantId { get; set; }

    public Role Role { get; set; } = default!;

    public Permission Permission { get; set; } = default!;
}
