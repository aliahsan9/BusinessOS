using BusinessOS.Domain.Common;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

public class TenantSubscription : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid SubscriptionPlanId { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trial;
    public BillingInterval BillingInterval { get; set; } = BillingInterval.Monthly;
    public PaymentProviderType PaymentProvider { get; set; } = PaymentProviderType.Manual;
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime? EndDate { get; set; }
    public DateTime? TrialEndDate { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
    public bool CancelAtPeriodEnd { get; set; }
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public string? ExternalReference { get; set; }

    public Tenant Tenant { get; set; } = default!;
    public SubscriptionPlan Plan { get; set; } = default!;
}
