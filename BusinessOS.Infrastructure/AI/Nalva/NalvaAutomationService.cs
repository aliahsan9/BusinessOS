using System.Text.Json;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.AI.Nalva;

/// <summary>Production automation dashboard, schedules, approval policy, and failure visibility — reuses Nalva workflow definitions.</summary>
public sealed class NalvaAutomationService : INalvaAutomationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly BusinessOSDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly INalvaWorkforceService _workforce;

    public NalvaAutomationService(BusinessOSDbContext db, ICurrentUserService user, INalvaWorkforceService workforce)
    {
        _db = db;
        _user = user;
        _workforce = workforce;
    }

    public async Task<NalvaAutomationDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        var today = DateTime.UtcNow.Date;
        var definitions = await _db.NalvaWorkflowDefinitions.AsNoTracking().ToListAsync(cancellationToken);
        var runs = await _db.NalvaWorkflowRuns.AsNoTracking().Where(x => x.StartedAt >= today).ToListAsync(cancellationToken);
        var pendingApprovals = await _db.NalvaApprovals.AsNoTracking().CountAsync(x => x.Status == NalvaStatuses.Pending, cancellationToken);
        var expiredApprovals = await _db.NalvaApprovals.AsNoTracking().CountAsync(x => x.Status == NalvaStatuses.Expired, cancellationToken);
        var deadLetterTasks = await _db.NalvaTasks.AsNoTracking().CountAsync(x => x.Status == NalvaStatuses.DeadLetter, cancellationToken);
        var runningExecutions = await _db.NalvaExecutions.AsNoTracking().CountAsync(x => x.Status == NalvaStatuses.Running, cancellationToken);

        var activity = await GetAgentActivityAsync(cancellationToken);
        var upcoming = definitions
            .Where(x => x.TriggerKind == "Scheduled" && x.IsActive && !x.IsPaused && x.CancelledAt is null)
            .OrderBy(x => x.NextRunAt ?? DateTime.MaxValue)
            .Take(10)
            .Select(MapSchedule)
            .ToList();
        var failed = await GetFailedAsync(cancellationToken);

        return new NalvaAutomationDashboardDto(
            definitions.Count(x => x.IsActive && !x.IsPaused),
            definitions.Count(x => x.IsPaused || (!x.IsActive && x.TriggerKind == "Scheduled")),
            definitions.Count(x => x.TriggerKind == "Scheduled" && x.IsActive),
            definitions.Count(x => x.TriggerKind == "Event" && x.IsActive),
            runningExecutions,
            runs.Count(x => x.Status == NalvaStatuses.Completed),
            runs.Count(x => x.Status == NalvaStatuses.Failed),
            pendingApprovals,
            expiredApprovals,
            deadLetterTasks,
            activity,
            upcoming,
            failed.Take(15).ToList());
    }

    public async Task<IReadOnlyList<NalvaScheduleDto>> GetSchedulesAsync(CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        var rows = await _db.NalvaWorkflowDefinitions.AsNoTracking()
            .Where(x => x.TriggerKind == "Scheduled")
            .OrderBy(x => x.NextRunAt ?? DateTime.MaxValue)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return rows.Select(MapSchedule).ToList();
    }

    public async Task<NalvaScheduleDto> SaveScheduleAsync(NalvaScheduleWriteRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        if (string.IsNullOrWhiteSpace(request.Name) || request.Steps.Count == 0)
            throw new BadRequestException("A schedule needs a name and at least one step.");

        var saved = await _workforce.SaveWorkflowAsync(new NalvaWorkflowWriteRequest
        {
            Key = request.Key,
            Name = request.Name,
            Description = request.Description,
            TriggerKind = "Scheduled",
            RecurrenceKind = request.RecurrenceKind,
            IntervalMinutes = request.IntervalMinutes,
            TimeZoneId = request.TimeZoneId,
            LocalTimeOfDay = request.LocalTimeOfDay,
            DaysOfWeek = request.DaysOfWeek,
            DayOfMonth = request.DayOfMonth,
            ScheduleStartUtc = request.ScheduleStartUtc,
            ScheduleEndUtc = request.ScheduleEndUtc,
            MaxConcurrentRuns = request.MaxConcurrentRuns,
            Steps = request.Steps
        }, cancellationToken);

        if (request.Id is Guid existingId && existingId != saved.Id)
        {
            // Id targeting: load and update if key matched a different row.
        }

        if (request.Activate)
            saved = await _workforce.SetWorkflowActiveAsync(saved.Id, true, cancellationToken);

        var row = await _db.NalvaWorkflowDefinitions.AsNoTracking().FirstAsync(x => x.Id == saved.Id, cancellationToken);
        return MapSchedule(row);
    }

    public Task<NalvaScheduleDto> PauseScheduleAsync(Guid id, CancellationToken cancellationToken = default) =>
        SetPausedAsync(id, paused: true, cancel: false, cancellationToken);

    public Task<NalvaScheduleDto> ResumeScheduleAsync(Guid id, CancellationToken cancellationToken = default) =>
        SetPausedAsync(id, paused: false, cancel: false, cancellationToken);

    public Task<NalvaScheduleDto> CancelScheduleAsync(Guid id, CancellationToken cancellationToken = default) =>
        SetPausedAsync(id, paused: true, cancel: true, cancellationToken);

    public async Task<IReadOnlyList<NalvaAutomationRuleDto>> GetRulesAsync(CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        var rows = await _db.NalvaWorkflowDefinitions.AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return rows.Select(MapRule).ToList();
    }

    public async Task<NalvaAutomationRuleDto> SaveRuleAsync(NalvaAutomationRuleWriteRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        var saved = await _workforce.SaveWorkflowAsync(new NalvaWorkflowWriteRequest
        {
            Key = request.Key,
            Name = request.Name,
            Description = request.Description,
            TriggerKind = request.TriggerKind,
            TriggerEvent = request.TriggerEvent,
            RecurrenceKind = request.RecurrenceKind,
            IntervalMinutes = request.IntervalMinutes,
            TimeZoneId = request.TimeZoneId,
            LocalTimeOfDay = request.LocalTimeOfDay,
            DaysOfWeek = request.DaysOfWeek,
            DayOfMonth = request.DayOfMonth,
            ScheduleStartUtc = request.ScheduleStartUtc,
            ScheduleEndUtc = request.ScheduleEndUtc,
            Steps = request.Steps.Select(step =>
            {
                if (!string.IsNullOrWhiteSpace(request.Condition) && string.IsNullOrWhiteSpace(step.Condition))
                    step.Condition = request.Condition;
                return step;
            }).ToList()
        }, cancellationToken);

        if (request.Enable)
            saved = await _workforce.SetWorkflowActiveAsync(saved.Id, true, cancellationToken);
        else if (request.Id is null)
            saved = await _workforce.SetWorkflowActiveAsync(saved.Id, false, cancellationToken);

        var row = await _db.NalvaWorkflowDefinitions.AsNoTracking().FirstAsync(x => x.Id == saved.Id, cancellationToken);
        return MapRule(row);
    }

    public async Task<NalvaAutomationRuleDto> SetRuleEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        var saved = await _workforce.SetWorkflowActiveAsync(id, enabled, cancellationToken);
        var row = await _db.NalvaWorkflowDefinitions.AsNoTracking().FirstAsync(x => x.Id == saved.Id, cancellationToken);
        if (!enabled)
        {
            // Keep schedule configured but paused when disabling scheduled rules.
            var tracked = await _db.NalvaWorkflowDefinitions.FirstAsync(x => x.Id == id, cancellationToken);
            if (tracked.TriggerKind == "Scheduled")
            {
                tracked.IsPaused = true;
                await _db.SaveChangesAsync(cancellationToken);
            }
        }
        else
        {
            var tracked = await _db.NalvaWorkflowDefinitions.FirstAsync(x => x.Id == id, cancellationToken);
            tracked.IsPaused = false;
            tracked.CancelledAt = null;
            await _db.SaveChangesAsync(cancellationToken);
        }

        row = await _db.NalvaWorkflowDefinitions.AsNoTracking().FirstAsync(x => x.Id == id, cancellationToken);
        return MapRule(row);
    }

    public async Task<IReadOnlyList<NalvaWorkflowRunDto>> GetExecutionHistoryAsync(string? status, int take, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        take = Math.Clamp(take <= 0 ? 50 : take, 1, 200);
        var query = _db.NalvaWorkflowRuns.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);
        var runs = await query.OrderByDescending(x => x.StartedAt).Take(take).ToListAsync(cancellationToken);
        var definitionIds = runs.Select(x => x.DefinitionId).Distinct().ToList();
        var names = await _db.NalvaWorkflowDefinitions.AsNoTracking()
            .Where(x => definitionIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name, x.Key })
            .ToListAsync(cancellationToken);
        var nameMap = names.ToDictionary(x => x.Id, x => x);
        return runs.Select(run =>
        {
            nameMap.TryGetValue(run.DefinitionId, out var def);
            return new NalvaWorkflowRunDto(
                run.Id, run.DefinitionId, def?.Name ?? "Workflow", run.Status, run.CurrentStep,
                run.Summary, run.Error, run.StartedAt, run.CompletedAt, run.TriggerSource, run.IsDryRun,
                run.EstimatedCostUsd, run.CorrelationId, def?.Key);
        }).ToList();
    }

    public async Task<IReadOnlyList<NalvaFailedAutomationDto>> GetFailedAsync(CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        var failedRuns = await _db.NalvaWorkflowRuns.AsNoTracking()
            .Where(x => x.Status == NalvaStatuses.Failed)
            .OrderByDescending(x => x.StartedAt)
            .Take(40)
            .ToListAsync(cancellationToken);
        var definitionIds = failedRuns.Select(x => x.DefinitionId).Distinct().ToList();
        var defs = await _db.NalvaWorkflowDefinitions.AsNoTracking()
            .Where(x => definitionIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var failedTasks = await _db.NalvaTasks.AsNoTracking()
            .Where(x => x.Status == NalvaStatuses.Failed || x.Status == NalvaStatuses.DeadLetter)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Take(40)
            .ToListAsync(cancellationToken);

        var items = failedRuns.Select(run =>
        {
            defs.TryGetValue(run.DefinitionId, out var def);
            return new NalvaFailedAutomationDto(
                run.Id, "WorkflowRun", def?.Name ?? "Workflow", null, run.Status, run.Error,
                run.CompletedAt ?? run.StartedAt, run.DefinitionId, null);
        }).Concat(failedTasks.Select(task =>
            new NalvaFailedAutomationDto(
                task.Id, "Task", task.Objective, task.AgentKey, task.Status, task.LastError,
                task.UpdatedAt ?? task.CreatedAt, null, task.Id))).ToList();

        return items.OrderByDescending(x => x.OccurredAt).Take(50).ToList();
    }

    public async Task<IReadOnlyList<NalvaAgentActivityDto>> GetAgentActivityAsync(CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        var today = DateTime.UtcNow.Date;
        var agents = await _db.NalvaCompanyAgents.AsNoTracking().ToListAsync(cancellationToken);
        var executions = await _db.NalvaExecutions.AsNoTracking()
            .Where(x => x.StartedAt >= today)
            .Select(x => new { x.AgentKey, x.Status, x.StartedAt })
            .ToListAsync(cancellationToken);
        var approvals = await _db.NalvaApprovals.AsNoTracking()
            .Where(x => x.Status == NalvaStatuses.Pending)
            .GroupBy(x => x.AgentKey)
            .Select(g => new { AgentKey = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var approvalMap = approvals.ToDictionary(x => x.AgentKey, x => x.Count, StringComparer.OrdinalIgnoreCase);

        return agents.Select(agent =>
        {
            var todayRows = executions.Where(x => string.Equals(x.AgentKey, agent.AgentKey, StringComparison.OrdinalIgnoreCase)).ToList();
            return new NalvaAgentActivityDto(
                agent.AgentKey,
                agent.DisplayName,
                agent.Status,
                todayRows.Count,
                todayRows.Count(x => x.Status is NalvaStatuses.Failed or NalvaStatuses.DeadLetter),
                approvalMap.GetValueOrDefault(agent.AgentKey),
                todayRows.MaxBy(x => x.StartedAt)?.StartedAt ?? agent.UpdatedAt ?? agent.ActivatedAt);
        }).OrderByDescending(x => x.LastActivityAt ?? DateTime.MinValue).ToList();
    }

    public async Task<NalvaApprovalPolicyDto> GetApprovalPolicyAsync(CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        var settings = await _workforce.GetSettingsAsync(cancellationToken);
        var catalog = NalvaToolCatalog.All
            .Where(x => x.RequiresApproval)
            .Select(x => x.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
        return new NalvaApprovalPolicyDto(
            settings.ApprovalExpiryDays,
            settings.NotifyPendingApprovals,
            settings.AutoExpireApprovals,
            settings.RequireApprovalForExternalActions,
            settings.ExtraApprovalTools ?? [],
            catalog);
    }

    public async Task<NalvaApprovalPolicyDto> SaveApprovalPolicyAsync(NalvaApprovalPolicyWriteRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        await _workforce.SaveSettingsAsync(new NalvaSettingsWriteRequest
        {
            ApprovalExpiryDays = request.ApprovalExpiryDays,
            NotifyPendingApprovals = request.NotifyPendingApprovals,
            AutoExpireApprovals = request.AutoExpireApprovals,
            RequireApprovalForExternalActions = request.RequireApprovalForExternalActions,
            ExtraApprovalTools = request.ExtraApprovalTools
        }, cancellationToken);
        return await GetApprovalPolicyAsync(cancellationToken);
    }

    public async Task<int> ExpireApprovalsAsync(CancellationToken cancellationToken = default)
    {
        // Called from the schedule processor without an interactive user; tenant is set by the worker.
        var settings = await _db.NalvaCompanySettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (settings is not null && !settings.AutoExpireApprovals)
            return 0;

        var now = DateTime.UtcNow;
        var rows = await _db.NalvaApprovals
            .Where(x => (x.Status == NalvaStatuses.Pending || x.Status == NalvaStatuses.ChangesRequested)
                        && x.ExpiresAt != null
                        && x.ExpiresAt < now)
            .ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            row.Status = NalvaStatuses.Expired;
            row.ExecutionResult = "Approval expired before a person decided.";
            row.DecidedAt = now;
        }

        if (rows.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);
        return rows.Count;
    }

    private async Task<NalvaScheduleDto> SetPausedAsync(Guid id, bool paused, bool cancel, CancellationToken cancellationToken)
    {
        Require(PermissionCodes.NalvaManage);
        var row = await _db.NalvaWorkflowDefinitions.FirstOrDefaultAsync(x => x.Id == id && x.TriggerKind == "Scheduled", cancellationToken)
            ?? throw new NotFoundException("Schedule was not found.");
        row.IsPaused = paused;
        if (cancel)
        {
            row.CancelledAt = DateTime.UtcNow;
            row.IsActive = false;
        }
        else if (!paused)
        {
            row.CancelledAt = null;
            if (!row.IsActive)
                row.IsActive = true;
            var spec = new NalvaScheduleSpec(row.TimeZoneId, row.RecurrenceKind, row.IntervalMinutes, row.ScheduleStartUtc, row.ScheduleEndUtc, row.LocalTimeOfDay, row.DaysOfWeek, row.DayOfMonth);
            _ = NalvaSchedule.TryDue(spec, DateTime.UtcNow, row.LastScheduleWindow, out _, out var next);
            row.NextRunAt = next;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return MapSchedule(row);
    }

    private static NalvaScheduleDto MapSchedule(NalvaWorkflowDefinition row)
    {
        var steps = JsonSerializer.Deserialize<List<NalvaWorkflowStepWrite>>(row.StepsJson, JsonOptions) ?? [];
        return new NalvaScheduleDto(
            row.Id, row.Key, row.Name, row.Description, row.RecurrenceKind, row.IntervalMinutes, row.TimeZoneId,
            row.LocalTimeOfDay, row.DaysOfWeek, row.DayOfMonth, row.ScheduleStartUtc, row.ScheduleEndUtc,
            row.NextRunAt, row.LastRunAt, row.IsActive, row.IsPaused, row.CancelledAt is not null,
            row.MaxConcurrentRuns, row.ConsecutiveFailures, row.LastFailure, steps, row.Version);
    }

    private static NalvaAutomationRuleDto MapRule(NalvaWorkflowDefinition row)
    {
        var steps = JsonSerializer.Deserialize<List<NalvaWorkflowStepWrite>>(row.StepsJson, JsonOptions) ?? [];
        var condition = steps.Select(x => x.Condition).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        return new NalvaAutomationRuleDto(
            row.Id, row.Key, row.Name, row.Description, row.TriggerKind, row.TriggerEvent, condition,
            row.RecurrenceKind, row.IntervalMinutes, row.TimeZoneId, row.IsActive && !row.IsPaused, row.IsPaused,
            steps.Select(x => x.AgentKey).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            steps, row.Version, row.NextRunAt);
    }

    private void Require(string permission)
    {
        if (!_user.HasPermission(permission))
            throw new ForbiddenException("You do not have permission for that automation action.");
    }
}
