using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Stores a tenant-scoped forecast snapshot so accuracy can be evaluated after the target period ends.
/// Predictions are never treated as financial facts.
/// </summary>
public class PredictionAccuracyLog : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string MetricKey { get; set; } = default!;
    public string Method { get; set; } = default!;
    public decimal PredictedValue { get; set; }
    public decimal? ActualValue { get; set; }
    public decimal? AbsolutePercentError { get; set; }
    public string Confidence { get; set; } = "low";
    public DateTime TargetPeriodStart { get; set; }
    public DateTime TargetPeriodEnd { get; set; }
    public DateTime? EvaluatedAt { get; set; }
    public string Status { get; set; } = "Pending"; // Pending | Evaluated | Skipped
    public string? AssumptionsJson { get; set; }
}
