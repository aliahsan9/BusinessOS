namespace BusinessOS.Application.Features.Nalva;

public interface INalvaAutomationService
{
    Task<NalvaAutomationDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NalvaScheduleDto>> GetSchedulesAsync(CancellationToken cancellationToken = default);
    Task<NalvaScheduleDto> SaveScheduleAsync(NalvaScheduleWriteRequest request, CancellationToken cancellationToken = default);
    Task<NalvaScheduleDto> PauseScheduleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<NalvaScheduleDto> ResumeScheduleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<NalvaScheduleDto> CancelScheduleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NalvaAutomationRuleDto>> GetRulesAsync(CancellationToken cancellationToken = default);
    Task<NalvaAutomationRuleDto> SaveRuleAsync(NalvaAutomationRuleWriteRequest request, CancellationToken cancellationToken = default);
    Task<NalvaAutomationRuleDto> SetRuleEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NalvaWorkflowRunDto>> GetExecutionHistoryAsync(string? status, int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NalvaFailedAutomationDto>> GetFailedAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NalvaAgentActivityDto>> GetAgentActivityAsync(CancellationToken cancellationToken = default);
    Task<NalvaApprovalPolicyDto> GetApprovalPolicyAsync(CancellationToken cancellationToken = default);
    Task<NalvaApprovalPolicyDto> SaveApprovalPolicyAsync(NalvaApprovalPolicyWriteRequest request, CancellationToken cancellationToken = default);
    Task<int> ExpireApprovalsAsync(CancellationToken cancellationToken = default);
}

public sealed class NalvaScheduleWriteRequest
{
    public Guid? Id { get; set; }
    public string? Key { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string RecurrenceKind { get; set; } = NalvaRecurrenceKinds.Daily;
    public int? IntervalMinutes { get; set; }
    public string? TimeZoneId { get; set; }
    public string? LocalTimeOfDay { get; set; }
    public string? DaysOfWeek { get; set; }
    public int? DayOfMonth { get; set; }
    public DateTime? ScheduleStartUtc { get; set; }
    public DateTime? ScheduleEndUtc { get; set; }
    public int? MaxConcurrentRuns { get; set; }
    public bool Activate { get; set; } = true;
    public List<NalvaWorkflowStepWrite> Steps { get; set; } = [];
}

public sealed class NalvaAutomationRuleWriteRequest
{
    public Guid? Id { get; set; }
    public string? Key { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>Scheduled | Event | Manual</summary>
    public string TriggerKind { get; set; } = "Event";
    public string? TriggerEvent { get; set; }
    public string? Condition { get; set; }
    public string RecurrenceKind { get; set; } = NalvaRecurrenceKinds.Interval;
    public int? IntervalMinutes { get; set; }
    public string? TimeZoneId { get; set; }
    public string? LocalTimeOfDay { get; set; }
    public string? DaysOfWeek { get; set; }
    public int? DayOfMonth { get; set; }
    public DateTime? ScheduleStartUtc { get; set; }
    public DateTime? ScheduleEndUtc { get; set; }
    public bool Enable { get; set; }
    public List<NalvaWorkflowStepWrite> Steps { get; set; } = [];
}

public sealed class NalvaApprovalPolicyWriteRequest
{
    public int? ApprovalExpiryDays { get; set; }
    public bool? NotifyPendingApprovals { get; set; }
    public bool? AutoExpireApprovals { get; set; }
    public bool? RequireApprovalForExternalActions { get; set; }
    public List<string>? ExtraApprovalTools { get; set; }
}

public sealed record NalvaAutomationDashboardDto(
    int ActiveAutomations,
    int PausedSchedules,
    int ScheduledAutomations,
    int EventAutomations,
    int RunningExecutions,
    int CompletedToday,
    int FailedToday,
    int PendingApprovals,
    int ExpiredApprovals,
    int DeadLetterTasks,
    IReadOnlyList<NalvaAgentActivityDto> AgentActivity,
    IReadOnlyList<NalvaScheduleDto> UpcomingSchedules,
    IReadOnlyList<NalvaFailedAutomationDto> RecentFailures);

public sealed record NalvaScheduleDto(
    Guid Id,
    string Key,
    string Name,
    string Description,
    string RecurrenceKind,
    int? IntervalMinutes,
    string TimeZoneId,
    string? LocalTimeOfDay,
    string? DaysOfWeek,
    int? DayOfMonth,
    DateTime? ScheduleStartUtc,
    DateTime? ScheduleEndUtc,
    DateTime? NextRunAt,
    DateTime? LastRunAt,
    bool IsActive,
    bool IsPaused,
    bool IsCancelled,
    int MaxConcurrentRuns,
    int ConsecutiveFailures,
    string? LastFailure,
    IReadOnlyList<NalvaWorkflowStepWrite> Steps,
    int Version);

public sealed record NalvaAutomationRuleDto(
    Guid Id,
    string Key,
    string Name,
    string Description,
    string TriggerKind,
    string? TriggerEvent,
    string? Condition,
    string RecurrenceKind,
    int? IntervalMinutes,
    string TimeZoneId,
    bool IsEnabled,
    bool IsPaused,
    IReadOnlyList<string> AgentKeys,
    IReadOnlyList<NalvaWorkflowStepWrite> Steps,
    int Version,
    DateTime? NextRunAt);

public sealed record NalvaFailedAutomationDto(
    Guid Id,
    string Kind,
    string Name,
    string? AgentKey,
    string Status,
    string? Error,
    DateTime OccurredAt,
    Guid? DefinitionId,
    Guid? TaskId);

public sealed record NalvaAgentActivityDto(
    string AgentKey,
    string DisplayName,
    string Status,
    int ExecutionsToday,
    int FailuresToday,
    int PendingApprovals,
    DateTime? LastActivityAt);

public sealed record NalvaApprovalPolicyDto(
    int ApprovalExpiryDays,
    bool NotifyPendingApprovals,
    bool AutoExpireApprovals,
    bool RequireApprovalForExternalActions,
    IReadOnlyList<string> ExtraApprovalTools,
    IReadOnlyList<string> CatalogApprovalTools);
