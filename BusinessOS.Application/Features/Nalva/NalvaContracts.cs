using System.Text.Json.Nodes;

namespace BusinessOS.Application.Features.Nalva;

public sealed class NalvaToolOutcome
{
    public bool Success { get; init; }
    public bool ApprovalRequired { get; init; }
    public string? ApprovalAction { get; init; }
    public string Risk { get; init; } = "Low";
    public string? Error { get; init; }
    public string ErrorClass { get; init; } = NalvaErrorClass.None;
    public bool Idempotent { get; init; } = true;
    public string Summary { get; init; } = "";
    public JsonObject Data { get; init; } = new();
}

public sealed class NalvaExecuteRequest
{
    public string AgentKey { get; set; } = "";
    public string Objective { get; set; } = "";
    public string ArgumentsJson { get; set; } = "{}";
    public IReadOnlyList<string>? Tools { get; set; }
    public Guid? TaskId { get; set; }
    public Guid? WorkflowRunId { get; set; }
    public Guid? ParentExecutionId { get; set; }
    public string DelegationChain { get; set; } = "";
    public int Depth { get; set; }
    public int RetryAttempt { get; set; }
    public Guid? ApprovedApprovalId { get; set; }
    public bool DryRun { get; set; }
    public string? CorrelationId { get; set; }
}

public sealed class NalvaExecutionResult
{
    public Guid ExecutionId { get; init; }
    public string Status { get; init; } = "";
    public string AgentKey { get; init; } = "";
    public string? Summary { get; init; }
    public Guid? ApprovalId { get; init; }
    public decimal EstimatedCostUsd { get; init; }
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public bool CostIsEstimate { get; init; } = true;
    public string PricingVersion { get; init; } = NalvaLimits.PricingVersion;
    public IReadOnlyList<string> Tools { get; init; } = [];
    public string? ResultJson { get; init; }
    public string? Error { get; init; }
    public string ErrorClass { get; init; } = NalvaErrorClass.None;
    public bool IdempotentRetryAllowed { get; init; } = true;
}

public sealed class NalvaRunRequest
{
    public string Objective { get; set; } = "";
    public string? ArgumentsJson { get; set; }
    public List<string>? Tools { get; set; }
    public DateTime? RunAfter { get; set; }
    public int? Priority { get; set; }
    public string? IdempotencyKey { get; set; }
    public DateTime? Deadline { get; set; }
    public Guid? ParentTaskId { get; set; }
    /// <summary>When set, a completed task enqueues the next occurrence after this many minutes.</summary>
    public int? IntervalMinutes { get; set; }
}

public sealed class NalvaConfigureAgentRequest
{
    public int? AutonomyLevel { get; set; }
    public Guid? DepartmentId { get; set; }
    public bool ClearDepartment { get; set; }
    public string? Model { get; set; }
    public decimal? Temperature { get; set; }
    public int? DailyExecutionLimit { get; set; }
    public int? MonthlyTokenBudget { get; set; }
    public List<string>? Tools { get; set; }
}

public sealed class NalvaCustomAgentRequest
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Role { get; set; } = "Custom agent";
    public string Category { get; set; } = "Administration";
    public List<string> Tools { get; set; } = [];
    public List<string> Responsibilities { get; set; } = [];
}

