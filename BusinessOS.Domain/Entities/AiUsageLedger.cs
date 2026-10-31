using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Idempotent AI quota consumption. Provider cost is internal and is not shown to tenant users.
/// </summary>
public class AiUsageLedger : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string OperationKey { get; set; } = default!;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal EstimatedProviderCostUsd { get; set; }
    public bool ProviderFailed { get; set; }
    public DateTime PeriodStart { get; set; }

    public Tenant Tenant { get; set; } = default!;
}
