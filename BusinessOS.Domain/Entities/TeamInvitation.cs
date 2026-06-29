using BusinessOS.Domain.Common;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

public class TeamInvitation : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Email { get; set; } = default!;
    public Guid RoleId { get; set; }
    public string Token { get; set; } = default!;
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;
    public string InvitedByUserId { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }

    public Role Role { get; set; } = default!;
}
