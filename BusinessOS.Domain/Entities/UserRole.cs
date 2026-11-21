namespace BusinessOS.Domain.Entities;

public class UserRole
{
    public string UserId { get; set; } = default!;

    public Guid RoleId { get; set; }

    /// <summary>
    /// Company in which this role assignment is active.
    /// Preserves per-company assignments when a user belongs to multiple companies.
    /// </summary>
    public Guid TenantId { get; set; }

    public Role Role { get; set; } = default!;
}
