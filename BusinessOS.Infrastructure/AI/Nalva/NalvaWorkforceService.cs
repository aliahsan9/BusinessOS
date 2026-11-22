using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.AI.Nalva;

public sealed partial class NalvaWorkforceService : INalvaWorkforceService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly BusinessOSDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly INalvaExecutionEngine _engine;
    private readonly INalvaWorkflowEngine _workflows;
    private readonly INalvaEventOutbox? _events;
    private readonly INalvaEventDispatcher? _dispatcher;

    public NalvaWorkforceService(
        BusinessOSDbContext db,
        ICurrentUserService user,
        INalvaExecutionEngine engine,
        INalvaWorkflowEngine workflows,
        INalvaEventOutbox? events = null,
        INalvaEventDispatcher? dispatcher = null)
    {
        _db = db;
        _user = user;
        _engine = engine;
        _workflows = workflows;
        _events = events;
        _dispatcher = dispatcher;
    }

    public async Task<NalvaOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        var month = MonthStart();
        var agents = await _db.NalvaCompanyAgents.AsNoTracking().ToListAsync(cancellationToken);
        var executions = await _db.NalvaExecutions.AsNoTracking().Where(x => x.StartedAt >= month).ToListAsync(cancellationToken);
        var settings = await SettingsRowAsync(cancellationToken);
        var spend = executions.Sum(x => x.EstimatedCostUsd);
        return new NalvaOverviewDto(
            agents.Count,
            agents.Count(x => x.Status == NalvaStatuses.Active),
            await _db.NalvaExecutions.CountAsync(x => x.Status == NalvaStatuses.Running, cancellationToken),
            await _db.NalvaTasks.CountAsync(x => x.Status == NalvaStatuses.Completed, cancellationToken),
            executions.Count(x => x.Status == NalvaStatuses.Failed),
            await _db.NalvaApprovals.CountAsync(x => x.Status == NalvaStatuses.Pending, cancellationToken),
            executions.Sum(x => x.InputTokens + x.OutputTokens),
            spend,
            true,
            NalvaLimits.PricingVersion,
            await _db.NalvaWorkflowRuns.CountAsync(cancellationToken),
            settings.CostAlert || (settings.MonthlyBudgetUsd > 0 && spend >= settings.MonthlyBudgetUsd * settings.AlertThresholdPercent / 100m));
    }

    public async Task<IReadOnlyList<NalvaAgentCardDto>> GetMarketplaceAsync(CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        var mine = await _db.NalvaCompanyAgents.AsNoTracking().ToListAsync(cancellationToken);
        var cards = new List<NalvaAgentCardDto>();
        foreach (var agent in NalvaAgentCatalog.All)
            cards.Add(await CardAsync(agent.Key, agent.Name, agent.Description, agent.Role, agent.Category, agent.Responsibilities, false, agent.IsCore, mine.FirstOrDefault(x => x.AgentKey == agent.Key), cancellationToken));
        foreach (var custom in mine.Where(x => x.IsCustom))
            cards.Add(await CardAsync(custom.AgentKey, custom.DisplayName, custom.Description, custom.RoleTitle, custom.Category, NalvaJson.Strings(custom.ResponsibilitiesJson), true, false, custom, cancellationToken));
        return cards;
    }

    public async Task<IReadOnlyList<NalvaAgentCardDto>> GetMyAgentsAsync(CancellationToken cancellationToken = default)
    {
        var all = await GetMarketplaceAsync(cancellationToken);
        return all.Where(x => x.Status != NalvaStatuses.Inactive).ToList();
    }

    public async Task<NalvaAgentDetailDto> GetAgentAsync(string key, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        return await DetailAsync(key, cancellationToken);
    }

    public async Task<NalvaAgentDetailDto> ActivateAsync(string key, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        if (!NalvaAgentCatalog.TryGet(key, out var definition) || definition is null)
            throw new NotFoundException("That agent is not in the catalog.");
        var row = await _db.NalvaCompanyAgents.FirstOrDefaultAsync(x => x.AgentKey == definition.Key, cancellationToken);
        row ??= new NalvaCompanyAgent { AgentKey = definition.Key };
        if (row.Id == Guid.Empty || _db.Entry(row).State == EntityState.Detached)
            _db.NalvaCompanyAgents.Add(row);
        CopyDefinition(row, definition);
        row.Status = NalvaStatuses.Active;
        row.ActivatedAt = DateTime.UtcNow;
        row.ActivatedByUserId = _user.UserId;
        row.HealthMessage = row.DefinitionVersion < definition.Version ? "A newer catalog version is available." : null;
        await ReviseAsync(row, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await DetailAsync(definition.Key, cancellationToken);
    }

    public async Task<NalvaAgentDetailDto> DeactivateAsync(string key, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        var row = await RequireAgentAsync(key, cancellationToken);
        row.Status = NalvaStatuses.Inactive;
        var queued = await _db.NalvaTasks.Where(x => x.AgentKey == row.AgentKey && (x.Status == NalvaStatuses.Queued || x.Status == NalvaStatuses.Assigned)).ToListAsync(cancellationToken);
        foreach (var task in queued)
            task.Status = NalvaStatuses.Cancelled;
        await ReviseAsync(row, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await DetailAsync(row.AgentKey, cancellationToken);
    }

    public async Task<NalvaAgentDetailDto> ConfigureAsync(string key, NalvaConfigureAgentRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        var row = await RequireAgentAsync(key, cancellationToken);
        var settings = await SettingsRowAsync(cancellationToken);
        if (request.AutonomyLevel is int level)
        {
            if (level is < 1 or > 4)
                throw new BadRequestException("Autonomy must be between 1 and 4.");
            if (level > settings.AutonomyCeiling)
                throw new BadRequestException("That autonomy level is above the company ceiling.");
            row.AutonomyLevel = level;
        }
        if (request.ClearDepartment)
            row.DepartmentId = null;
        else if (request.DepartmentId is Guid departmentId)
        {
            var department = await _db.Departments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == departmentId, cancellationToken)
                ?? throw new BadRequestException("That department was not found in this company.");
            row.DepartmentId = department.Id;
        }
        if (!string.IsNullOrWhiteSpace(request.Model))
        {
            if (NalvaModelCatalog.Find(request.Model) is null || NalvaModelCatalog.Find(request.Model)!.Kind != "chat")
                throw new BadRequestException("That model is not available.");
            row.Model = request.Model;
        }
        if (request.Temperature is decimal temperature)
        {
            if (temperature is < 0 or > 1)
                throw new BadRequestException("Temperature must be between 0 and 1.");
            row.Temperature = temperature;
        }
        if (request.DailyExecutionLimit is int daily)
            row.DailyExecutionLimit = Math.Clamp(daily, 1, 1000);
        if (request.MonthlyTokenBudget is int budget)
            row.MonthlyTokenBudget = Math.Clamp(budget, 1000, settings.MonthlyTokenBudget);
        if (request.Tools is not null)
        {
            var ceiling = CeilingTools(row);
            var narrowed = request.Tools.Where(tool => ceiling.Contains(tool, StringComparer.OrdinalIgnoreCase) && NalvaToolCatalog.TryGet(tool, out _)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (narrowed.Length == 0)
                throw new BadRequestException("Choose at least one tool from this agent's catalog.");
            row.ToolsJson = JsonSerializer.Serialize(narrowed);
            row.PermissionsJson = JsonSerializer.Serialize(narrowed.SelectMany(tool => NalvaToolCatalog.Get(tool).Permissions).Distinct(StringComparer.OrdinalIgnoreCase));
            row.ApprovalActionsJson = JsonSerializer.Serialize(narrowed.Select(NalvaToolCatalog.Get).Where(tool => tool.ApprovalAction is not null).Select(tool => tool.ApprovalAction).Distinct());
        }
        await ReviseAsync(row, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await DetailAsync(row.AgentKey, cancellationToken);
    }

    public async Task<NalvaAgentDetailDto> CreateCustomAsync(NalvaCustomAgentRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BadRequestException("A name is required.");
        if (!NalvaAgentCatalog.Categories.Contains(request.Category))
            throw new BadRequestException("Choose a workforce category.");
        var tools = request.Tools.Where(tool => NalvaToolCatalog.TryGet(tool, out _)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (tools.Length == 0)
            throw new BadRequestException("Choose at least one registered tool.");
        var slug = Regex.Replace(request.Name.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        var key = $"custom-{slug}-{Guid.NewGuid().ToString("N")[..6]}";
        var row = new NalvaCompanyAgent
        {
            AgentKey = key,
            IsCustom = true,
            DisplayName = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? "Custom company agent." : request.Description.Trim(),
            RoleTitle = string.IsNullOrWhiteSpace(request.Role) ? "Custom agent" : request.Role.Trim(),
            Category = request.Category,
            Status = NalvaStatuses.Active,
            ToolsJson = JsonSerializer.Serialize(tools),
            PermissionsJson = JsonSerializer.Serialize(tools.SelectMany(tool => NalvaToolCatalog.Get(tool).Permissions).Distinct()),
            ResponsibilitiesJson = JsonSerializer.Serialize(request.Responsibilities.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray()),
            ApprovalActionsJson = JsonSerializer.Serialize(tools.Select(NalvaToolCatalog.Get).Where(tool => tool.ApprovalAction is not null).Select(tool => tool.ApprovalAction)),
            ActivatedAt = DateTime.UtcNow,
            ActivatedByUserId = _user.UserId
        };
        _db.NalvaCompanyAgents.Add(row);
        await ReviseAsync(row, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await DetailAsync(key, cancellationToken);
    }

    public async Task<NalvaExecutionResult> RunAsync(string key, NalvaRunRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        if (request.RunAfter is DateTime when && when > DateTime.UtcNow.AddMinutes(1))
        {
            var queued = await EnqueueAsync(key, request, cancellationToken);
            return new NalvaExecutionResult { ExecutionId = queued.Id, Status = queued.Status, AgentKey = queued.AgentKey, Summary = "Task queued." };
        }
        return await _engine.ExecuteAsync(new NalvaExecuteRequest
        {
            AgentKey = key,
            Objective = request.Objective,
            ArgumentsJson = request.ArgumentsJson ?? "{}",
            Tools = request.Tools
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<NalvaTaskDto>> GetTasksAsync(string? agentKey, string? status, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        var query = _db.NalvaTasks.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(agentKey))
            query = query.Where(x => x.AgentKey == agentKey);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);
        var rows = await query.OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(cancellationToken);
        return rows.Select(MapTask).ToList();
    }

    public async Task<NalvaTaskDto> EnqueueAsync(string key, NalvaRunRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        if (string.IsNullOrWhiteSpace(request.Objective))
            throw new BadRequestException("An objective is required.");
        if (request.Deadline is not null && request.Deadline.Value < DateTime.UtcNow.AddMinutes(-1))
            throw new BadRequestException("The deadline must be in the future.");
        if (request.ParentTaskId is not null && !await _db.NalvaTasks.AnyAsync(x => x.Id == request.ParentTaskId, cancellationToken))
            throw new BadRequestException("The parent task was not found.");
        var agent = await RequireAgentAsync(key, cancellationToken);
        if (agent.Status != NalvaStatuses.Active)
            throw new BadRequestException("Activate the agent before assigning work.");
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var idempotencyKey = request.IdempotencyKey.Trim();
            var existing = await _db.NalvaTasks.AsNoTracking().FirstOrDefaultAsync(
                x => x.IdempotencyKey == idempotencyKey
                     && (x.Status == NalvaStatuses.Completed
                         || x.Status == NalvaStatuses.Queued
                         || x.Status == NalvaStatuses.Assigned
                         || x.Status == NalvaStatuses.Running
                         || x.Status == NalvaStatuses.AwaitingApproval
                         || x.Status == NalvaStatuses.Paused),
                cancellationToken);
            if (existing is not null)
                return MapTask(existing);
        }
        var task = new NalvaTask
        {
            AgentKey = agent.AgentKey,
            Objective = request.Objective.Trim(),
            Status = NalvaStatuses.Assigned,
            Priority = Math.Clamp(request.Priority ?? 1, 1, 5),
            RunAfter = request.RunAfter ?? DateTime.UtcNow,
            Deadline = request.Deadline,
            ParentTaskId = request.ParentTaskId,
            IdempotencyKey = request.IdempotencyKey,
            IntervalMinutes = request.IntervalMinutes is int interval and > 0 ? Math.Clamp(interval, 5, 43_200) : null,
            InitiatorUserId = _user.UserId,
            ToolPlanJson = request.Tools is null ? null : JsonSerializer.Serialize(request.Tools),
            ArgumentsJson = request.ArgumentsJson
        };
        _db.NalvaTasks.Add(task);
        await _db.SaveChangesAsync(cancellationToken);
        return MapTask(task);
    }

    public Task<NalvaTaskDto> CancelTaskAsync(Guid id, CancellationToken cancellationToken = default) =>
        SetTaskAsync(id, NalvaStatuses.Cancelled, [NalvaStatuses.Queued, NalvaStatuses.Assigned, NalvaStatuses.Paused, NalvaStatuses.Pending, NalvaStatuses.AwaitingApproval], cancellationToken);

    public Task<NalvaTaskDto> PauseTaskAsync(Guid id, CancellationToken cancellationToken = default) =>
        SetTaskAsync(id, NalvaStatuses.Paused, [NalvaStatuses.Queued, NalvaStatuses.Assigned], cancellationToken);

    public Task<NalvaTaskDto> ResumeTaskAsync(Guid id, CancellationToken cancellationToken = default) =>
        SetTaskAsync(id, NalvaStatuses.Assigned, [NalvaStatuses.Paused], cancellationToken);

    public async Task<NalvaTaskDto> RetryTaskAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        var task = await _db.NalvaTasks.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Task was not found.");
        if (task.Status is not NalvaStatuses.Failed and not NalvaStatuses.DeadLetter)
            throw new BadRequestException("Only a failed task can be retried.");
        task.Status = NalvaStatuses.Assigned;
        task.RunAfter = DateTime.UtcNow;
        task.LastError = null;
        task.ClaimToken = null;
        await _db.SaveChangesAsync(cancellationToken);
        return MapTask(task);
    }

    public async Task<IReadOnlyList<NalvaWorkflowDto>> GetWorkflowsAsync(CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        await EnsureTemplatesAsync(cancellationToken);
        var rows = await _db.NalvaWorkflowDefinitions.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
        return rows.Select(MapWorkflow).ToList();
    }

    public async Task<NalvaWorkflowDto> SaveWorkflowAsync(NalvaWorkflowWriteRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        if (string.IsNullOrWhiteSpace(request.Name) || request.Steps.Count == 0)
            throw new BadRequestException("A workflow needs a name and at least one step.");
        foreach (var step in request.Steps)
        {
            if (!NalvaAgentCatalog.TryGet(step.AgentKey, out var agent) || agent is null)
            {
                var custom = await _db.NalvaCompanyAgents.AsNoTracking().FirstOrDefaultAsync(x => x.AgentKey == step.AgentKey && x.IsCustom, cancellationToken)
                    ?? throw new BadRequestException($"Unknown agent {step.AgentKey}.");
                var granted = NalvaJson.Strings(custom.ToolsJson);
                if (step.Tools.Any(tool => !granted.Contains(tool, StringComparer.OrdinalIgnoreCase)))
                    throw new BadRequestException($"A tool is not granted to {step.AgentKey}.");
            }
            else if (step.Tools.Any(tool => !agent.Tools.Contains(tool, StringComparer.OrdinalIgnoreCase)))
            {
                throw new BadRequestException($"A tool is not available to {step.AgentKey}.");
            }
        }

        var key = string.IsNullOrWhiteSpace(request.Key) ? "custom-" + Guid.NewGuid().ToString("N")[..8] : request.Key.Trim().ToLowerInvariant();
        var row = await _db.NalvaWorkflowDefinitions.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
        row ??= new NalvaWorkflowDefinition { Key = key };
        if (_db.Entry(row).State == EntityState.Detached)
            _db.NalvaWorkflowDefinitions.Add(row);
        row.Name = request.Name.Trim();
        row.Description = request.Description?.Trim() ?? "";
        row.TriggerKind = request.TriggerKind is "Scheduled" or "Event" ? request.TriggerKind : "Manual";
        row.TriggerEvent = request.TriggerKind == "Event" ? request.TriggerEvent : null;
        if (row.TriggerKind == "Event" && !NalvaEventNames.IsAllowed(row.TriggerEvent))
            throw new BadRequestException("That event is not registered.");
        row.IntervalMinutes = request.TriggerKind == "Scheduled" ? request.IntervalMinutes : null;
        row.TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId) ? row.TimeZoneId ?? "UTC" : request.TimeZoneId.Trim();
        row.ScheduleStartUtc = request.ScheduleStartUtc;
        row.ScheduleEndUtc = request.ScheduleEndUtc;
        row.RecurrenceKind = request.TriggerKind == "Scheduled"
            ? NalvaRecurrenceKinds.Normalize(request.RecurrenceKind)
            : NalvaRecurrenceKinds.Interval;
        row.LocalTimeOfDay = string.IsNullOrWhiteSpace(request.LocalTimeOfDay) ? row.LocalTimeOfDay : request.LocalTimeOfDay.Trim();
        row.DaysOfWeek = string.IsNullOrWhiteSpace(request.DaysOfWeek) ? row.DaysOfWeek : request.DaysOfWeek.Trim();
        row.DayOfMonth = request.DayOfMonth is int day ? Math.Clamp(day, 1, 28) : row.DayOfMonth;
        if (request.MaxConcurrentRuns is int concurrency)
            row.MaxConcurrentRuns = Math.Clamp(concurrency, 1, 4);
        if (request.IsPaused is bool paused)
            row.IsPaused = paused;
        if (row.TriggerKind == "Scheduled" && row.CancelledAt is not null)
            row.CancelledAt = null;
        if (!string.Equals(row.TimeZoneId, "UTC", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                _ = TimeZoneInfo.FindSystemTimeZoneById(row.TimeZoneId);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                throw new BadRequestException("That timezone is not recognized.");
            }
        }
        if (row.TriggerKind == "Scheduled")
        {
            var spec = new NalvaScheduleSpec(row.TimeZoneId, row.RecurrenceKind, row.IntervalMinutes, row.ScheduleStartUtc, row.ScheduleEndUtc, row.LocalTimeOfDay, row.DaysOfWeek, row.DayOfMonth);
            _ = NalvaSchedule.TryDue(spec, DateTime.UtcNow, row.LastScheduleWindow, out _, out var next);
            row.NextRunAt = next;
        }
        var stepsJson = JsonSerializer.Serialize(request.Steps, JsonOptions);
        if (_db.Entry(row).State != EntityState.Added && !string.Equals(row.StepsJson, stepsJson, StringComparison.Ordinal))
            row.Version += 1;
        row.StepsJson = stepsJson;
        row.MaxToolCalls = NalvaLimits.MaxToolCalls;
        await _db.SaveChangesAsync(cancellationToken);
        return MapWorkflow(row);
    }

    public async Task<NalvaWorkflowDto> SetWorkflowActiveAsync(Guid id, bool active, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        var row = await _db.NalvaWorkflowDefinitions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Workflow was not found.");
        row.IsActive = active;
        await _db.SaveChangesAsync(cancellationToken);
        return MapWorkflow(row);
    }

    public Task<NalvaWorkflowRunDto> RunWorkflowAsync(Guid id, string? argumentsJson, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        return _workflows.RunAsync(id, argumentsJson, cancellationToken);
    }

    public Task<NalvaWorkflowRunDto> TestWorkflowAsync(Guid id, string? argumentsJson, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        return _workflows.RunAsync(id, argumentsJson, new NalvaWorkflowRunOptions { DryRun = true, RequireActive = false, TriggerSource = "Test" }, cancellationToken);
    }

    public async Task<IReadOnlyList<NalvaWorkflowRunDto>> GetWorkflowRunsAsync(CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        var rows = await _db.NalvaWorkflowRuns.AsNoTracking().OrderByDescending(x => x.StartedAt).Take(50).ToListAsync(cancellationToken);
        var names = await _db.NalvaWorkflowDefinitions.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        return rows.Select(x => new NalvaWorkflowRunDto(x.Id, x.DefinitionId, names.GetValueOrDefault(x.DefinitionId, "Workflow"), x.Status, x.CurrentStep, x.Summary, x.Error, x.StartedAt, x.CompletedAt, x.TriggerSource, x.IsDryRun, x.EstimatedCostUsd)).ToList();
    }

    public async Task<int> PublishEventAsync(NalvaEventRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        if (!NalvaEventNames.IsAllowed(request.Name))
            throw new BadRequestException("That event is not registered.");
        if (_events is not null && _dispatcher is not null)
        {
            var id = await _events.EnqueueAsync(request.Name, request.ArgumentsJson ?? "{}", request.IdempotencyKey, cancellationToken);
            return id is null ? 0 : await _dispatcher.DispatchOneAsync(id.Value, cancellationToken);
        }
        var matches = await _db.NalvaWorkflowDefinitions.AsNoTracking()
            .Where(x => x.IsActive && x.TriggerKind == "Event" && x.TriggerEvent == request.Name)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        foreach (var id in matches)
            await _workflows.RunAsync(id, request.ArgumentsJson, cancellationToken);
        return matches.Count;
    }

    public async Task<IReadOnlyList<NalvaApprovalDto>> GetApprovalsAsync(string? status, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        var query = _db.NalvaApprovals.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);
        var rows = await query.OrderByDescending(x => x.RequestedAt).Take(100).ToListAsync(cancellationToken);
        return rows.Select(MapApproval).ToList();
    }

    public Task<NalvaExecutionResult> DecideApprovalAsync(Guid id, string decision, string? note, CancellationToken cancellationToken = default) =>
        DecideApprovalAsync(id, decision, note, modifiedArgsJson: null, cancellationToken);

    public async Task<NalvaExecutionResult> DecideApprovalAsync(Guid id, string decision, string? note, string? modifiedArgsJson, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaApprove);
        if (string.IsNullOrWhiteSpace(_user.UserId))
            throw new ForbiddenException("A person must decide this approval.");
        var approval = await _db.NalvaApprovals.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Approval was not found.");
        if (approval.Status != NalvaStatuses.Pending && approval.Status != NalvaStatuses.ChangesRequested)
            throw new BadRequestException("That approval is already decided.");
        if (approval.ExpiresAt is DateTime expiry && expiry < DateTime.UtcNow)
        {
            approval.Status = NalvaStatuses.Expired;
            await _db.SaveChangesAsync(cancellationToken);
            throw new BadRequestException("That approval has expired.");
        }
        approval.ApproverUserId = _user.UserId;
        approval.DecisionNote = NalvaGuard.Clip(note, 500);
        approval.DecidedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(modifiedArgsJson))
        {
            if (NalvaGuard.RejectsArguments(modifiedArgsJson, out var rejected))
                throw new BadRequestException(rejected);
            var redacted = NalvaGuard.Redact(modifiedArgsJson);
            approval.ArgsJson = redacted;
            var fingerprint = NalvaCollaborationService.Fingerprint(redacted);
            try
            {
                var context = JsonNode.Parse(string.IsNullOrWhiteSpace(approval.ContextJson) ? "{}" : approval.ContextJson) as JsonObject
                              ?? new JsonObject();
                context["argsFingerprint"] = fingerprint;
                context["modifiedByUserId"] = _user.UserId;
                context["modifiedAt"] = DateTime.UtcNow;
                approval.ContextJson = context.ToJsonString();
            }
            catch (JsonException)
            {
                approval.ContextJson = new JsonObject
                {
                    ["argsFingerprint"] = fingerprint,
                    ["modifiedByUserId"] = _user.UserId
                }.ToJsonString();
            }

            var executionForPlan = await _db.NalvaExecutions.FirstOrDefaultAsync(x => x.Id == approval.ExecutionId, cancellationToken);
            if (executionForPlan is not null)
            {
                var plan = JsonSerializer.Deserialize<List<NalvaPlannedTool>>(executionForPlan.PlanJson) ?? [];
                for (var i = 0; i < plan.Count; i++)
                {
                    if (string.Equals(plan[i].Name, approval.ToolName, StringComparison.OrdinalIgnoreCase))
                        plan[i] = plan[i] with { ArgsJson = redacted };
                }
                executionForPlan.PlanJson = JsonSerializer.Serialize(plan);
            }
        }

        if (decision == "reject")
        {
            approval.Status = NalvaStatuses.Rejected;
            approval.ExecutionResult = "Rejected. The action was not executed.";
            var execution = await _db.NalvaExecutions.FirstOrDefaultAsync(x => x.Id == approval.ExecutionId, cancellationToken);
            if (execution is not null)
            {
                execution.Status = NalvaStatuses.Cancelled;
                execution.Summary = "A person rejected the action.";
                execution.CompletedAt = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync(cancellationToken);
            return new NalvaExecutionResult { ExecutionId = approval.ExecutionId, Status = NalvaStatuses.Cancelled, AgentKey = approval.AgentKey, Summary = approval.ExecutionResult, ApprovalId = approval.Id };
        }
        if (decision == "changes")
        {
            approval.Status = NalvaStatuses.ChangesRequested;
            approval.ExecutionResult = string.IsNullOrWhiteSpace(modifiedArgsJson)
                ? "Changes requested. The action was not executed."
                : "Changes requested with modified parameters. Approve again to execute.";
            approval.DecidedAt = null;
            await _db.SaveChangesAsync(cancellationToken);
            return new NalvaExecutionResult { ExecutionId = approval.ExecutionId, Status = NalvaStatuses.AwaitingApproval, AgentKey = approval.AgentKey, Summary = approval.ExecutionResult, ApprovalId = approval.Id };
        }

        // Bind approval to exact args fingerprint; material changes invalidate it unless this decision supplied ModifiedArgsJson.
        var currentFingerprint = NalvaCollaborationService.Fingerprint(approval.ArgsJson);
        try
        {
            using var contextDoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(approval.ContextJson) ? "{}" : approval.ContextJson);
            if (contextDoc.RootElement.TryGetProperty("argsFingerprint", out var stored)
                && stored.GetString() is string expected
                && !string.Equals(expected, currentFingerprint, StringComparison.OrdinalIgnoreCase))
            {
                approval.Status = NalvaStatuses.Expired;
                approval.ExecutionResult = "Approval invalidated because the proposed action parameters changed.";
                await _db.SaveChangesAsync(cancellationToken);
                throw new BadRequestException("Approval was invalidated because the proposed action parameters changed.");
            }
        }
        catch (JsonException)
        {
            // Older approvals without fingerprint still proceed; new ones always store one.
        }

        if (string.Equals(approval.ApproverUserId, approval.AgentKey, StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Agents cannot approve their own actions.");

        approval.Status = NalvaStatuses.Approved;
        await _db.SaveChangesAsync(cancellationToken);
        var executionRow = await _db.NalvaExecutions.AsNoTracking().FirstAsync(x => x.Id == approval.ExecutionId, cancellationToken);
        var result = await _engine.ExecuteAsync(new NalvaExecuteRequest
        {
            AgentKey = executionRow.AgentKey,
            Objective = executionRow.Objective,
            ApprovedApprovalId = approval.Id
        }, cancellationToken);
        approval.ExecutionResult = result.Summary;
        await _db.SaveChangesAsync(cancellationToken);
        if (executionRow.WorkflowRunId is Guid workflowRunId)
            await _workflows.ContinueAfterAsync(workflowRunId, result.Status, cancellationToken);
        return result;
    }

    public async Task<NalvaReliabilitySnapshotDto> GetReliabilitySnapshotAsync(CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        var now = DateTime.UtcNow;
        var leaseCutoff = now.AddSeconds(-NalvaLimits.ClaimLeaseSeconds);
        var today = now.Date;
        var tasks = await _db.NalvaTasks.AsNoTracking()
            .Select(x => new { x.Status, x.UpdatedAt, x.CreatedAt })
            .ToListAsync(cancellationToken);
        var executions = await _db.NalvaExecutions.AsNoTracking()
            .Where(x => x.Status == NalvaStatuses.Running)
            .Select(x => x.StartedAt)
            .ToListAsync(cancellationToken);
        var events = await _db.NalvaBusinessEvents.AsNoTracking()
            .Select(x => new { x.Status, x.UpdatedAt, x.OccurredAt })
            .ToListAsync(cancellationToken);
        var pendingApprovals = await _db.NalvaApprovals.AsNoTracking().CountAsync(x => x.Status == NalvaStatuses.Pending, cancellationToken);
        var expiredApprovals = await _db.NalvaApprovals.AsNoTracking().CountAsync(x => x.Status == NalvaStatuses.Expired, cancellationToken);
        var failedRunsToday = await _db.NalvaWorkflowRuns.AsNoTracking()
            .CountAsync(x => x.StartedAt >= today && x.Status == NalvaStatuses.Failed, cancellationToken);

        return new NalvaReliabilitySnapshotDto(
            tasks.Count(x => x.Status == NalvaStatuses.Queued || x.Status == NalvaStatuses.Assigned),
            tasks.Count(x => x.Status == NalvaStatuses.Running),
            tasks.Count(x => x.Status == NalvaStatuses.DeadLetter),
            tasks.Count(x => x.Status == NalvaStatuses.Failed),
            pendingApprovals,
            expiredApprovals,
            events.Count(x => x.Status == "Pending"),
            events.Count(x => x.Status == "DeadLetter"),
            tasks.Count(x => x.Status == NalvaStatuses.Running && (x.UpdatedAt ?? x.CreatedAt) < leaseCutoff),
            executions.Count(x => x < leaseCutoff),
            events.Count(x => x.Status == "Running" && (x.UpdatedAt ?? x.OccurredAt) < leaseCutoff),
            failedRunsToday,
            now);
    }

    public async Task<IReadOnlyList<NalvaKnowledgeDto>> GetKnowledgeAsync(string? agentKey, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        var query = _db.NalvaKnowledgeSources.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(agentKey))
            query = query.Where(x => x.AgentKey == null || x.AgentKey == agentKey);
        var rows = await query.OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(cancellationToken);
        return rows.Select(x => new NalvaKnowledgeDto(x.Id, x.AgentKey, x.Title, NalvaGuard.Clip(x.Content, 180), x.CreatedAt)).ToList();
    }

    public async Task<NalvaKnowledgeDto> AddKnowledgeAsync(NalvaKnowledgeWriteRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
            throw new BadRequestException("Title and content are required.");
        if (!string.IsNullOrWhiteSpace(request.AgentKey))
            await RequireAgentAsync(request.AgentKey, cancellationToken);
        var row = new NalvaKnowledgeSource
        {
            AgentKey = string.IsNullOrWhiteSpace(request.AgentKey) ? null : request.AgentKey,
            Title = request.Title.Trim(),
            Content = NalvaGuard.Redact(request.Content),
            CreatedByUserId = _user.UserId ?? "user"
        };
        _db.NalvaKnowledgeSources.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return new NalvaKnowledgeDto(row.Id, row.AgentKey, row.Title, NalvaGuard.Clip(row.Content, 180), row.CreatedAt);
    }

    public async Task DeleteKnowledgeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        var row = await _db.NalvaKnowledgeSources.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Knowledge was not found.");
        _db.NalvaKnowledgeSources.Remove(row);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NalvaMemoryDto>> GetMemoryAsync(string agentKey, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        var rows = await _db.NalvaMemoryRecords.AsNoTracking()
            .Where(x => x.AgentKey == agentKey && (x.ExpiresAt == null || x.ExpiresAt > DateTime.UtcNow))
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);
        return rows.Select(x => new NalvaMemoryDto(x.Id, x.AgentKey, x.Kind, x.Title, NalvaGuard.Clip(x.Content, 180), x.CreatedAt)).ToList();
    }

    public async Task DeleteMemoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        var row = await _db.NalvaMemoryRecords.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Memory was not found.");
        _db.NalvaMemoryRecords.Remove(row);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NalvaExecutionDto>> GetExecutionsAsync(string? agentKey, string? status, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        var rows = await FilteredExecutions(agentKey, status, from, to, cancellationToken);
        return rows.Select(x => new NalvaExecutionDto(x.Id, x.AgentKey, x.TaskId, x.StartedAt, x.CompletedAt, x.Status, x.Model, x.InputTokens, x.OutputTokens, x.ToolCallCount, x.ApprovalCount, x.RetryAttempt, x.DurationMs, x.EstimatedCostUsd, x.CostIsEstimate, x.Error, x.Summary)).ToList();
    }

    public async Task<NalvaAnalyticsDto> GetAnalyticsAsync(string? agentKey, string? category, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        var rows = await FilteredExecutions(agentKey, null, from, to, cancellationToken);
        if (!string.IsNullOrWhiteSpace(category))
        {
            var keys = await AgentKeysForCategory(category, cancellationToken);
            rows = rows.Where(x => keys.Contains(x.AgentKey)).ToList();
        }
        var succeeded = rows.Count(x => x.Status == NalvaStatuses.Completed);
        var failed = rows.Count(x => x.Status == NalvaStatuses.Failed);
        var decided = await _db.NalvaApprovals.AsNoTracking().Where(x => x.Status == NalvaStatuses.Rejected).CountAsync(cancellationToken);
        var requests = await _db.NalvaApprovals.AsNoTracking().CountAsync(cancellationToken);
        var completedTasks = await _db.NalvaTasks.CountAsync(x => x.Status == NalvaStatuses.Completed, cancellationToken);
        var workflowRuns = await _db.NalvaWorkflowRuns.AsNoTracking().CountAsync(cancellationToken);
        var workflowsCompleted = await _db.NalvaWorkflowRuns.AsNoTracking().CountAsync(x => x.Status == NalvaStatuses.Completed, cancellationToken);
        return new NalvaAnalyticsDto(
            rows.Count,
            succeeded,
            failed,
            rows.Count == 0 ? 0 : decimal.Round((decimal)succeeded / rows.Count, 4),
            rows.Count == 0 ? 0 : rows.Average(x => x.DurationMs),
            rows.Sum(x => x.InputTokens + x.OutputTokens),
            rows.Sum(x => x.EstimatedCostUsd),
            true,
            NalvaLimits.PricingVersion,
            rows.Sum(x => x.ToolCallCount),
            requests,
            decided,
            rows.Sum(x => x.RetryAttempt),
            completedTasks,
            workflowRuns,
            workflowRuns == 0 ? 0 : decimal.Round((decimal)workflowsCompleted / workflowRuns, 4));
    }

    public async Task<IReadOnlyList<NalvaAgentOutcomeDto>> GetOutcomesAsync(string? agentKey, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        var executions = await FilteredExecutions(agentKey, null, from, to, cancellationToken);
        var workQuery = _db.NalvaWorkItems.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(agentKey))
            workQuery = workQuery.Where(x => x.AgentKey == agentKey);
        if (from is DateTime start)
            workQuery = workQuery.Where(x => x.CreatedAt >= start);
        if (to is DateTime end)
            workQuery = workQuery.Where(x => x.CreatedAt <= end);
        var workItems = await workQuery.Take(2000).ToListAsync(cancellationToken);
        var approvals = await _db.NalvaApprovals.AsNoTracking().ToListAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(agentKey))
            approvals = approvals.Where(x => string.Equals(x.AgentKey, agentKey, StringComparison.OrdinalIgnoreCase)).ToList();
        if (from is DateTime approvalFrom)
            approvals = approvals.Where(x => x.RequestedAt >= approvalFrom).ToList();
        if (to is DateTime approvalTo)
            approvals = approvals.Where(x => x.RequestedAt <= approvalTo).ToList();

        var keys = executions.Select(x => x.AgentKey)
            .Concat(workItems.Select(x => x.AgentKey))
            .Concat(approvals.Select(x => x.AgentKey))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        var outcomes = new List<NalvaAgentOutcomeDto>();
        foreach (var key in keys)
        {
            var agentExecutions = executions.Where(x => string.Equals(x.AgentKey, key, StringComparison.OrdinalIgnoreCase)).ToList();
            var agentWork = workItems.Where(x => string.Equals(x.AgentKey, key, StringComparison.OrdinalIgnoreCase)).ToList();
            var agentApprovals = approvals.Where(x => string.Equals(x.AgentKey, key, StringComparison.OrdinalIgnoreCase)).ToList();
            var name = NalvaAgentCatalog.TryGet(key, out var definition) && definition is not null
                ? definition.Name
                : key;
            outcomes.Add(new NalvaAgentOutcomeDto(
                key,
                name,
                agentExecutions.Count,
                agentExecutions.Count(x => x.Status == NalvaStatuses.Completed),
                agentExecutions.Count(x => x.Status == NalvaStatuses.Failed),
                agentApprovals.Count(x => x.Status == NalvaStatuses.Pending),
                agentApprovals.Count(x => x.Status == NalvaStatuses.Approved),
                agentApprovals.Count(x => x.Status == NalvaStatuses.Rejected),
                CountOutcome(agentExecutions, "CreateLead"),
                CountOutcome(agentExecutions, "CreateDeal"),
                CountOutcome(agentExecutions, "CreateFollowUp") + agentWork.Count(x => x.Kind == "FollowUp"),
                agentWork.Count(x => x.Kind is "Content" or "Strategy"),
                agentWork.Count(x => x.Kind == "Email"),
                agentWork.Count(x => x.Kind == "Campaign"),
                agentWork.Count(x => x.Kind == "SupportTicket"),
                agentWork.Count(x => x.Kind is "Purchase" or "PurchaseOrder") + CountOutcome(agentExecutions, "CreatePurchaseOrderDraft") + CountOutcome(agentExecutions, "CreatePurchaseOrder"),
                agentWork.Count(x => x.Kind is "Report" or "BusinessReport" or "FinancialReport" or "HRReport" or "DailySummary"),
                agentWork.Count,
                agentExecutions.Sum(x => x.EstimatedCostUsd),
                true));
        }

        return outcomes;
    }

    public async Task<NalvaWorkforceRecommendationDto> GetWorkforceRecommendationAsync(CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        await EnsureTemplatesAsync(cancellationToken);
        var tenantId = _user.TenantId ?? throw new BadRequestException("Company context is required.");
        var businessType = await _db.Tenants.AsNoTracking()
            .Where(x => x.Id == tenantId)
            .Select(x => x.BusinessType)
            .FirstOrDefaultAsync(cancellationToken) ?? "custom";
        var pack = NalvaWorkforcePackCatalog.Resolve(businessType);
        var agents = await _db.NalvaCompanyAgents.AsNoTracking().ToListAsync(cancellationToken);
        var workflows = await _db.NalvaWorkflowDefinitions.AsNoTracking().ToListAsync(cancellationToken);
        var recommended = pack.RecommendedAgentKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var agentDtos = NalvaAgentCatalog.All.Select(agent =>
        {
            var row = agents.FirstOrDefault(x => string.Equals(x.AgentKey, agent.Key, StringComparison.OrdinalIgnoreCase));
            return new NalvaRecommendedAgentDto(
                agent.Key,
                agent.Name,
                agent.Role,
                agent.Category,
                row?.Status == NalvaStatuses.Active,
                recommended.Contains(agent.Key));
        }).ToList();
        var workflowRecommended = pack.RecommendedWorkflowKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var workflowDtos = workflows.Select(workflow => new NalvaRecommendedWorkflowDto(
            workflow.Key,
            workflow.Name,
            workflow.TriggerKind,
            workflow.IsActive,
            workflowRecommended.Contains(workflow.Key))).ToList();
        return new NalvaWorkforceRecommendationDto(
            businessType,
            pack.BusinessTypeKey,
            pack.Name,
            pack.Description,
            agentDtos,
            workflowDtos,
            agentDtos.Count(x => x.IsActive && x.IsRecommended),
            pack.RecommendedAgentKeys.Count);
    }

    public async Task<NalvaApplyWorkforcePackResult> ApplyWorkforcePackAsync(NalvaApplyWorkforcePackRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        await EnsureTemplatesAsync(cancellationToken);
        var tenantId = _user.TenantId ?? throw new BadRequestException("Company context is required.");
        NalvaWorkforcePack pack;
        if (!string.IsNullOrWhiteSpace(request.PackKey)
            && NalvaWorkforcePackCatalog.All.FirstOrDefault(x =>
                string.Equals(x.BusinessTypeKey, request.PackKey, StringComparison.OrdinalIgnoreCase)) is { } explicitPack)
        {
            pack = explicitPack;
        }
        else
        {
            var businessType = await _db.Tenants.AsNoTracking()
                .Where(x => x.Id == tenantId)
                .Select(x => x.BusinessType)
                .FirstOrDefaultAsync(cancellationToken) ?? "custom";
            pack = NalvaWorkforcePackCatalog.Resolve(businessType);
        }
        var activatedAgents = new List<string>();
        var activatedWorkflows = new List<string>();
        var skipped = new List<string>();
        var only = request.OnlyAgentKeys?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (request.ActivateRecommendedAgents)
        {
            foreach (var key in pack.RecommendedAgentKeys)
            {
                if (only is not null && !only.Contains(key))
                {
                    skipped.Add(key);
                    continue;
                }

                if (!NalvaAgentCatalog.TryGet(key, out _))
                {
                    skipped.Add(key);
                    continue;
                }

                await ActivateAsync(key, cancellationToken);
                activatedAgents.Add(key);
            }
        }

        if (request.ActivateRecommendedWorkflows)
        {
            var recommendedWorkflowKeys = pack.RecommendedWorkflowKeys.ToList();
            var workflows = await _db.NalvaWorkflowDefinitions
                .Where(x => recommendedWorkflowKeys.Contains(x.Key))
                .ToListAsync(cancellationToken);
            foreach (var workflow in workflows)
            {
                if (!workflow.IsActive)
                {
                    workflow.IsActive = true;
                    activatedWorkflows.Add(workflow.Key);
                }
            }

            if (activatedWorkflows.Count > 0)
                await _db.SaveChangesAsync(cancellationToken);
        }

        return new NalvaApplyWorkforcePackResult(pack.BusinessTypeKey, activatedAgents, activatedWorkflows, skipped);
    }

    public async Task<NalvaSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        return await MapSettingsAsync(await SettingsRowAsync(cancellationToken), cancellationToken);
    }

    public async Task<NalvaSettingsDto> SaveSettingsAsync(NalvaSettingsWriteRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        var row = await SettingsRowAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.DefaultModel))
        {
            var model = NalvaModelCatalog.Find(request.DefaultModel);
            if (model is null || model.Kind != "chat")
                throw new BadRequestException("That chat model is not available.");
            row.DefaultModel = model.Model;
        }
        if (!string.IsNullOrWhiteSpace(request.EmbeddingModel))
        {
            var model = NalvaModelCatalog.Find(request.EmbeddingModel);
            if (model is null || model.Kind != "embedding")
                throw new BadRequestException("That embedding model is not available.");
            row.EmbeddingModel = model.Model;
        }
        if (request.Temperature is decimal temperature)
        {
            if (temperature is < 0 or > 1)
                throw new BadRequestException("Temperature must be between 0 and 1.");
            row.Temperature = temperature;
        }
        if (request.MonthlyTokenBudget is int tokens)
            row.MonthlyTokenBudget = Math.Clamp(tokens, 1000, 50_000_000);
        if (request.MonthlyBudgetUsd is decimal budget)
            row.MonthlyBudgetUsd = Math.Clamp(budget, 0, 100_000);
        if (request.DailyExecutionLimit is int daily)
            row.DailyExecutionLimit = Math.Clamp(daily, 1, 5000);
        if (request.AlertThresholdPercent is int percent)
            row.AlertThresholdPercent = Math.Clamp(percent, 1, 100);
        if (request.AutonomyCeiling is int ceiling)
            row.AutonomyCeiling = Math.Clamp(ceiling, 1, 4);
        if (request.ApprovalExpiryDays is int expiry)
            row.ApprovalExpiryDays = Math.Clamp(expiry, 1, 30);
        if (request.NotifyPendingApprovals is bool notify)
            row.NotifyPendingApprovals = notify;
        if (request.AutoExpireApprovals is bool autoExpire)
            row.AutoExpireApprovals = autoExpire;
        if (request.RequireApprovalForExternalActions is bool requireExternal)
            row.RequireApprovalForExternalActions = requireExternal;
        if (request.ExtraApprovalTools is not null)
            row.ExtraApprovalToolsJson = JsonSerializer.Serialize(request.ExtraApprovalTools.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(40).ToList(), JsonOptions);
        await _db.SaveChangesAsync(cancellationToken);
        return await MapSettingsAsync(row, cancellationToken);
    }

    public Task<IReadOnlyList<NalvaModelDto>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        var models = NalvaModelCatalog.Prices.Select(x => new NalvaModelDto(x.Model, x.Kind, x.InputUsdPerMillion, x.OutputUsdPerMillion, x.MaxTokens, x.SupportsTools, NalvaLimits.PricingVersion, x.Model == "gpt-4o-mini")).ToList();
        return Task.FromResult<IReadOnlyList<NalvaModelDto>>(models);
    }

    private async Task<NalvaTaskDto> SetTaskAsync(Guid id, string status, string[] allowed, CancellationToken ct)
    {
        Require(PermissionCodes.NalvaManage);
        var task = await _db.NalvaTasks.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Task was not found.");
        if (!allowed.Contains(task.Status))
            throw new BadRequestException("That task cannot change to the requested status.");
        task.Status = status;
        if (status == NalvaStatuses.Cancelled)
        {
            var execution = await _db.NalvaExecutions.FirstOrDefaultAsync(x => x.TaskId == task.Id && x.Status == NalvaStatuses.Running, ct);
            if (execution is not null)
                execution.CancelRequested = true;
        }
        await _db.SaveChangesAsync(ct);
        return MapTask(task);
    }

    private async Task EnsureTemplatesAsync(CancellationToken ct)
    {
        var existing = await _db.NalvaWorkflowDefinitions.Select(x => x.Key).ToListAsync(ct);
        var added = false;
        foreach (var template in NalvaWorkflowCatalog.All)
        {
            if (existing.Contains(template.Key, StringComparer.OrdinalIgnoreCase))
                continue;
            _db.NalvaWorkflowDefinitions.Add(new NalvaWorkflowDefinition
            {
                Key = template.Key,
                Name = template.Name,
                Description = template.Description,
                TriggerKind = template.TriggerKind,
                TriggerEvent = template.TriggerEvent,
                IntervalMinutes = template.IntervalMinutes,
                TimeZoneId = "UTC",
                StepsJson = JsonSerializer.Serialize(template.Steps.Select(step => new NalvaWorkflowStepWrite
                {
                    Key = step.Key,
                    AgentKey = step.AgentKey,
                    Objective = step.Objective,
                    Tools = step.Tools.ToList(),
                    Condition = step.Condition,
                    ParallelGroup = step.ParallelGroup
                }), JsonOptions),
                IsActive = false,
                MaxToolCalls = NalvaLimits.MaxToolCalls
            });
            added = true;
        }
        if (added)
            await _db.SaveChangesAsync(ct);
    }

    private async Task<NalvaCompanySettings> SettingsRowAsync(CancellationToken ct)
    {
        var row = await _db.NalvaCompanySettings.FirstOrDefaultAsync(ct);
        if (row is not null)
            return row;
        row = new NalvaCompanySettings();
        _db.NalvaCompanySettings.Add(row);
        await _db.SaveChangesAsync(ct);
        return row;
    }

    private async Task<NalvaSettingsDto> MapSettingsAsync(NalvaCompanySettings row, CancellationToken ct)
    {
        var month = MonthStart();
        var usage = await _db.NalvaExecutions.AsNoTracking().Where(x => x.StartedAt >= month)
            .Select(x => new { x.InputTokens, x.OutputTokens, x.EstimatedCostUsd }).ToListAsync(ct);
        return new NalvaSettingsDto(
            row.DefaultModel,
            row.EmbeddingModel,
            row.Temperature,
            row.MonthlyTokenBudget,
            row.MonthlyBudgetUsd,
            row.DailyExecutionLimit,
            row.AlertThresholdPercent,
            row.CostAlert,
            row.AutonomyCeiling,
            usage.Sum(x => x.InputTokens + x.OutputTokens),
            usage.Sum(x => x.EstimatedCostUsd),
            NalvaLimits.PricingVersion,
            row.ApprovalExpiryDays,
            row.NotifyPendingApprovals,
            row.AutoExpireApprovals,
            row.RequireApprovalForExternalActions,
            NalvaJson.Strings(row.ExtraApprovalToolsJson));
    }

    private async Task<NalvaAgentCardDto> CardAsync(string key, string name, string description, string role, string category, IReadOnlyList<string> responsibilities, bool custom, bool core, NalvaCompanyAgent? row, CancellationToken ct)
    {
        var executions = await _db.NalvaExecutions.AsNoTracking().Where(x => x.AgentKey == key).OrderByDescending(x => x.StartedAt).Take(20).ToListAsync(ct);
        var department = row?.DepartmentId is Guid departmentId
            ? await _db.Departments.AsNoTracking().Where(x => x.Id == departmentId).Select(x => x.Name).FirstOrDefaultAsync(ct)
            : null;
        return new NalvaAgentCardDto(key, name, description, role, category, row?.Status ?? NalvaStatuses.Inactive, Health(row), custom, core, row?.AutonomyLevel ?? 3, department, responsibilities, executions.FirstOrDefault()?.Summary, executions.Count, executions.Sum(x => x.InputTokens + x.OutputTokens), executions.Sum(x => x.EstimatedCostUsd));
    }

    private async Task<NalvaAgentDetailDto> DetailAsync(string key, CancellationToken ct)
    {
        var row = await _db.NalvaCompanyAgents.AsNoTracking().FirstOrDefaultAsync(x => x.AgentKey == key, ct);
        NalvaAgentDefinition definition;
        if (NalvaAgentCatalog.TryGet(key, out var catalog) && catalog is not null)
            definition = catalog;
        else if (row is { IsCustom: true })
            definition = new NalvaAgentDefinition(row.AgentKey, row.DisplayName, row.Description, row.RoleTitle, row.Category, NalvaJson.Strings(row.ResponsibilitiesJson), NalvaJson.Strings(row.ResponsibilitiesJson), NalvaJson.Strings(row.ToolsJson), NalvaJson.Strings(row.PermissionsJson), [], row.Model ?? "gpt-4o-mini", NalvaJson.Strings(row.ApprovalActionsJson), row.DailyExecutionLimit, row.AutonomyLevel, false, row.DefinitionVersion, true);
        else
            throw new NotFoundException("That agent was not found.");

        var granted = row is null ? definition.Tools : NalvaJson.Strings(row.ToolsJson);
        var tools = granted.Where(tool => NalvaToolCatalog.TryGet(tool, out _)).Select(tool =>
        {
            var item = NalvaToolCatalog.Get(tool);
            return new NalvaToolInfoDto(item.Name, item.Description, item.IsWrite, item.RequiresApproval, item.ApprovalAction, item.Risk, item.Integration, item.Permissions);
        }).ToList();
        var available = row?.Status == NalvaStatuses.Active;
        return new NalvaAgentDetailDto(
            definition.Key, definition.Name, definition.Description, definition.Role, definition.Category,
            row?.Status ?? NalvaStatuses.Inactive, Health(row), row?.HealthMessage, row?.IsCustom ?? false, definition.IsCore,
            row?.DefinitionVersion ?? definition.Version, row?.AutonomyLevel ?? definition.DefaultAutonomy, row?.DepartmentId,
            row?.Model ?? definition.DefaultModel, row?.Temperature, row?.DailyExecutionLimit ?? definition.DailyExecutionLimit,
            row?.MonthlyTokenBudget ?? 2_000_000, definition.Responsibilities, tools,
            row is null ? definition.Permissions : NalvaJson.Strings(row.PermissionsJson), definition.Workflows,
            row is null ? definition.ApprovalActions : NalvaJson.Strings(row.ApprovalActionsJson), definition.UsesKnowledge,
            available, available ? null : "Activate the agent before it can run.");
    }

    private async Task<List<NalvaExecution>> FilteredExecutions(string? agentKey, string? status, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var query = _db.NalvaExecutions.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(agentKey))
            query = query.Where(x => x.AgentKey == agentKey);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);
        if (from is DateTime start)
            query = query.Where(x => x.StartedAt >= start);
        if (to is DateTime end)
            query = query.Where(x => x.StartedAt <= end);
        return await query.OrderByDescending(x => x.StartedAt).Take(200).ToListAsync(ct);
    }

    private async Task<HashSet<string>> AgentKeysForCategory(string category, CancellationToken ct)
    {
        var keys = NalvaAgentCatalog.All.Where(x => x.Category == category).Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var custom = await _db.NalvaCompanyAgents.AsNoTracking().Where(x => x.IsCustom && x.Category == category).Select(x => x.AgentKey).ToListAsync(ct);
        foreach (var key in custom)
            keys.Add(key);
        return keys;
    }

    private async Task<NalvaCompanyAgent> RequireAgentAsync(string key, CancellationToken ct) =>
        await _db.NalvaCompanyAgents.FirstOrDefaultAsync(x => x.AgentKey == key, ct)
        ?? throw new NotFoundException("Activate the agent before changing it.");

    private async Task ReviseAsync(NalvaCompanyAgent row, CancellationToken ct)
    {
        var version = await _db.NalvaAgentRevisions.CountAsync(x => x.CompanyAgentId == row.Id, ct) + 1;
        _db.NalvaAgentRevisions.Add(new NalvaAgentRevision
        {
            CompanyAgentId = row.Id,
            Version = version,
            SnapshotJson = JsonSerializer.Serialize(new { row.AgentKey, row.Status, row.AutonomyLevel, row.Model, row.ToolsJson, row.DefinitionVersion })
        });
    }

    private static void CopyDefinition(NalvaCompanyAgent row, NalvaAgentDefinition definition)
    {
        row.DisplayName = definition.Name;
        row.Description = definition.Description;
        row.RoleTitle = definition.Role;
        row.Category = definition.Category;
        row.DefinitionVersion = definition.Version;
        row.IsCustom = false;
        row.ToolsJson = JsonSerializer.Serialize(definition.Tools);
        row.PermissionsJson = JsonSerializer.Serialize(definition.Permissions);
        row.ResponsibilitiesJson = JsonSerializer.Serialize(definition.Responsibilities);
        row.ApprovalActionsJson = JsonSerializer.Serialize(definition.ApprovalActions);
        row.DailyExecutionLimit = definition.DailyExecutionLimit;
        if (row.AutonomyLevel is < 1 or > 4)
            row.AutonomyLevel = definition.DefaultAutonomy;
    }

    private static IReadOnlyList<string> CeilingTools(NalvaCompanyAgent row)
    {
        if (row.IsCustom)
            return NalvaToolCatalog.All.Select(x => x.Name).ToArray();
        return NalvaAgentCatalog.TryGet(row.AgentKey, out var definition) && definition is not null
            ? definition.Tools
            : NalvaJson.Strings(row.ToolsJson);
    }

    private static string Health(NalvaCompanyAgent? row) =>
        row is null || row.Status != NalvaStatuses.Active ? NalvaStatuses.Unavailable : NalvaStatuses.Healthy;

    private static NalvaTaskDto MapTask(NalvaTask task) =>
        new(task.Id, task.AgentKey, task.Objective, task.Status, task.Priority, task.Deadline, task.RunAfter, task.IntervalMinutes, task.Attempt, task.LastError, task.CompletedExecutionId, task.CreatedAt, task.ParentTaskId);

    private static NalvaWorkflowDto MapWorkflow(NalvaWorkflowDefinition row)
    {
        var steps = JsonSerializer.Deserialize<List<NalvaWorkflowStepWrite>>(row.StepsJson, JsonOptions) ?? [];
        return new NalvaWorkflowDto(
            row.Id,
            row.Key,
            row.Name,
            row.Description,
            row.TriggerKind,
            row.TriggerEvent,
            row.IntervalMinutes,
            row.IsActive,
            steps,
            row.Version,
            row.TimeZoneId,
            row.RecurrenceKind,
            row.LocalTimeOfDay,
            row.DaysOfWeek,
            row.DayOfMonth,
            row.IsPaused,
            row.MaxConcurrentRuns,
            row.NextRunAt,
            row.ScheduleStartUtc,
            row.ScheduleEndUtc,
            row.LastRunAt,
            row.CancelledAt is not null,
            row.ConsecutiveFailures,
            row.LastFailure);
    }

    private NalvaApprovalDto MapApproval(NalvaApproval row)
    {
        var execution = _db.NalvaExecutions.AsNoTracking().FirstOrDefault(x => x.Id == row.ExecutionId);
        string? workflowName = null;
        if (execution?.WorkflowRunId is Guid runId)
        {
            var run = _db.NalvaWorkflowRuns.AsNoTracking().FirstOrDefault(x => x.Id == runId);
            if (run is not null)
            {
                var definition = _db.NalvaWorkflowDefinitions.AsNoTracking().FirstOrDefault(x => x.Id == run.DefinitionId);
                workflowName = definition?.Name;
            }
        }

        var related = ExtractApprovalSummary(row.ArgsJson, row.ContextJson);
        return new NalvaApprovalDto(
            row.Id,
            row.ExecutionId,
            row.AgentKey,
            row.Action,
            row.ToolName,
            row.Risk,
            row.ContextJson,
            row.Status,
            row.RequestedAt,
            row.RequestedByUserId,
            row.ApproverUserId,
            row.DecidedAt,
            row.DecisionNote,
            row.ExecutionResult,
            execution?.WorkflowRunId,
            workflowName,
            execution?.Objective,
            related.Entity,
            related.Amount,
            related.Risk,
            row.ExpiresAt,
            NalvaCollaborationService.Fingerprint(row.ArgsJson),
            row.ArgsJson);
    }

    private static (string? Entity, string? Amount, string? Risk) ExtractApprovalSummary(string? argsJson, string? contextJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? (contextJson ?? "{}") : argsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return (null, null, null);
            string? entity = null;
            if (doc.RootElement.TryGetProperty("customerId", out var customer))
                entity = $"customer:{customer}";
            else if (doc.RootElement.TryGetProperty("orderId", out var order))
                entity = $"order:{order}";
            else if (doc.RootElement.TryGetProperty("leadId", out var lead))
                entity = $"lead:{lead}";
            else if (doc.RootElement.TryGetProperty("supplierId", out var supplier))
                entity = $"supplier:{supplier}";
            else if (doc.RootElement.TryGetProperty("ticketId", out var ticket))
                entity = $"ticket:{ticket}";

            string? amount = null;
            if (doc.RootElement.TryGetProperty("amount", out var amt))
                amount = amt.ToString();
            else if (doc.RootElement.TryGetProperty("total", out var total))
                amount = total.ToString();
            else if (doc.RootElement.TryGetProperty("grandTotal", out var grand))
                amount = grand.ToString();

            string? risk = null;
            if (doc.RootElement.TryGetProperty("risk", out var riskNode))
                risk = riskNode.ToString();
            return (entity, amount, risk);
        }
        catch (JsonException)
        {
            return (null, null, null);
        }
    }

    private static DateTime MonthStart() => new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    private static int CountOutcome(IEnumerable<NalvaExecution> executions, string toolName) =>
        executions.Count(x =>
            x.Status == NalvaStatuses.Completed &&
            ((!string.IsNullOrWhiteSpace(x.ToolLogJson) && x.ToolLogJson.Contains($"\"{toolName}\"", StringComparison.OrdinalIgnoreCase))
             || (!string.IsNullOrWhiteSpace(x.PlanJson) && x.PlanJson.Contains($"\"{toolName}\"", StringComparison.OrdinalIgnoreCase))
             || (!string.IsNullOrWhiteSpace(x.ResultJson) && x.ResultJson.Contains($"\"{toolName}\"", StringComparison.OrdinalIgnoreCase))));

    private void Require(string permission)
    {
        if (!_user.HasPermission(permission))
            throw new ForbiddenException("You do not have permission to perform this action.");
    }
}
