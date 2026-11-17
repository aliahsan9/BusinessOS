using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>Company-owned instance of a Nalva.ai agent. Catalog definitions stay in code.</summary>
public class NalvaCompanyAgent : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string AgentKey { get; set; } = default!;
    public int DefinitionVersion { get; set; } = 1;
    public bool IsCustom { get; set; }
    public string DisplayName { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string RoleTitle { get; set; } = default!;
    public string Category { get; set; } = default!;
    public string Status { get; set; } = "Inactive";
    public int AutonomyLevel { get; set; } = 3;
    public Guid? DepartmentId { get; set; }
    public string? Model { get; set; }
    public decimal? Temperature { get; set; }
    public int DailyExecutionLimit { get; set; } = 100;
    public int MonthlyTokenBudget { get; set; } = 2_000_000;
    public string ToolsJson { get; set; } = "[]";
    public string PermissionsJson { get; set; } = "[]";
    public string ResponsibilitiesJson { get; set; } = "[]";
    public string ApprovalActionsJson { get; set; } = "[]";
    public string? HealthMessage { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public string? ActivatedByUserId { get; set; }
}

public class NalvaAgentRevision : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid CompanyAgentId { get; set; }
    public int Version { get; set; }
    public string SnapshotJson { get; set; } = default!;
}

public class NalvaTask : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string AgentKey { get; set; } = default!;
    public string Objective { get; set; } = default!;
    public string Status { get; set; } = "Pending";
    public int Priority { get; set; } = 1;
    public DateTime? Deadline { get; set; }
    public DateTime RunAfter { get; set; } = DateTime.UtcNow;
    public int? IntervalMinutes { get; set; }
    public string? IdempotencyKey { get; set; }
    public Guid? ParentTaskId { get; set; }
    public Guid? ParentExecutionId { get; set; }
    public Guid? WorkflowRunId { get; set; }
    public int Depth { get; set; }
    public string DelegationChain { get; set; } = "";
    public string? InitiatorUserId { get; set; }
    public string? ToolPlanJson { get; set; }
    public string? ArgumentsJson { get; set; }
    public int Attempt { get; set; }
    public int MaxAttempts { get; set; } = 3;
    public string? LastError { get; set; }
    public string? ErrorClass { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public Guid? ClaimToken { get; set; }
    public Guid? CompletedExecutionId { get; set; }
}

public class NalvaExecution : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid? CompanyAgentId { get; set; }
    public string AgentKey { get; set; } = default!;
    public Guid? TaskId { get; set; }
    public Guid? WorkflowRunId { get; set; }
    public Guid? ParentExecutionId { get; set; }
    public string DelegationChain { get; set; } = "";
    public int Depth { get; set; }
    public string Objective { get; set; } = default!;
    public string Status { get; set; } = "Running";
    public string Model { get; set; } = default!;
    public decimal? Temperature { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public bool CostIsEstimate { get; set; } = true;
    public string PricingVersion { get; set; } = default!;
    public int ToolCallCount { get; set; }
    public string ToolLogJson { get; set; } = "[]";
    public string PlanJson { get; set; } = "[]";
    public int ApprovalCount { get; set; }
    public int RetryAttempt { get; set; }
    public string? Error { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int DurationMs { get; set; }
    public string? InitiatorUserId { get; set; }
    public string? Summary { get; set; }
    public string? ResultJson { get; set; }
    public bool CancelRequested { get; set; }
    public int ModelCallCount { get; set; }
    public string? CorrelationId { get; set; }
}

public class NalvaApproval : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid ExecutionId { get; set; }
    public string AgentKey { get; set; } = default!;
    public string Action { get; set; } = default!;
    public string ToolName { get; set; } = default!;
    public string Risk { get; set; } = "Medium";
    public string? ContextJson { get; set; }
    public string? ArgsJson { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public string? RequestedByUserId { get; set; }
    public string? ApproverUserId { get; set; }
    public string? DecisionNote { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? ExecutionResult { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class NalvaMemoryRecord : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string AgentKey { get; set; } = default!;
    public string Kind { get; set; } = "ShortTerm";
    public string Title { get; set; } = default!;
    public string Content { get; set; } = default!;
    public Guid? ExecutionId { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class NalvaKnowledgeSource : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string? AgentKey { get; set; }
    public string Title { get; set; } = default!;
    public string Content { get; set; } = default!;
    public string? MetadataJson { get; set; }
    public string CreatedByUserId { get; set; } = default!;
}

public class NalvaWorkflowDefinition : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Key { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string TriggerKind { get; set; } = "Manual";
    public string? TriggerEvent { get; set; }
    public int? IntervalMinutes { get; set; }
    public string StepsJson { get; set; } = "[]";
    public bool IsActive { get; set; }
    public int MaxToolCalls { get; set; } = 12;
    public int Version { get; set; } = 1;
    public string TimeZoneId { get; set; } = "UTC";
    public DateTime? ScheduleStartUtc { get; set; }
    public DateTime? ScheduleEndUtc { get; set; }
    public DateTime? NextRunAt { get; set; }
    public string? LastScheduleWindow { get; set; }
}

public class NalvaWorkflowRun : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid DefinitionId { get; set; }
    public string Status { get; set; } = "Pending";
    public int CurrentStep { get; set; }
    public string ContextJson { get; set; } = "{}";
    public string? Summary { get; set; }
    public string? Error { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int DefinitionVersion { get; set; } = 1;
    public string TriggerSource { get; set; } = "Manual";
    public bool IsDryRun { get; set; }
    public string StepsSnapshotJson { get; set; } = "[]";
    public string StepLogJson { get; set; } = "[]";
    public decimal EstimatedCostUsd { get; set; }
    public string? CorrelationId { get; set; }
}

public class NalvaCompanySettings : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string DefaultModel { get; set; } = "gpt-4o-mini";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public decimal Temperature { get; set; } = 0.2m;
    public int MonthlyTokenBudget { get; set; } = 2_000_000;
    public decimal MonthlyBudgetUsd { get; set; } = 50m;
    public int DailyExecutionLimit { get; set; } = 200;
    public int AlertThresholdPercent { get; set; } = 80;
    public bool CostAlert { get; set; }
    public int AutonomyCeiling { get; set; } = 3;
}

public class NalvaWorkItem : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string AgentKey { get; set; } = default!;
    public string Kind { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Body { get; set; } = default!;
    public string Status { get; set; } = "Draft";
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public Guid? ExecutionId { get; set; }
}
