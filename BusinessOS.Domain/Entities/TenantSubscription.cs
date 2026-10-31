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
    public decimal? ContractedMonthlyPrice { get; set; }
    public decimal? ContractedAnnualPrice { get; set; }
    public string? ContractedCurrency { get; set; }
    public string? ContractedLimitsJson { get; set; }
    public bool PreserveContract { get; set; }
    public bool TrialConsumed { get; set; }
    public Guid? ScheduledPlanId { get; set; }
    public DateTime? CancelEffectiveAt { get; set; }
    public string? BillingCountry { get; set; }
    public string? DisplayCurrency { get; set; }
    public string? SettlementCurrency { get; set; }
    public string? PaymentMethodSummary { get; set; }
    public int? AiRequestAllowance { get; set; }
    public bool AutoRenewEnabled { get; set; }

    public Tenant Tenant { get; set; } = default!;
    public SubscriptionPlan Plan { get; set; } = default!;
}
