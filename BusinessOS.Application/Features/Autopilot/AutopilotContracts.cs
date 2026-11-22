namespace BusinessOS.Application.Features.Autopilot;

public static class AutopilotRiskLevels
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";
}

public static class AutopilotScenarioKeys
{
    public const string InventoryRestocking = "low-stock-to-procurement";
    public const string DailyInventoryMonitor = "procurement-daily-monitor";
    public const string OverduePayments = "overdue-payment-followup";
    public const string FinancialMonitoring = "financial-monitoring";
    public const string CustomerSupport = "customer-support-automation";
    public const string AutonomousMarketing = "autonomous-marketing-weekly";
    public const string SocialCampaign = "social-campaign";
    public const string AutonomousSales = "autonomous-lead-generation";
    public const string RevenueGrowth = "revenue-growth-collaboration";
    public const string ProductLaunch = "product-launch-collaboration";

    public static readonly string[] DefaultSupervised =
    [
        DailyInventoryMonitor,
        FinancialMonitoring,
        InventoryRestocking,
        OverduePayments,
        CustomerSupport
    ];
}

public sealed class AutopilotConfigDto
{
    public Guid Id { get; set; }
    public string Mode { get; set; } = "Copilot";
    public bool IsEnabled { get; set; }
    public bool EmergencyStop { get; set; }
    public IReadOnlyList<string> Objectives { get; set; } = [];
    public IReadOnlyList<string> Priorities { get; set; } = [];
    public decimal? MonthlySalesTarget { get; set; }
    public decimal? MarketingBudgetMonthly { get; set; }
    public decimal? SpendingLimitPerAction { get; set; }
    public decimal? DailySpendingLimit { get; set; }
    public string InventoryPolicyJson { get; set; } = "{}";
    public string CustomerServicePolicyJson { get; set; } = "{}";
    public string ApprovalRequirementsJson { get; set; } = "{}";
    public string WorkingHoursJson { get; set; } = "{}";
    public string AgentResponsibilitiesJson { get; set; } = "{}";
    public IReadOnlyList<string> PausedAgentKeys { get; set; } = [];
    public IReadOnlyList<string> EnabledScenarioKeys { get; set; } = [];
    public IReadOnlyList<string> ControlledAllowlistTools { get; set; } = [];
    public IReadOnlyList<string> AllowedMediumTools { get; set; } = [];
    public string TimeZoneId { get; set; } = "UTC";
    public string? Notes { get; set; }
    public DateTime? LastActivatedAt { get; set; }
}

public sealed class AutopilotConfigWriteRequest
{
    public string? Mode { get; set; }
    public bool? IsEnabled { get; set; }
    public IReadOnlyList<string>? Objectives { get; set; }
    public IReadOnlyList<string>? Priorities { get; set; }
    public decimal? MonthlySalesTarget { get; set; }
    public decimal? MarketingBudgetMonthly { get; set; }
    public decimal? SpendingLimitPerAction { get; set; }
    public decimal? DailySpendingLimit { get; set; }
    public string? InventoryPolicyJson { get; set; }
    public string? CustomerServicePolicyJson { get; set; }
    public string? ApprovalRequirementsJson { get; set; }
    public string? WorkingHoursJson { get; set; }
    public string? AgentResponsibilitiesJson { get; set; }
    public IReadOnlyList<string>? EnabledScenarioKeys { get; set; }
    public IReadOnlyList<string>? ControlledAllowlistTools { get; set; }
    public IReadOnlyList<string>? AllowedMediumTools { get; set; }
    public string? TimeZoneId { get; set; }
    public string? Notes { get; set; }
}

public sealed class AutopilotActivateRequest
{
    public string Mode { get; set; } = "Supervised";
    public IReadOnlyList<string>? ScenarioKeys { get; set; }
    public bool ActivateRecommendedAgents { get; set; } = true;
}

public sealed class AutopilotScenarioResult
{
    public string ScenarioKey { get; set; } = "";
    public Guid? WorkflowDefinitionId { get; set; }
    public Guid? WorkflowRunId { get; set; }
    public string Status { get; set; } = "";
    public string Summary { get; set; } = "";
}

public sealed class AutopilotOpsSnapshotDto
{
    public string Mode { get; set; } = "Copilot";
    public bool IsEnabled { get; set; }
    public bool EmergencyStop { get; set; }
    public int ActiveWorkflows { get; set; }
    public int RunningAgents { get; set; }
    public int CompletedOperations24h { get; set; }
    public int PendingApprovals { get; set; }
    public int FailedExecutions24h { get; set; }
    public int UpcomingSchedules { get; set; }
    public int ExternalCommunications24h { get; set; }
    public int FinancialOperations24h { get; set; }
    public int AiExecutions24h { get; set; }
    public decimal EstimatedCostUsd24h { get; set; }
    public IReadOnlyList<string> PausedAgentKeys { get; set; } = [];
    public IReadOnlyList<AutopilotOpsItemDto> ActiveRuns { get; set; } = [];
    public IReadOnlyList<AutopilotOpsItemDto> PendingApprovalItems { get; set; } = [];
    public IReadOnlyList<AutopilotOpsItemDto> FailedItems { get; set; } = [];
    public IReadOnlyList<AutopilotOpsItemDto> Upcoming { get; set; } = [];
    public IReadOnlyList<AutopilotOpsItemDto> RecentOutcomes { get; set; } = [];
}

