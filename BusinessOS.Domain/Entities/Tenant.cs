using BusinessOS.Domain.Common;
namespace BusinessOS.Domain.Entities;

public class Tenant : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string BusinessType { get; set; } = default!;

    public string Email { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string Address { get; set; } = default!;
    public string? Website { get; set; }
    public string? Description { get; set; }

    public string SubscriptionPlan { get; set; } = "Free";
    public bool IsActive { get; set; } = true;
    public string OwnerUserId { get; set; } = default!;
    // Navigation
}