public sealed class NalvaWorkflowWriteRequest
{
    public string? Key { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string TriggerKind { get; set; } = "Manual";
    public string? TriggerEvent { get; set; }
    public int? IntervalMinutes { get; set; }
    public string? TimeZoneId { get; set; }
    public DateTime? ScheduleStartUtc { get; set; }
    public DateTime? ScheduleEndUtc { get; set; }
    public string? RecurrenceKind { get; set; }
    public string? LocalTimeOfDay { get; set; }
    public string? DaysOfWeek { get; set; }
    public int? DayOfMonth { get; set; }
    public int? MaxConcurrentRuns { get; set; }
    public bool? IsPaused { get; set; }
    public List<NalvaWorkflowStepWrite> Steps { get; set; } = [];
}

public sealed class NalvaWorkflowStepWrite
{
    public string Key { get; set; } = "";
    public string AgentKey { get; set; } = "";
    public string Objective { get; set; } = "";
    public List<string> Tools { get; set; } = [];
    public string? Condition { get; set; }
    public int? ParallelGroup { get; set; }
    public string? OutputKey { get; set; }
}

public sealed class NalvaKnowledgeWriteRequest
{
    public string? AgentKey { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
}

public sealed class NalvaSettingsWriteRequest
{
    public string? DefaultModel { get; set; }
    public string? EmbeddingModel { get; set; }
    public decimal? Temperature { get; set; }
    public int? MonthlyTokenBudget { get; set; }
    public decimal? MonthlyBudgetUsd { get; set; }
    public int? DailyExecutionLimit { get; set; }
    public int? AlertThresholdPercent { get; set; }
    public int? AutonomyCeiling { get; set; }
    public int? ApprovalExpiryDays { get; set; }
    public bool? NotifyPendingApprovals { get; set; }
    public bool? AutoExpireApprovals { get; set; }
    public bool? RequireApprovalForExternalActions { get; set; }
    public List<string>? ExtraApprovalTools { get; set; }
}

public sealed class NalvaApprovalDecisionRequest
{
    public string? Note { get; set; }
    /// <summary>Optional replacement args JSON. Bound into the approval fingerprint before approve/execute.</summary>
    public string? ModifiedArgsJson { get; set; }
}

public sealed class NalvaEventRequest
{
    public string Name { get; set; } = "";
    public string? ArgumentsJson { get; set; }
    public string? IdempotencyKey { get; set; }
}

public interface INalvaWorkforceService
{
    Task<NalvaOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NalvaAgentCardDto>> GetMarketplaceAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NalvaAgentCardDto>> GetMyAgentsAsync(CancellationToken cancellationToken = default);
    Task<NalvaAgentDetailDto> GetAgentAsync(string key, CancellationToken cancellationToken = default);
    Task<NalvaAgentDetailDto> ActivateAsync(string key, CancellationToken cancellationToken = default);
    Task<NalvaAgentDetailDto> DeactivateAsync(string key, CancellationToken cancellationToken = default);
    Task<NalvaAgentDetailDto> ConfigureAsync(string key, NalvaConfigureAgentRequest request, CancellationToken cancellationToken = default);
    Task<NalvaAgentDetailDto> CreateCustomAsync(NalvaCustomAgentRequest request, CancellationToken cancellationToken = default);
    Task<NalvaExecutionResult> RunAsync(string key, NalvaRunRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NalvaTaskDto>> GetTasksAsync(string? agentKey, string? status, CancellationToken cancellationToken = default);
    Task<NalvaTaskDto> EnqueueAsync(string key, NalvaRunRequest request, CancellationToken cancellationToken = default);
    Task<NalvaTaskDto> CancelTaskAsync(Guid id, CancellationToken cancellationToken = default);
    Task<NalvaTaskDto> PauseTaskAsync(Guid id, CancellationToken cancellationToken = default);
    Task<NalvaTaskDto> ResumeTaskAsync(Guid id, CancellationToken cancellationToken = default);
    Task<NalvaTaskDto> RetryTaskAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NalvaWorkflowDto>> GetWorkflowsAsync(CancellationToken cancellationToken = default);
    Task<NalvaWorkflowDto> SaveWorkflowAsync(NalvaWorkflowWriteRequest request, CancellationToken cancellationToken = default);
    Task<NalvaWorkflowDto> SetWorkflowActiveAsync(Guid id, bool active, CancellationToken cancellationToken = default);
    Task<NalvaWorkflowRunDto> RunWorkflowAsync(Guid id, string? argumentsJson, CancellationToken cancellationToken = default);
    Task<NalvaWorkflowRunDto> TestWorkflowAsync(Guid id, string? argumentsJson, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NalvaWorkflowRunDto>> GetWorkflowRunsAsync(CancellationToken cancellationToken = default);
    Task<int> PublishEventAsync(NalvaEventRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NalvaApprovalDto>> GetApprovalsAsync(string? status, CancellationToken cancellationToken = default);
    Task<NalvaExecutionResult> DecideApprovalAsync(Guid id, string decision, string? note, CancellationToken cancellationToken = default);
    Task<NalvaExecutionResult> DecideApprovalAsync(Guid id, string decision, string? note, string? modifiedArgsJson, CancellationToken cancellationToken = default);
    Task<NalvaReliabilitySnapshotDto> GetReliabilitySnapshotAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NalvaKnowledgeDto>> GetKnowledgeAsync(string? agentKey, CancellationToken cancellationToken = default);
    Task<NalvaKnowledgeDto> AddKnowledgeAsync(NalvaKnowledgeWriteRequest request, CancellationToken cancellationToken = default);
    Task DeleteKnowledgeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NalvaMemoryDto>> GetMemoryAsync(string agentKey, CancellationToken cancellationToken = default);
    Task DeleteMemoryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NalvaExecutionDto>> GetExecutionsAsync(string? agentKey, string? status, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<NalvaAnalyticsDto> GetAnalyticsAsync(string? agentKey, string? category, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NalvaAgentOutcomeDto>> GetOutcomesAsync(string? agentKey, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<NalvaWorkforceRecommendationDto> GetWorkforceRecommendationAsync(CancellationToken cancellationToken = default);
    Task<NalvaApplyWorkforcePackResult> ApplyWorkforcePackAsync(NalvaApplyWorkforcePackRequest request, CancellationToken cancellationToken = default);
    Task<NalvaSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<NalvaSettingsDto> SaveSettingsAsync(NalvaSettingsWriteRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NalvaModelDto>> GetModelsAsync(CancellationToken cancellationToken = default);
}

public interface INalvaExecutionEngine
{
    Task<NalvaExecutionResult> ExecuteAsync(NalvaExecuteRequest request, CancellationToken cancellationToken = default);
    Task RunQueuedTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
}

public interface INalvaWorkflowEngine
{
    Task<NalvaWorkflowRunDto> RunAsync(Guid definitionId, string? argumentsJson, CancellationToken cancellationToken = default);
    Task<NalvaWorkflowRunDto> RunAsync(Guid definitionId, string? argumentsJson, NalvaWorkflowRunOptions options, CancellationToken cancellationToken = default);
    Task ContinueAfterAsync(Guid workflowRunId, string executionStatus, CancellationToken cancellationToken = default);
}

public sealed record NalvaOverviewDto(
    int ActivatedAgents,
    int ActiveAgents,
    int RunningExecutions,
    int CompletedTasks,
    int FailedExecutions,
    int PendingApprovals,
    int TotalTokens,
    decimal EstimatedCostUsd,
    bool CostIsEstimate,
    string PricingVersion,
    int WorkflowRuns,
    bool CostAlert);

public sealed record NalvaAgentCardDto(
    string Key,
    string Name,
    string Description,
    string Role,
    string Category,
    string Status,
    string Health,
    bool IsCustom,
    bool IsCore,
    int AutonomyLevel,
    string? DepartmentName,
    IReadOnlyList<string> Responsibilities,
    string? RecentExecution,
    int Executions,
    int Tokens,
    decimal EstimatedCostUsd);

public sealed record NalvaAgentDetailDto(
    string Key,
    string Name,
    string Description,
    string Role,
    string Category,
    string Status,
    string Health,
    string? HealthMessage,
    bool IsCustom,
    bool IsCore,
    int Version,
    int AutonomyLevel,
    Guid? DepartmentId,
    string? Model,
    decimal? Temperature,
    int DailyExecutionLimit,
    int MonthlyTokenBudget,
    IReadOnlyList<string> Responsibilities,
    IReadOnlyList<NalvaToolInfoDto> Tools,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Workflows,
    IReadOnlyList<string> ApprovalActions,
    bool UsesKnowledge,
    bool ExecutionAvailable,
    string? UnavailableReason);

public sealed record NalvaToolInfoDto(
    string Name,
    string Description,
    bool IsWrite,
    bool RequiresApproval,
    string? ApprovalAction,
    string Risk,
    string Integration,
    IReadOnlyList<string> Permissions);

public sealed record NalvaTaskDto(
    Guid Id,
    string AgentKey,
    string Objective,
    string Status,
    int Priority,
    DateTime? Deadline,
    DateTime RunAfter,
    int? IntervalMinutes,
    int Attempt,
    string? LastError,
    Guid? CompletedExecutionId,
    DateTime CreatedAt,
    Guid? ParentTaskId = null);

public sealed record NalvaWorkflowDto(
    Guid Id,
    string Key,
    string Name,
    string Description,
    string TriggerKind,
    string? TriggerEvent,
    int? IntervalMinutes,
    bool IsActive,
    IReadOnlyList<NalvaWorkflowStepWrite> Steps,
    int Version = 1,
    string? TimeZoneId = null,
    string RecurrenceKind = "Interval",
    string? LocalTimeOfDay = null,
    string? DaysOfWeek = null,
    int? DayOfMonth = null,
    bool IsPaused = false,
    int MaxConcurrentRuns = 1,
    DateTime? NextRunAt = null,
    DateTime? ScheduleStartUtc = null,
    DateTime? ScheduleEndUtc = null,
    DateTime? LastRunAt = null,
    bool IsCancelled = false,
    int ConsecutiveFailures = 0,
    string? LastFailure = null);

public sealed record NalvaReliabilitySnapshotDto(
    int QueuedTasks,
    int RunningTasks,
    int DeadLetterTasks,
    int FailedTasks,
    int PendingApprovals,
    int ExpiredApprovals,
    int PendingEvents,
    int DeadLetterEvents,
    int StuckRunningTasks,
    int StuckRunningExecutions,
    int StuckRunningEvents,
    int FailedWorkflowRunsToday,
    DateTime GeneratedAtUtc);

public sealed record NalvaWorkflowRunDto(
    Guid Id,
    Guid DefinitionId,
    string WorkflowName,
    string Status,
    int CurrentStep,
    string? Summary,
    string? Error,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string TriggerSource = "Manual",
    bool IsDryRun = false,
    decimal EstimatedCostUsd = 0,
    string? CorrelationId = null,
    string? WorkflowKey = null);

public sealed record NalvaApprovalDto(
    Guid Id,
    Guid ExecutionId,
    string AgentKey,
    string Action,
    string ToolName,
    string Risk,
    string? ContextJson,
    string Status,
    DateTime RequestedAt,
    string? RequestedByUserId,
    string? ApproverUserId,
    DateTime? DecidedAt,
    string? DecisionNote,
    string? ExecutionResult,
    Guid? WorkflowRunId = null,
    string? WorkflowName = null,
    string? TaskObjective = null,
    string? RelatedEntitySummary = null,
    string? AmountSummary = null,
    string? RiskSummary = null,
    DateTime? ExpiresAt = null,
    string? ArgsFingerprint = null,
    string? ExactActionParametersJson = null);

public sealed record NalvaKnowledgeDto(Guid Id, string? AgentKey, string Title, string Excerpt, DateTime CreatedAt);

public sealed record NalvaMemoryDto(Guid Id, string AgentKey, string Kind, string Title, string Excerpt, DateTime CreatedAt);

public sealed record NalvaExecutionDto(
    Guid Id,
    string AgentKey,
    Guid? TaskId,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string Status,
    string Model,
    int InputTokens,
    int OutputTokens,
    int ToolCallCount,
    int ApprovalCount,
    int RetryAttempt,
    int DurationMs,
    decimal EstimatedCostUsd,
    bool CostIsEstimate,
    string? Error,
    string? Summary);

public sealed record NalvaAnalyticsDto(
    int Executions,
    int Succeeded,
    int Failed,
    decimal SuccessRate,
    double AverageDurationMs,
    int Tokens,
    decimal EstimatedCostUsd,
    bool CostIsEstimate,
    string PricingVersion,
    int ToolCalls,
    int ApprovalRequests,
    int RejectedActions,
    int Retries,
    int TasksCompleted,
    int WorkflowRuns = 0,
    decimal WorkflowCompletionRate = 0);

public sealed record NalvaSettingsDto(
    string DefaultModel,
    string EmbeddingModel,
    decimal Temperature,
    int MonthlyTokenBudget,
    decimal MonthlyBudgetUsd,
    int DailyExecutionLimit,
    int AlertThresholdPercent,
    bool CostAlert,
    int AutonomyCeiling,
    int TokensUsedThisMonth,
    decimal EstimatedSpendThisMonthUsd,
    string PricingVersion,
    int ApprovalExpiryDays = 7,
    bool NotifyPendingApprovals = true,
    bool AutoExpireApprovals = true,
    bool RequireApprovalForExternalActions = true,
    IReadOnlyList<string>? ExtraApprovalTools = null);

public sealed record NalvaModelDto(
    string Model,
    string Kind,
    decimal InputUsdPerMillion,
    decimal OutputUsdPerMillion,
    int MaxTokens,
    bool SupportsTools,
    string PricingVersion,
    bool IsDefault);
