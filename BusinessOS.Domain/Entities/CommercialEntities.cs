using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Plan-level feature switch or limit. A later effective date replaces an earlier one.
/// Absence of a row means the catalog default and the plan columns apply.
/// </summary>
public class PlanFeatureGrant : BaseEntity
{
    public Guid SubscriptionPlanId { get; set; }
    public string FeatureKey { get; set; } = default!;
    public bool Enabled { get; set; }
    public long? Limit { get; set; }
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SubscriptionPlan Plan { get; set; } = default!;
}

/// <summary>
/// Company-specific entitlement. Used for enterprise overrides and authorized exceptions.
/// Applied after the plan, including when the subscription is restricted.
/// </summary>
public class CompanyFeatureOverride : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string FeatureKey { get; set; } = default!;
    public bool Enabled { get; set; }
    public long? Limit { get; set; }
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }
    public string? Reason { get; set; }

    public Tenant Tenant { get; set; } = default!;
}

/// <summary>
/// Configurable model price card. Estimated usage cost reads the active row.
/// This is not a provider invoice.
/// </summary>
public class AiModelPrice : BaseEntity
{
    public string Provider { get; set; } = "OpenAI";
    public string Model { get; set; } = default!;
    public string UsageCategory { get; set; } = "chat";
    public decimal InputUsdPerMillion { get; set; }
    public decimal OutputUsdPerMillion { get; set; }
    public string PricingVersion { get; set; } = "2026-11";
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// One alert per company, resource, level, and period. Quota exhaustion does not delete data.
/// </summary>
public class UsageAlert : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Resource { get; set; } = default!;
    public string Level { get; set; } = default!;
    public DateTime PeriodStart { get; set; }

    public Tenant Tenant { get; set; } = default!;
}
