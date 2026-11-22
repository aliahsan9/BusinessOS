using System.Text.Json;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Autopilot;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Domain.Billing;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Billing;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BusinessOS.Infrastructure.AI.Autopilot;

public sealed class BusinessAutopilotService : IBusinessAutopilotService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly BusinessOSDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly ITenantProvider _tenant;
    private readonly INalvaWorkforceService _workforce;
    private readonly INalvaWorkflowEngine _workflows;
    private readonly IMemoryCache _cache;

    public BusinessAutopilotService(
        BusinessOSDbContext db,
        ICurrentUserService user,
        ITenantProvider tenant,
        INalvaWorkforceService workforce,
        INalvaWorkflowEngine workflows,
        IMemoryCache cache)
    {
        _db = db;
        _user = user;
        _tenant = tenant;
        _workforce = workforce;
        _workflows = workflows;
        _cache = cache;
    }

    public async Task<AutopilotConfigDto> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        return Map(await EnsureConfigAsync(cancellationToken));
    }

    public async Task<AutopilotConfigDto> SaveConfigAsync(AutopilotConfigWriteRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        await EnsureAutomationsAsync(cancellationToken);
        var row = await EnsureConfigAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Mode))
        {
            if (!AutopilotModes.IsValid(request.Mode))
                throw new BadRequestException("Mode must be Copilot, Supervised, or Controlled.");
            row.Mode = AutopilotModes.Normalize(request.Mode);
        }
        if (request.IsEnabled is bool enabled)
            row.IsEnabled = enabled;
        if (request.Objectives is not null)
            row.ObjectivesJson = JsonSerializer.Serialize(request.Objectives, JsonOptions);
        if (request.Priorities is not null)
            row.PrioritiesJson = JsonSerializer.Serialize(request.Priorities, JsonOptions);
        if (request.MonthlySalesTarget is not null)
            row.MonthlySalesTarget = request.MonthlySalesTarget;
        if (request.MarketingBudgetMonthly is not null)
            row.MarketingBudgetMonthly = request.MarketingBudgetMonthly;
        if (request.SpendingLimitPerAction is not null)
            row.SpendingLimitPerAction = request.SpendingLimitPerAction;
        if (request.DailySpendingLimit is not null)
            row.DailySpendingLimit = request.DailySpendingLimit;
        if (request.InventoryPolicyJson is not null)
            row.InventoryPolicyJson = request.InventoryPolicyJson;
        if (request.CustomerServicePolicyJson is not null)
            row.CustomerServicePolicyJson = request.CustomerServicePolicyJson;
        if (request.ApprovalRequirementsJson is not null)
            row.ApprovalRequirementsJson = request.ApprovalRequirementsJson;
        if (request.WorkingHoursJson is not null)
            row.WorkingHoursJson = request.WorkingHoursJson;
        if (request.AgentResponsibilitiesJson is not null)
            row.AgentResponsibilitiesJson = request.AgentResponsibilitiesJson;
        if (request.EnabledScenarioKeys is not null)
            row.EnabledScenarioKeysJson = JsonSerializer.Serialize(request.EnabledScenarioKeys, JsonOptions);
        if (request.ControlledAllowlistTools is not null)
            row.ControlledAllowlistToolsJson = JsonSerializer.Serialize(request.ControlledAllowlistTools, JsonOptions);
        if (request.AllowedMediumTools is not null)
            row.AllowedMediumToolsJson = JsonSerializer.Serialize(request.AllowedMediumTools, JsonOptions);
        if (!string.IsNullOrWhiteSpace(request.TimeZoneId))
            row.TimeZoneId = request.TimeZoneId.Trim();
        if (request.Notes is not null)
            row.Notes = request.Notes;

        // Never allow Controlled mode without spending limits for high-risk financial safety.
        if (string.Equals(row.Mode, AutopilotModes.Controlled, StringComparison.OrdinalIgnoreCase)
            && row.SpendingLimitPerAction is null or <= 0)
        {
            row.SpendingLimitPerAction = 500m;
        }

        await AlignCompanyApprovalPolicyAsync(row, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        InvalidateSafetyCache();
        await LogAsync("config.save", "Completed", $"Autopilot config saved ({row.Mode})", cancellationToken: cancellationToken);
        return Map(row);
    }

    public async Task<AutopilotConfigDto> ActivateAsync(AutopilotActivateRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        await EnsureAutomationsAsync(cancellationToken);
        if (!AutopilotModes.IsValid(request.Mode))
            throw new BadRequestException("Mode must be Copilot, Supervised, or Controlled.");

        var row = await EnsureConfigAsync(cancellationToken);
        row.Mode = AutopilotModes.Normalize(request.Mode);
        row.IsEnabled = true;
        row.EmergencyStop = false;
        row.LastActivatedAt = DateTime.UtcNow;
        row.LastActivatedByUserId = _user.UserId;

        var scenarios = request.ScenarioKeys is { Count: > 0 }
            ? request.ScenarioKeys.ToList()
            : AutopilotScenarioKeys.DefaultSupervised.ToList();
        row.EnabledScenarioKeysJson = JsonSerializer.Serialize(scenarios, JsonOptions);

        if (string.Equals(row.Mode, AutopilotModes.Controlled, StringComparison.OrdinalIgnoreCase)
            && row.SpendingLimitPerAction is null or <= 0)
            row.SpendingLimitPerAction = 500m;

        if (request.ActivateRecommendedAgents)
        {
            await _workforce.ApplyWorkforcePackAsync(new NalvaApplyWorkforcePackRequest
            {
                ActivateRecommendedAgents = true,
                ActivateRecommendedWorkflows = false
            }, cancellationToken);
        }

        await AlignCompanyApprovalPolicyAsync(row, cancellationToken);

        foreach (var key in scenarios)
            await EnsureScenarioWorkflowAsync(key, activate: row.Mode != AutopilotModes.Copilot, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        InvalidateSafetyCache();
        await LogAsync("autopilot.activate", "Completed", $"Activated {row.Mode} with {scenarios.Count} scenarios",
            detailJson: JsonSerializer.Serialize(new { scenarios, mode = row.Mode }, JsonOptions),
            cancellationToken: cancellationToken);
        return Map(row);
    }

    public async Task<AutopilotConfigDto> SetEmergencyStopAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        var row = await EnsureConfigAsync(cancellationToken);
        row.EmergencyStop = enabled;
        if (enabled)
        {
            // Pause all scheduled Autopilot scenarios and cancel runnable autonomous tasks.
            var keys = ParseStrings(row.EnabledScenarioKeysJson);
            var defs = await _db.NalvaWorkflowDefinitions
                .Where(x => keys.Contains(x.Key))
                .ToListAsync(cancellationToken);
            foreach (var def in defs)
                def.IsPaused = true;

            var runnable = await _db.NalvaTasks
                .Where(x => x.Status == NalvaStatuses.Queued || x.Status == NalvaStatuses.Assigned || x.Status == NalvaStatuses.Pending)
                .ToListAsync(cancellationToken);
            foreach (var task in runnable)
                task.Status = NalvaStatuses.Paused;
        }

        await _db.SaveChangesAsync(cancellationToken);
        InvalidateSafetyCache();
        await LogAsync(enabled ? "emergency.stop" : "emergency.clear", "Completed",
            enabled ? "Emergency stop engaged" : "Emergency stop cleared",
            cancellationToken: cancellationToken);
        return Map(row);
    }

    public async Task<AutopilotConfigDto> PauseAllAgentsAsync(CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        var row = await EnsureConfigAsync(cancellationToken);
        var agents = await _db.NalvaCompanyAgents.AsNoTracking()
            .Where(x => x.Status == NalvaStatuses.Active)
            .Select(x => x.AgentKey)
            .ToListAsync(cancellationToken);
        row.PausedAgentKeysJson = JsonSerializer.Serialize(agents, JsonOptions);

        var defs = await _db.NalvaWorkflowDefinitions.Where(x => x.IsActive && !x.IsPaused).ToListAsync(cancellationToken);
        foreach (var def in defs)
            def.IsPaused = true;

        await _db.SaveChangesAsync(cancellationToken);
        InvalidateSafetyCache();
        await LogAsync("agents.pause_all", "Completed", $"Paused {agents.Count} agents", cancellationToken: cancellationToken);
        return Map(row);
    }

    public async Task<AutopilotConfigDto> PauseAgentAsync(string agentKey, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        if (string.IsNullOrWhiteSpace(agentKey))
            throw new BadRequestException("Agent key is required.");
        var row = await EnsureConfigAsync(cancellationToken);
        var paused = ParseStrings(row.PausedAgentKeysJson).ToHashSet(StringComparer.OrdinalIgnoreCase);
        paused.Add(agentKey.Trim());
        row.PausedAgentKeysJson = JsonSerializer.Serialize(paused.ToList(), JsonOptions);
        await _db.SaveChangesAsync(cancellationToken);
        InvalidateSafetyCache();
        await LogAsync("agent.pause", "Completed", $"Paused agent {agentKey}", agentKey: agentKey, cancellationToken: cancellationToken);
        return Map(row);
    }

    public async Task<AutopilotConfigDto> ResumeAgentAsync(string agentKey, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        var row = await EnsureConfigAsync(cancellationToken);
        var paused = ParseStrings(row.PausedAgentKeysJson)
            .Where(x => !string.Equals(x, agentKey, StringComparison.OrdinalIgnoreCase))
            .ToList();
        row.PausedAgentKeysJson = JsonSerializer.Serialize(paused, JsonOptions);
        await _db.SaveChangesAsync(cancellationToken);
        InvalidateSafetyCache();
        return Map(row);
    }

    public async Task<AutopilotConfigDto> ClearAgentPausesAsync(CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        var row = await EnsureConfigAsync(cancellationToken);
        row.PausedAgentKeysJson = "[]";
        var keys = ParseStrings(row.EnabledScenarioKeysJson);
        var defs = await _db.NalvaWorkflowDefinitions.Where(x => keys.Contains(x.Key)).ToListAsync(cancellationToken);
        foreach (var def in defs)
        {
            def.IsPaused = false;
            if (!def.IsActive && row.IsEnabled && row.Mode != AutopilotModes.Copilot)
                def.IsActive = true;
        }
        await _db.SaveChangesAsync(cancellationToken);
        InvalidateSafetyCache();
        return Map(row);
    }

    public async Task<AutopilotScenarioResult> RunScenarioAsync(string scenarioKey, bool dryRun = false, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        await EnsureAutomationsAsync(cancellationToken);
        var config = await EnsureConfigAsync(cancellationToken);
        if (config.EmergencyStop)
            throw new ForbiddenException("Emergency stop is active.");
        if (!config.IsEnabled && !dryRun)
            throw new BadRequestException("Activate Autopilot before running scenarios.");

        var def = await EnsureScenarioWorkflowAsync(scenarioKey, activate: true, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var run = await _workflows.RunAsync(
            def.Id,
            argumentsJson: null,
            new NalvaWorkflowRunOptions
            {
                DryRun = dryRun || config.Mode == AutopilotModes.Copilot,
                RequireActive = false,
                TriggerSource = "Autopilot",
                CorrelationId = $"autopilot:{scenarioKey}:{Guid.NewGuid():N}"
            },
            cancellationToken);

        await LogAsync("scenario.run", run.Status, $"Scenario {scenarioKey} → {run.Status}",
            scenarioKey: scenarioKey,
            workflowRunId: run.Id,
            detailJson: JsonSerializer.Serialize(new { dryRun = run.IsDryRun, summary = run.Summary }, JsonOptions),
            cancellationToken: cancellationToken);

        return new AutopilotScenarioResult
        {
            ScenarioKey = scenarioKey,
            WorkflowDefinitionId = def.Id,
            WorkflowRunId = run.Id,
            Status = run.Status,
            Summary = run.Summary ?? run.Error ?? run.Status
        };
    }

    public async Task<AutopilotOpsSnapshotDto> GetOpsSnapshotAsync(CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        var config = await EnsureConfigAsync(cancellationToken);
        var since = DateTime.UtcNow.AddHours(-24);

        var activeRuns = await _db.NalvaWorkflowRuns.AsNoTracking()
            .Where(x => x.Status == "Running" || x.Status == "Pending" || x.Status == NalvaStatuses.AwaitingApproval)
            .OrderByDescending(x => x.StartedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        var pendingApprovals = await _db.NalvaApprovals.AsNoTracking()
            .Where(x => x.Status == NalvaStatuses.Pending)
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        var failed = await _db.NalvaExecutions.AsNoTracking()
            .Where(x => x.StartedAt >= since && x.Status == NalvaStatuses.Failed)
            .OrderByDescending(x => x.StartedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        var upcoming = await _db.NalvaWorkflowDefinitions.AsNoTracking()
            .Where(x => x.IsActive && !x.IsPaused && x.NextRunAt != null)
            .OrderBy(x => x.NextRunAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        var recentLogs = await _db.AutopilotOperationLogs.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(30)
            .ToListAsync(cancellationToken);

        var exec24 = await _db.NalvaExecutions.AsNoTracking()
            .Where(x => x.StartedAt >= since)
            .Select(x => new { x.Status, x.EstimatedCostUsd, x.ToolLogJson })
            .ToListAsync(cancellationToken);

        return new AutopilotOpsSnapshotDto
        {
            Mode = config.Mode,
            IsEnabled = config.IsEnabled,
            EmergencyStop = config.EmergencyStop,
            ActiveWorkflows = activeRuns.Count,
            RunningAgents = await _db.NalvaExecutions.CountAsync(x => x.Status == NalvaStatuses.Running, cancellationToken),
            CompletedOperations24h = exec24.Count(x => x.Status == NalvaStatuses.Completed),
            PendingApprovals = pendingApprovals.Count,
            FailedExecutions24h = failed.Count,
            UpcomingSchedules = upcoming.Count,
            ExternalCommunications24h = await _db.AutopilotOperationLogs.CountAsync(
                x => x.CreatedAt >= since && x.ExternalEffect, cancellationToken),
            FinancialOperations24h = await _db.AutopilotOperationLogs.CountAsync(
                x => x.CreatedAt >= since && x.FinancialEffect, cancellationToken),
            AiExecutions24h = exec24.Count,
            EstimatedCostUsd24h = exec24.Sum(x => x.EstimatedCostUsd),
            PausedAgentKeys = ParseStrings(config.PausedAgentKeysJson),
            ActiveRuns = activeRuns.Select(x => new AutopilotOpsItemDto
            {
                Id = x.Id, Kind = "workflow", Title = x.CorrelationId ?? x.Id.ToString("N")[..8],
                Status = x.Status, At = x.StartedAt, Detail = x.Summary
            }).ToList(),
            PendingApprovalItems = pendingApprovals.Select(x => new AutopilotOpsItemDto
            {
                Id = x.Id, Kind = "approval", Title = x.Action, Status = x.Status,
                AgentKey = x.AgentKey, At = x.CreatedAt, Detail = x.ToolName
            }).ToList(),
            FailedItems = failed.Select(x => new AutopilotOpsItemDto
            {
                Id = x.Id, Kind = "execution", Title = x.Objective, Status = x.Status,
                AgentKey = x.AgentKey, At = x.StartedAt, Detail = x.Summary
            }).ToList(),
            Upcoming = upcoming.Select(x => new AutopilotOpsItemDto
            {
                Id = x.Id, Kind = "schedule", Title = x.Name, Status = x.IsPaused ? "Paused" : "Scheduled",
                At = x.NextRunAt, Detail = x.Key
            }).ToList(),
            RecentOutcomes = recentLogs.Select(x => new AutopilotOpsItemDto
            {
                Id = x.Id, Kind = x.Kind, Title = x.Summary, Status = x.Status,
                AgentKey = x.AgentKey, At = x.CreatedAt, Detail = x.ScenarioKey
            }).ToList()
        };
    }

    public async Task RetryFailedTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        var config = await EnsureConfigAsync(cancellationToken);
        if (config.EmergencyStop)
            throw new ForbiddenException("Emergency stop is active.");
        await _workforce.RetryTaskAsync(taskId, cancellationToken);
        await LogAsync("task.retry", "Queued", $"Retried task {taskId}", taskId: taskId, cancellationToken: cancellationToken);
    }

    public async Task StopWorkflowRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        var run = await _db.NalvaWorkflowRuns.FirstOrDefaultAsync(x => x.Id == runId, cancellationToken)
            ?? throw new NotFoundException("Workflow run was not found.");
        if (run.Status is "Completed" or "Cancelled" or "Failed")
            return;
        run.Status = "Cancelled";
        run.CompletedAt = DateTime.UtcNow;
        run.Error = "Stopped by Autopilot operations control.";
        var tasks = await _db.NalvaTasks
            .Where(x => x.WorkflowRunId == runId && (x.Status == NalvaStatuses.Queued || x.Status == NalvaStatuses.Assigned || x.Status == NalvaStatuses.Running || x.Status == NalvaStatuses.Paused))
            .ToListAsync(cancellationToken);
        foreach (var task in tasks)
            task.Status = NalvaStatuses.Cancelled;
        await _db.SaveChangesAsync(cancellationToken);
        await LogAsync("workflow.stop", "Cancelled", $"Stopped workflow run {runId}", workflowRunId: runId, cancellationToken: cancellationToken);
    }

    private async Task<NalvaWorkflowDefinition> EnsureScenarioWorkflowAsync(string scenarioKey, bool activate, CancellationToken cancellationToken)
    {
        if (!NalvaWorkflowCatalog.TryGet(scenarioKey, out var template) || template is null)
            throw new NotFoundException($"Unknown Autopilot scenario '{scenarioKey}'.");

        var row = await _db.NalvaWorkflowDefinitions.FirstOrDefaultAsync(x => x.Key == template.Key, cancellationToken);
        row ??= new NalvaWorkflowDefinition { Key = template.Key };
        if (_db.Entry(row).State == EntityState.Detached)
            _db.NalvaWorkflowDefinitions.Add(row);

        row.Name = template.Name;
        row.Description = template.Description;
        row.TriggerKind = template.TriggerKind;
        row.TriggerEvent = template.TriggerEvent;
        row.IntervalMinutes = template.IntervalMinutes;
        row.StepsJson = JsonSerializer.Serialize(template.Steps.Select(s => new NalvaWorkflowStepWrite
        {
            Key = s.Key,
            AgentKey = s.AgentKey,
            Objective = s.Objective,
            Tools = s.Tools.ToList(),
            ParallelGroup = s.ParallelGroup,
            Condition = s.Condition
        }), JsonOptions);
        row.MaxToolCalls = NalvaLimits.MaxToolCalls;
        if (activate)
        {
            row.IsActive = true;
            row.IsPaused = false;
            row.CancelledAt = null;
            if (row.TriggerKind == "Scheduled" && row.NextRunAt is null)
                row.NextRunAt = DateTime.UtcNow.AddMinutes(1);
        }

        return row;
    }

    private async Task AlignCompanyApprovalPolicyAsync(BusinessAutopilotConfig row, CancellationToken cancellationToken)
    {
        var settings = await _db.NalvaCompanySettings.FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            settings = new NalvaCompanySettings { TenantId = _tenant.TenantId };
            _db.NalvaCompanySettings.Add(settings);
        }

        // Copilot and Supervised always require external approvals. Controlled still defaults true
        // unless tools are explicitly allowlisted (enforced by safety engine, not by disabling this flag).
        settings.RequireApprovalForExternalActions = true;
        if (string.Equals(row.Mode, AutopilotModes.Copilot, StringComparison.OrdinalIgnoreCase))
            settings.AutonomyCeiling = Math.Min(settings.AutonomyCeiling, 2);
    }

    private async Task EnsureAutomationsAsync(CancellationToken cancellationToken)
    {
        await CommercialGate.EnsureFeatureAsync(_db, _tenant.TenantId, Domain.Billing.CommercialFeatureKeys.Automations, cancellationToken);
    }

    private async Task<BusinessAutopilotConfig> EnsureConfigAsync(CancellationToken cancellationToken)
    {
        var row = await _db.BusinessAutopilotConfigs.FirstOrDefaultAsync(cancellationToken);
        if (row is not null)
            return row;
        row = new BusinessAutopilotConfig
        {
            TenantId = _tenant.TenantId,
            Mode = AutopilotModes.Copilot,
            IsEnabled = false,
            EnabledScenarioKeysJson = JsonSerializer.Serialize(AutopilotScenarioKeys.DefaultSupervised, JsonOptions)
        };
        _db.BusinessAutopilotConfigs.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return row;
    }

    private async Task LogAsync(
        string kind,
        string status,
        string summary,
        string? scenarioKey = null,
        string? agentKey = null,
        Guid? workflowRunId = null,
        Guid? taskId = null,
        Guid? executionId = null,
        Guid? approvalId = null,
        string? detailJson = null,
        bool external = false,
        bool financial = false,
        CancellationToken cancellationToken = default)
    {
        _db.AutopilotOperationLogs.Add(new AutopilotOperationLog
        {
            TenantId = _tenant.TenantId,
            Kind = kind,
            Status = status,
            ScenarioKey = scenarioKey,
            AgentKey = agentKey,
            WorkflowRunId = workflowRunId,
            TaskId = taskId,
            ExecutionId = executionId,
            ApprovalId = approvalId,
            Summary = summary,
            DetailJson = detailJson ?? "{}",
            ExternalEffect = external,
            FinancialEffect = financial,
            CorrelationId = workflowRunId?.ToString()
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    private void InvalidateSafetyCache() => _cache.Remove("autopilot-config:current");

    private void Require(string permission)
    {
        if (!_user.HasPermission(permission))
            throw new ForbiddenException("Missing permission.");
    }

    private static IReadOnlyList<string> ParseStrings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    private static AutopilotConfigDto Map(BusinessAutopilotConfig row) => new()
    {
        Id = row.Id,
        Mode = row.Mode,
        IsEnabled = row.IsEnabled,
        EmergencyStop = row.EmergencyStop,
        Objectives = ParseStrings(row.ObjectivesJson),
        Priorities = ParseStrings(row.PrioritiesJson),
        MonthlySalesTarget = row.MonthlySalesTarget,
        MarketingBudgetMonthly = row.MarketingBudgetMonthly,
        SpendingLimitPerAction = row.SpendingLimitPerAction,
        DailySpendingLimit = row.DailySpendingLimit,
        InventoryPolicyJson = row.InventoryPolicyJson,
        CustomerServicePolicyJson = row.CustomerServicePolicyJson,
        ApprovalRequirementsJson = row.ApprovalRequirementsJson,
        WorkingHoursJson = row.WorkingHoursJson,
        AgentResponsibilitiesJson = row.AgentResponsibilitiesJson,
        PausedAgentKeys = ParseStrings(row.PausedAgentKeysJson),
        EnabledScenarioKeys = ParseStrings(row.EnabledScenarioKeysJson),
        ControlledAllowlistTools = ParseStrings(row.ControlledAllowlistToolsJson),
        AllowedMediumTools = ParseStrings(row.AllowedMediumToolsJson),
        TimeZoneId = row.TimeZoneId,
        Notes = row.Notes,
        LastActivatedAt = row.LastActivatedAt
    };
}
