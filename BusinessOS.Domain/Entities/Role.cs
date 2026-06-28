using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class Role : BaseEntity
{
    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
