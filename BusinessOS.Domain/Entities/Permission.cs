using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class Permission : BaseEntity
{
    public string Name { get; set; } = default!;

    public string Code { get; set; } = default!;

    public string? Description { get; set; }

    public string Category { get; set; } = default!;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
