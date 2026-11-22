using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>Tenant Business Autopilot configuration and operating mode.</summary>
public class BusinessAutopilotConfig : AuditableEntity
{
    public Guid TenantId { get; set; }

    /// <summary>Copilot | Supervised | Controlled</summary>
    public string Mode { get; set; } = AutopilotModes.Copilot;

    public bool IsEnabled { get; set; }

    /// <summary>Global kill switch — blocks new autonomous external/mutating work.</summary>
    public bool EmergencyStop { get; set; }

    public string ObjectivesJson { get; set; } = "[]";
    public string PrioritiesJson { get; set; } = "[]";
    public decimal? MonthlySalesTarget { get; set; }
    public decimal? MarketingBudgetMonthly { get; set; }
    public decimal? SpendingLimitPerAction { get; set; }
    public decimal? DailySpendingLimit { get; set; }
    public string InventoryPolicyJson { get; set; } = "{}";
    public string CustomerServicePolicyJson { get; set; } = "{}";
    public string ApprovalRequirementsJson { get; set; } = "{}";
    public string WorkingHoursJson { get; set; } = "{}";
    public string AgentResponsibilitiesJson { get; set; } = "{}";

    /// <summary>JSON array of agent keys paused by Autopilot controls.</summary>
    public string PausedAgentKeysJson { get; set; } = "[]";

    /// <summary>JSON array of scenario/workflow catalog keys enabled for Autopilot.</summary>
    public string EnabledScenarioKeysJson { get; set; } = "[]";

    /// <summary>JSON array of HIGH-risk tool names explicitly allowlisted in Controlled mode.</summary>
    public string ControlledAllowlistToolsJson { get; set; } = "[]";

    /// <summary>JSON array of MEDIUM-risk tools allowed without approval in Supervised/Controlled.</summary>
    public string AllowedMediumToolsJson { get; set; } = "[]";

    public string TimeZoneId { get; set; } = "UTC";
    public string? Notes { get; set; }
    public DateTime? LastActivatedAt { get; set; }
    public string? LastActivatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public static class AutopilotModes
{
    public const string Copilot = "Copilot";
    public const string Supervised = "Supervised";
    public const string Controlled = "Controlled";

    public static readonly string[] All = [Copilot, Supervised, Controlled];

    public static bool IsValid(string? mode) =>
        !string.IsNullOrWhiteSpace(mode) && All.Contains(mode, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string? mode) =>
        All.FirstOrDefault(x => string.Equals(x, mode, StringComparison.OrdinalIgnoreCase)) ?? Copilot;
}

/// <summary>Measurable business goal with clear target vs actual separation.</summary>
public class BusinessGoal : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public string Description { get; set; } = "";
    /// <summary>Metric key e.g. monthly_revenue, stockout_rate, overdue_ar, response_time_hours</summary>
    public string MetricKey { get; set; } = default!;
    public decimal TargetValue { get; set; }
    public decimal? BaselineValue { get; set; }
    public decimal? PredictedValue { get; set; }
    public decimal? ActualValue { get; set; }
    public string Unit { get; set; } = "currency";
    /// <summary>Active | Paused | Achieved | Missed | Cancelled</summary>
    public string Status { get; set; } = "Active";
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public string AssignedAgentKeysJson { get; set; } = "[]";
    public string RecommendedActionsJson { get; set; } = "[]";
    public string CompletedActionsJson { get; set; } = "[]";
    public string OutcomeNotes { get; set; } = "";
    public DateTime? LastMeasuredAt { get; set; }
    public bool EvidenceLinked { get; set; }
}

public class BusinessGoalProgress : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid GoalId { get; set; }
    public decimal? TargetValue { get; set; }
    public decimal? PredictedValue { get; set; }
    public decimal? ActualValue { get; set; }
    public string Source { get; set; } = "system";
    public string Notes { get; set; } = "";
    public DateTime MeasuredAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Tenant-scoped operational memory. Never grants permissions or rewrites policies.</summary>
public class BusinessMemoryEntry : AuditableEntity
{
    public Guid TenantId { get; set; }
    /// <summary>policy_summary | product_knowledge | supplier_preference | communication_style | support_procedure | workflow_outcome</summary>
    public string Kind { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Content { get; set; } = default!;
    public string SourceAttribution { get; set; } = default!;
    public string? SourceEntityType { get; set; }
    public Guid? SourceEntityId { get; set; }
    public string? VectorPointId { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public bool IsTrusted { get; set; } = true;
    public string CreatedByUserId { get; set; } = "";
}

/// <summary>Correlated Autopilot operation for the operations dashboard.</summary>
public class AutopilotOperationLog : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Kind { get; set; } = default!;
    public string Status { get; set; } = "Recorded";
    public string? ScenarioKey { get; set; }
    public string? AgentKey { get; set; }
    public string? ToolName { get; set; }
    public Guid? WorkflowRunId { get; set; }
    public Guid? TaskId { get; set; }
    public Guid? ExecutionId { get; set; }
    public Guid? ApprovalId { get; set; }
    public Guid? GoalId { get; set; }
    public string Summary { get; set; } = "";
    public string DetailJson { get; set; } = "{}";
    public bool ExternalEffect { get; set; }
    public bool FinancialEffect { get; set; }
    public decimal? EstimatedCostUsd { get; set; }
    public string? CorrelationId { get; set; }
}