public sealed class AutopilotOpsItemDto
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string Status { get; set; } = "";
    public string? AgentKey { get; set; }
    public DateTime? At { get; set; }
    public string? Detail { get; set; }
}

public sealed class BusinessGoalDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string MetricKey { get; set; } = "";
    public decimal TargetValue { get; set; }
    public decimal? BaselineValue { get; set; }
    public decimal? PredictedValue { get; set; }
    public decimal? ActualValue { get; set; }
    public string Unit { get; set; } = "currency";
    public string Status { get; set; } = "Active";
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public IReadOnlyList<string> AssignedAgentKeys { get; set; } = [];
    public IReadOnlyList<string> RecommendedActions { get; set; } = [];
    public IReadOnlyList<string> CompletedActions { get; set; } = [];
    public string OutcomeNotes { get; set; } = "";
    public DateTime? LastMeasuredAt { get; set; }
    public bool EvidenceLinked { get; set; }
    public decimal? ProgressPercent { get; set; }
}

public sealed class BusinessGoalWriteRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string MetricKey { get; set; } = "";
    public decimal TargetValue { get; set; }
    public decimal? BaselineValue { get; set; }
    public string? Unit { get; set; }
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public IReadOnlyList<string>? AssignedAgentKeys { get; set; }
}

public sealed class BusinessMemoryDto
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string SourceAttribution { get; set; } = "";
    public string? SourceEntityType { get; set; }
    public Guid? SourceEntityId { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public bool IsTrusted { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class BusinessMemoryWriteRequest
{
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string SourceAttribution { get; set; } = "";
    public string? SourceEntityType { get; set; }
    public Guid? SourceEntityId { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}

public sealed class AutonomyDecision
{
    public bool AllowAutonomous { get; set; }
    public bool RequireApproval { get; set; }
    public bool Blocked { get; set; }
    public string RiskLevel { get; set; } = AutopilotRiskLevels.Low;
    public string Reason { get; set; } = "";
    public string Mode { get; set; } = "Copilot";
}

public interface IAutonomySafetyEngine
{
    Task<AutonomyDecision> EvaluateToolAsync(
        string agentKey,
        string toolName,
        string? argsJson,
        bool approvalAlreadyGranted,
        CancellationToken cancellationToken = default);

    Task<bool> IsEmergencyStoppedAsync(CancellationToken cancellationToken = default);
    Task<bool> IsAgentPausedAsync(string agentKey, CancellationToken cancellationToken = default);
}

public interface IBusinessAutopilotService
{
    Task<AutopilotConfigDto> GetConfigAsync(CancellationToken cancellationToken = default);
    Task<AutopilotConfigDto> SaveConfigAsync(AutopilotConfigWriteRequest request, CancellationToken cancellationToken = default);
    Task<AutopilotConfigDto> ActivateAsync(AutopilotActivateRequest request, CancellationToken cancellationToken = default);
    Task<AutopilotConfigDto> SetEmergencyStopAsync(bool enabled, CancellationToken cancellationToken = default);
    Task<AutopilotConfigDto> PauseAllAgentsAsync(CancellationToken cancellationToken = default);
    Task<AutopilotConfigDto> PauseAgentAsync(string agentKey, CancellationToken cancellationToken = default);
    Task<AutopilotConfigDto> ResumeAgentAsync(string agentKey, CancellationToken cancellationToken = default);
    Task<AutopilotConfigDto> ClearAgentPausesAsync(CancellationToken cancellationToken = default);
    Task<AutopilotScenarioResult> RunScenarioAsync(string scenarioKey, bool dryRun = false, CancellationToken cancellationToken = default);
    Task<AutopilotOpsSnapshotDto> GetOpsSnapshotAsync(CancellationToken cancellationToken = default);
    Task RetryFailedTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task StopWorkflowRunAsync(Guid runId, CancellationToken cancellationToken = default);
}

public interface IBusinessGoalService
{
    Task<IReadOnlyList<BusinessGoalDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<BusinessGoalDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BusinessGoalDto> CreateAsync(BusinessGoalWriteRequest request, CancellationToken cancellationToken = default);
    Task<BusinessGoalDto> UpdateAsync(Guid id, BusinessGoalWriteRequest request, CancellationToken cancellationToken = default);
    Task<BusinessGoalDto> RefreshActualAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IBusinessMemoryService
{
    Task<IReadOnlyList<BusinessMemoryDto>> ListAsync(string? kind = null, CancellationToken cancellationToken = default);
    Task<BusinessMemoryDto> UpsertAsync(BusinessMemoryWriteRequest request, CancellationToken cancellationToken = default);
    Task<BusinessMemoryDto> CorrectAsync(Guid id, BusinessMemoryWriteRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BusinessMemoryDto>> RetrieveForContextAsync(string query, int take = 8, CancellationToken cancellationToken = default);
}
