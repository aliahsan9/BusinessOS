using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class SubscriptionHistory : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid? SubscriptionPlanId { get; set; }
    public string Action { get; set; } = default!;
    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public string? Provider { get; set; }
    public string? Notes { get; set; }

    public Tenant Tenant { get; set; } = default!;
}
