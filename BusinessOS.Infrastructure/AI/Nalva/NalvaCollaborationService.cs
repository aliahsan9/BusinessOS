using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.AI.Nalva;

public sealed class NalvaCollaborationService : INalvaCollaborationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> CollaborationKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "lead-to-customer-collaboration",
        "order-to-fulfillment",
        "low-stock-to-procurement",
        "complaint-to-resolution",
        "weekly-business-review"
    };

    private readonly BusinessOSDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly ITenantProvider _tenant;
    private readonly INalvaWorkforceService _workforce;
    private readonly INalvaWorkflowEngine _workflows;

    public NalvaCollaborationService(
        BusinessOSDbContext db,
        ICurrentUserService user,
        ITenantProvider tenant,
        INalvaWorkforceService workforce,
        INalvaWorkflowEngine workflows)
    {
        _db = db;
        _user = user;
        _tenant = tenant;
        _workforce = workforce;
        _workflows = workflows;
    }

    public async Task<NalvaObjectivePlanDto> PlanObjectiveAsync(NalvaObjectivePlanRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        var objective = NalvaGuard.Clip(request.Objective, 2000);
        var match = NalvaObjectiveRouter.Match(objective);
        await EnsureCollaborationTemplatesAsync(cancellationToken);

        NalvaWorkflowCatalog.TryGet(match.WorkflowKey, out var template);
        var steps = template?.Steps.Select(step => new NalvaWorkflowStepWrite
        {
            Key = step.Key,
            AgentKey = step.AgentKey,
            Objective = step.Objective,
            Tools = step.Tools.ToList(),
            Condition = step.Condition,
            ParallelGroup = step.ParallelGroup,
            OutputKey = step.Key
        }).ToList() ?? [];

        var agents = await _db.NalvaCompanyAgents.AsNoTracking().ToListAsync(cancellationToken);
        var missing = match.RequiredAgents
            .Where(key => !agents.Any(a => string.Equals(a.AgentKey, key, StringComparison.OrdinalIgnoreCase) && a.Status == NalvaStatuses.Active))
            .ToList();

        var definition = await _db.NalvaWorkflowDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == match.WorkflowKey, cancellationToken);

        var canStart = match.CanStart && missing.Count == 0 && definition is not null;
        var block = match.BlockReason;
        if (missing.Count > 0)
        {
            canStart = request.ActivateMissingAgents && match.CanStart && definition is not null;
            if (!canStart)
                block = $"Activate required agents first: {string.Join(", ", missing)}.";
        }

        // Structured task validation for each planned step (capabilities + tools).
        foreach (var step in steps)
        {
            if (!NalvaAgentCatalog.TryGet(step.AgentKey, out var agent) || agent is null)
            {
                canStart = false;
                block = $"Unknown agent '{step.AgentKey}' in workflow plan.";
                break;
            }
            var invalidTool = step.Tools.FirstOrDefault(tool => !agent.Tools.Contains(tool, StringComparer.OrdinalIgnoreCase));
            if (invalidTool is not null)
            {
                canStart = false;
                block = $"Agent '{step.AgentKey}' is not granted tool '{invalidTool}'.";
                break;
            }

            var envelope = new NalvaAgentTaskEnvelope
            {
                TaskId = Guid.NewGuid(),
                WorkflowId = definition?.Id,
                TenantId = _tenant.HasTenant() ? _tenant.TenantId : Guid.Empty,
                RequestingUserId = _user.UserId,
                AssignedAgent = step.AgentKey,
                Objective = step.Objective,
                Permissions = step.Tools,
                ApprovalRequirements = step.Tools
                    .Select(t => NalvaToolCatalog.TryGet(t, out var tool) ? tool!.ApprovalAction : null)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                CorrelationId = request.IdempotencyKey ?? ""
            };
            if (!NalvaCollaborationProtocol.TryValidateTask(envelope, out var taskError))
            {
                canStart = false;
                block = taskError;
                break;
            }
        }

        return new NalvaObjectivePlanDto(
            objective,
            match.WorkflowKey,
            match.Name,
            match.Description,
            match.Confidence,
            match.RequiredAgents,
            missing,
            steps,
            match.ClarifyingQuestions,
            canStart,
            canStart ? null : block,
            definition?.Id,
            null,
            null,
            null);
    }

    public async Task<NalvaObjectivePlanDto> StartObjectiveAsync(NalvaObjectivePlanRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        var plan = await PlanObjectiveAsync(request, cancellationToken);
        if (!plan.CanStart || plan.WorkflowDefinitionId is null)
            return plan with { BlockReason = plan.BlockReason ?? "Objective cannot start yet." };

        if (request.ActivateMissingAgents)
        {
            foreach (var key in plan.MissingAgents)
                await _workforce.ActivateAsync(key, cancellationToken);
        }

        if (request.ActivateWorkflow)
            await _workforce.SetWorkflowActiveAsync(plan.WorkflowDefinitionId.Value, true, cancellationToken);

        var args = string.IsNullOrWhiteSpace(request.ArgumentsJson) ? "{}" : request.ArgumentsJson!;
        if (NalvaGuard.RejectsArguments(args, out var reason))
            throw new BadRequestException(reason);

        var context = JsonNode.Parse(args) as JsonObject ?? new JsonObject();
        context["ownerObjective"] = plan.Objective;
        context["orchestrator"] = "navla";
        context["protocol"] = "NalvaAgentTaskEnvelope";
        context["plannedAt"] = DateTime.UtcNow.ToString("O");

        var correlation = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? $"obj-{Guid.NewGuid():N}"
            : request.IdempotencyKey!;

        var run = await _workflows.RunAsync(
            plan.WorkflowDefinitionId.Value,
            context.ToJsonString(),
            new NalvaWorkflowRunOptions
            {
                TriggerSource = "NavlaObjective",
                CorrelationId = correlation,
                RequireActive = true
            },
            cancellationToken);

        var report = await BuildFinalReportAsync(run.Id, cancellationToken);
        return plan with
        {
            MissingAgents = [],
            CanStart = true,
            BlockReason = null,
            WorkflowRunId = run.Id,
            RunStatus = run.Status,
            ReportSummary = report
        };
    }

    public async Task<NalvaWorkforceBoardDto> GetWorkforceBoardAsync(CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        await EnsureCollaborationTemplatesAsync(cancellationToken);

        var overview = await _workforce.GetOverviewAsync(cancellationToken);
        var agents = await _workforce.GetMyAgentsAsync(cancellationToken);
        var runs = await _workforce.GetWorkflowRunsAsync(cancellationToken);
        var active = runs.Where(x => x.Status is NalvaStatuses.Running or NalvaStatuses.AwaitingApproval or NalvaStatuses.Paused).Take(25).ToList();
        var recent = runs.Take(25).ToList();
        var failedTasks = (await _workforce.GetTasksAsync(null, null, cancellationToken))
            .Where(x => x.Status is NalvaStatuses.Failed or NalvaStatuses.DeadLetter or NalvaStatuses.Paused or NalvaStatuses.AwaitingApproval)
            .Take(50)
            .ToList();
        var approvals = await _workforce.GetApprovalsAsync(NalvaStatuses.Pending, cancellationToken);
        var executions = await _workforce.GetExecutionsAsync(null, null, null, null, cancellationToken);

        var definitions = await _db.NalvaWorkflowDefinitions.AsNoTracking().ToListAsync(cancellationToken);
        var collab = new List<NalvaCollaborationWorkflowInfo>();
        foreach (var key in CollaborationKeys)
        {
            if (!NalvaWorkflowCatalog.TryGet(key, out var template) || template is null)
                continue;
            var row = definitions.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            collab.Add(new NalvaCollaborationWorkflowInfo(
                template.Key,
                template.Name,
                template.Description,
                template.Category,
                template.Steps.Select(s => s.AgentKey).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                row?.IsActive == true,
                row?.Id));
        }

        var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var spend = await _db.NalvaExecutions.AsNoTracking()
            .Where(x => x.StartedAt >= month)
            .SumAsync(x => (decimal?)x.EstimatedCostUsd, cancellationToken) ?? 0m;

        return new NalvaWorkforceBoardDto(
            overview,
            agents,
            active,
            recent,
            failedTasks,
            approvals,
            executions.Take(40).ToList(),
            collab,
            spend,
            true);
    }

    public async Task<NalvaWorkflowDetailDto> GetWorkflowDetailAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        var run = await _db.NalvaWorkflowRuns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == runId, cancellationToken)
            ?? throw new NotFoundException("Workflow run was not found.");
        var definition = await _db.NalvaWorkflowDefinitions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == run.DefinitionId, cancellationToken);
        var steps = JsonSerializer.Deserialize<List<NalvaWorkflowStepWrite>>(
            string.IsNullOrWhiteSpace(run.StepsSnapshotJson) ? definition?.StepsJson ?? "[]" : run.StepsSnapshotJson,
            JsonOptions) ?? [];

        var executions = await _db.NalvaExecutions.AsNoTracking()
            .Where(x => x.WorkflowRunId == runId)
            .OrderBy(x => x.StartedAt)
            .Take(100)
            .ToListAsync(cancellationToken);
        var executionDtos = executions.Select(x => new NalvaExecutionDto(
            x.Id, x.AgentKey, x.TaskId, x.StartedAt, x.CompletedAt, x.Status, x.Model,
            x.InputTokens, x.OutputTokens, x.ToolCallCount, x.ApprovalCount, x.RetryAttempt,
            x.DurationMs, x.EstimatedCostUsd, x.CostIsEstimate, x.Error, x.Summary)).ToList();

        var executionIds = executions.Select(x => x.Id).ToHashSet();
        var approvals = (await _workforce.GetApprovalsAsync(null, cancellationToken))
            .Where(x => executionIds.Contains(x.ExecutionId) || x.WorkflowRunId == runId)
            .ToList();

        var results = new List<NalvaAgentResultEnvelope>();
        foreach (var execution in executions)
        {
            if (NalvaCollaborationProtocol.TryParseResult(execution.ResultJson, out var envelope, out _) && envelope is not null)
            {
                results.Add(envelope);
                continue;
            }

            results.Add(NalvaCollaborationProtocol.FromExecution(
                execution.TaskId ?? execution.Id,
                runId,
                new NalvaExecutionResult
                {
                    ExecutionId = execution.Id,
                    Status = execution.Status,
                    AgentKey = execution.AgentKey,
                    Summary = execution.Summary,
                    ResultJson = execution.ResultJson,
                    Error = execution.Error,
                    ErrorClass = NalvaErrorClass.None,
                    InputTokens = execution.InputTokens,
                    OutputTokens = execution.OutputTokens,
                    EstimatedCostUsd = execution.EstimatedCostUsd,
                    CostIsEstimate = execution.CostIsEstimate,
                    Tools = []
                }));
        }

        var runDto = new NalvaWorkflowRunDto(
            run.Id, run.DefinitionId, definition?.Name ?? "Workflow", run.Status, run.CurrentStep,
            run.Summary, run.Error, run.StartedAt, run.CompletedAt, run.TriggerSource, run.IsDryRun,
            run.EstimatedCostUsd, run.CorrelationId, definition?.Key);

        return new NalvaWorkflowDetailDto(
            runDto,
            steps,
            run.ContextJson,
            run.StepLogJson,
            executionDtos,
            approvals,
            results,
            await BuildFinalReportAsync(runId, cancellationToken));
    }

    public async Task<IReadOnlyList<NalvaMemoryDto>> GetSharedContextAsync(string? kind, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        var query = _db.NalvaMemoryRecords.AsNoTracking()
            .Where(x => x.Kind == "BusinessDecision" || x.Kind == "SharedBusinessContext" || x.Kind == "BusinessPolicy");
        if (!string.IsNullOrWhiteSpace(kind))
            query = query.Where(x => x.Kind == kind);
        var rows = await query.OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(cancellationToken);
        return rows.Select(x => new NalvaMemoryDto(x.Id, x.AgentKey, x.Kind, x.Title, NalvaGuard.Clip(x.Content, 240), x.CreatedAt)).ToList();
    }

    public async Task<NalvaMemoryDto> StoreSharedDecisionAsync(string title, string content, Guid? executionId, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        if (string.IsNullOrWhiteSpace(content))
            throw new BadRequestException("Decision content is required.");
        var row = new NalvaMemoryRecord
        {
            AgentKey = "ceo",
            Kind = "BusinessDecision",
            Title = NalvaGuard.Clip(title, 200),
            Content = NalvaGuard.Clip(content, 4000),
            ExecutionId = executionId,
            ExpiresAt = DateTime.UtcNow.AddDays(180)
        };
        _db.NalvaMemoryRecords.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return new NalvaMemoryDto(row.Id, row.AgentKey, row.Kind, row.Title, NalvaGuard.Clip(row.Content, 240), row.CreatedAt);
    }

    private async Task<string> BuildFinalReportAsync(Guid runId, CancellationToken ct)
    {
        var detail = await _db.NalvaWorkflowRuns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == runId, ct);
        if (detail is null)
            return "Workflow run was not found.";

        var definition = await _db.NalvaWorkflowDefinitions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == detail.DefinitionId, ct);
        var executions = await _db.NalvaExecutions.AsNoTracking().Where(x => x.WorkflowRunId == runId).ToListAsync(ct);
        var completed = executions.Count(x => x.Status == NalvaStatuses.Completed);
        var failed = executions.Count(x => x.Status is NalvaStatuses.Failed or NalvaStatuses.DeadLetter);
        var awaiting = executions.Count(x => x.Status == NalvaStatuses.AwaitingApproval);
        var limitations = new List<string>();
        if (awaiting > 0)
            limitations.Add($"{awaiting} step(s) awaiting human approval.");
        if (failed > 0)
            limitations.Add($"{failed} step(s) failed.");
        if (detail.Status == NalvaStatuses.Completed && failed == 0 && awaiting == 0)
            limitations.Add("Report reflects verified backend step results only.");

        return string.Join(" ",
            $"Workflow '{definition?.Name ?? detail.Id.ToString()}' status={detail.Status}.",
            $"Steps completed={completed}, failed={failed}, awaitingApproval={awaiting}.",
            NalvaGuard.Clip(detail.Summary, 400),
            string.Join(" ", limitations));
    }

    private async Task EnsureCollaborationTemplatesAsync(CancellationToken ct)
    {
        var existing = await _db.NalvaWorkflowDefinitions.ToListAsync(ct);
        var changed = false;
        foreach (var template in NalvaWorkflowCatalog.All.Where(x => CollaborationKeys.Contains(x.Key)))
        {
            var stepsJson = JsonSerializer.Serialize(template.Steps.Select(step => new NalvaWorkflowStepWrite
            {
                Key = step.Key,
                AgentKey = step.AgentKey,
                Objective = step.Objective,
                Tools = step.Tools.ToList(),
                Condition = step.Condition,
                ParallelGroup = step.ParallelGroup,
                OutputKey = step.Key
            }), JsonOptions);

            var row = existing.FirstOrDefault(x => string.Equals(x.Key, template.Key, StringComparison.OrdinalIgnoreCase));
            if (row is null)
            {
                _db.NalvaWorkflowDefinitions.Add(new NalvaWorkflowDefinition
                {
                    Key = template.Key,
                    Name = template.Name,
                    Description = template.Description,
                    TriggerKind = template.TriggerKind,
                    TriggerEvent = template.TriggerEvent,
                    IntervalMinutes = template.IntervalMinutes,
                    TimeZoneId = "UTC",
                    StepsJson = stepsJson,
                    IsActive = false,
                    MaxToolCalls = NalvaLimits.MaxToolCalls,
                    Version = 1
                });
                changed = true;
                continue;
            }

            // Refresh inactive catalog collaboration workflows so tenants get Phase 5F steps.
            if (!row.IsActive && !string.Equals(row.StepsJson, stepsJson, StringComparison.Ordinal))
            {
                row.Name = template.Name;
                row.Description = template.Description;
                row.TriggerKind = template.TriggerKind;
                row.TriggerEvent = template.TriggerEvent;
                row.IntervalMinutes = template.IntervalMinutes;
                row.StepsJson = stepsJson;
                row.Version += 1;
                changed = true;
            }
        }

        if (changed)
            await _db.SaveChangesAsync(ct);
    }

    private void Require(string permission)
    {
        if (!_user.HasPermission(permission))
            throw new ForbiddenException("You do not have permission to perform this action.");
    }

    public static string Fingerprint(string? argsJson)
    {
        var payload = string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson.Trim();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }
}
