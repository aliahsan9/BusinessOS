using BusinessOS.Domain.Common;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

public class Tenant : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? LogoUrl { get; set; }
    public string? Domain { get; set; }
    public string Timezone { get; set; } = "UTC";
    public string Currency { get; set; } = "USD";
    public TenantStatus Status { get; set; } = TenantStatus.Active;
    public Guid SubscriptionPlanId { get; set; }

    public string BusinessType { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string Address { get; set; } = default!;
    public string? Website { get; set; }
    public string? Description { get; set; }

    public string SubscriptionPlan { get; set; } = "Free";
    public bool IsActive { get; set; } = true;
    public string OwnerUserId { get; set; } = default!;

    public SubscriptionPlan Plan { get; set; } = default!;
    public TenantSettings? Settings { get; set; }
    public TenantSubscription? Subscription { get; set; }
    public TenantUsage? Usage { get; set; }
}
