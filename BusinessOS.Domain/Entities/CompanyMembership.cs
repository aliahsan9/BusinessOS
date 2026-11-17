using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Links a platform user account to one company and the role they hold there.
/// A user may have several memberships. The identity user type is not referenced here
/// so the domain project stays free of Identity. The active company remains on the user account.
/// </summary>
public class CompanyMembership : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string UserId { get; set; } = default!;
    public string RoleName { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = default!;
}
