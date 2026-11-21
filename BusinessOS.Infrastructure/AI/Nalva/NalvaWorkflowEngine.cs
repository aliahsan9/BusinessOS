using System.Text.Json;
using System.Text.Json.Nodes;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Domain.Entities;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Infrastructure.Billing;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessOS.Infrastructure.AI.Nalva;

public sealed class NalvaWorkflowEngine : INalvaWorkflowEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly BusinessOSDbContext _db;
    private readonly INalvaExecutionEngine _engine;
    private readonly ITenantProvider? _tenant;
    private readonly IServiceScopeFactory? _scopes;

    public NalvaWorkflowEngine(
        BusinessOSDbContext db,
        INalvaExecutionEngine engine,
        ITenantProvider? tenant = null,
        IServiceScopeFactory? scopes = null)
    {
        _db = db;
        _engine = engine;
        _tenant = tenant;
        _scopes = scopes;
    }

    public Task<NalvaWorkflowRunDto> RunAsync(Guid definitionId, string? argumentsJson, CancellationToken cancellationToken = default) =>
        RunAsync(definitionId, argumentsJson, new NalvaWorkflowRunOptions(), cancellationToken);

    public async Task<NalvaWorkflowRunDto> RunAsync(
        Guid definitionId,
        string? argumentsJson,
        NalvaWorkflowRunOptions options,
        CancellationToken cancellationToken = default)
    {
        if (NalvaExecutionAmbient.Depth >= NalvaLimits.MaxEventDepth)
            throw new BadRequestException("Workflow depth limit reached.");

        var definition = await _db.NalvaWorkflowDefinitions.FirstOrDefaultAsync(x => x.Id == definitionId, cancellationToken)
            ?? throw new NotFoundException("Workflow was not found.");
        if (options.RequireActive && !definition.IsActive)
            throw new BadRequestException("Activate the workflow before running it.");
        if (_tenant?.HasTenant() == true)
        {
            var access = await CommercialGate.ResolveAsync(_db, _tenant.TenantId, cancellationToken);
            if (access is not null)
            {
                if (!access.IsEnabled(FeatureFlags.Automations))
                    throw new ForbiddenException("Automations are not included in the current subscription.");
                var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var used = await _db.NalvaWorkflowRuns.CountAsync(x => x.StartedAt >= monthStart, cancellationToken);
                await CommercialGate.EnsureQuotaAsync(_db, _tenant.TenantId, "Automation runs", used, access.AutomationRunLimit, cancellationToken);
            }
        }
        if (!string.IsNullOrWhiteSpace(options.CorrelationId))
        {
            var duplicate = await _db.NalvaWorkflowRuns.AnyAsync(
                x => x.DefinitionId == definition.Id && x.CorrelationId == options.CorrelationId && x.Status != NalvaStatuses.Failed,
                cancellationToken);
            if (duplicate)
                throw new ConflictException("This workflow window was already started.", "WORKFLOW_DUPLICATE");
        }

        using var ambient = NalvaExecutionAmbient.Push();
        var steps = JsonSerializer.Deserialize<List<NalvaWorkflowStepWrite>>(definition.StepsJson, JsonOptions) ?? [];
        if (steps.Count == 0 || steps.Count > 25)
            throw new BadRequestException("The workflow needs between 1 and 25 steps.");
        if (steps.Any(step => step.Tools.Count > definition.MaxToolCalls))
            throw new BadRequestException("A workflow step exceeds its tool-call limit.");

        var context = ParseContext(argumentsJson);
        var run = new NalvaWorkflowRun
        {
            DefinitionId = definition.Id,
            DefinitionVersion = definition.Version,
            Status = NalvaStatuses.Running,
            ContextJson = context.ToJsonString(),
            TriggerSource = options.TriggerSource,
            IsDryRun = options.DryRun,
            StepsSnapshotJson = definition.StepsJson,
            CorrelationId = options.CorrelationId,
            StartedAt = DateTime.UtcNow
        };
        _db.NalvaWorkflowRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);
        await ExecuteFromAsync(run, definition, steps, 0, context, options, cancellationToken);
        return Map(run, definition.Name);
    }

    public async Task ContinueAfterAsync(Guid workflowRunId, string executionStatus, CancellationToken cancellationToken = default)
    {
        var run = await _db.NalvaWorkflowRuns.FirstOrDefaultAsync(x => x.Id == workflowRunId, cancellationToken);
        if (run is null || run.Status != NalvaStatuses.AwaitingApproval)
            return;
        if (executionStatus != NalvaStatuses.Completed)
        {
            if (executionStatus is NalvaStatuses.Failed or NalvaStatuses.Cancelled)
            {
                run.Status = executionStatus;
                run.CompletedAt = DateTime.UtcNow;
                run.Error ??= "The approval did not complete the step.";
                await _db.SaveChangesAsync(cancellationToken);
            }
            return;
        }

        var definition = await _db.NalvaWorkflowDefinitions.FirstOrDefaultAsync(x => x.Id == run.DefinitionId, cancellationToken);
        if (definition is null)
            return;
        var steps = JsonSerializer.Deserialize<List<NalvaWorkflowStepWrite>>(
            string.IsNullOrWhiteSpace(run.StepsSnapshotJson) ? definition.StepsJson : run.StepsSnapshotJson, JsonOptions) ?? [];
        var context = ParseContext(run.ContextJson);
        run.Status = NalvaStatuses.Running;
        await ExecuteFromAsync(run, definition, steps, run.CurrentStep + 1, context, new NalvaWorkflowRunOptions { DryRun = run.IsDryRun, TriggerSource = run.TriggerSource }, cancellationToken);
    }

    private async Task ExecuteFromAsync(
        NalvaWorkflowRun run,
        NalvaWorkflowDefinition definition,
        List<NalvaWorkflowStepWrite> steps,
        int start,
        JsonObject context,
        NalvaWorkflowRunOptions options,
        CancellationToken cancellationToken)
    {
        var log = JsonNode.Parse(string.IsNullOrWhiteSpace(run.StepLogJson) ? "[]" : run.StepLogJson) as JsonArray ?? [];
        var summaries = new List<string>();
        if (!string.IsNullOrWhiteSpace(run.Summary))
            summaries.Add(run.Summary);
        var groups = NalvaWorkflowGraph.Group(steps);
        var cursor = 0;
        foreach (var group in groups)
        {
            var groupStart = cursor;
            cursor += group.Count;
            if (groupStart + group.Count - 1 < start)
                continue;

            var pending = group.Skip(Math.Max(0, start - groupStart)).ToList();
            if (pending.Count == 0)
                continue;

            IReadOnlyList<(NalvaWorkflowStepWrite Step, NalvaExecutionResult Result, string? Skip)> outcomes;
            if (pending.Count > 1 && _scopes is not null && _tenant is { } tenant && tenant.HasTenant())
                outcomes = await RunParallelAsync(pending, run, context, options, tenant.TenantId, cancellationToken);
            else
                outcomes = await RunSequentialAsync(pending, run, context, options, cancellationToken);

            var stop = false;
            foreach (var (step, result, skip) in outcomes)
            {
                run.CurrentStep = steps.FindIndex(item => item.Key == step.Key);
                if (skip is not null)
                {
                    log.Add(new JsonObject { ["step"] = step.Key, ["status"] = NalvaStatuses.Skipped, ["reason"] = skip });
                    summaries.Add($"{step.Key}: skipped");
                    continue;
                }

                summaries.Add($"{step.Key}: {result.Summary}");
                log.Add(new JsonObject { ["step"] = step.Key, ["status"] = result.Status, ["executionId"] = result.ExecutionId.ToString(), ["summary"] = result.Summary });
                run.EstimatedCostUsd += result.EstimatedCostUsd;

                // Pass only validated / trust-bounded peer results to downstream agents.
                JsonNode stepContext;
                if (NalvaCollaborationProtocol.TryParseResult(result.ResultJson, out var envelope, out _) && envelope is not null)
                    stepContext = NalvaCollaborationProtocol.TrustBoundary(envelope);
                else
                {
                    var fallback = NalvaCollaborationProtocol.FromExecution(
                        result.ExecutionId,
                        run.Id,
                        result,
                        JsonNode.Parse(result.ResultJson ?? "{}") as JsonObject);
                    stepContext = NalvaCollaborationProtocol.TrustBoundary(fallback);
                }
                context[step.OutputKey ?? step.Key] = stepContext;
                context["lastAgentResult"] = stepContext.DeepClone();

                if (result.Status == NalvaStatuses.AwaitingApproval)
                {
                    run.Status = NalvaStatuses.AwaitingApproval;
                    stop = true;
                    break;
                }
                if (result.Status != NalvaStatuses.Completed)
                {
                    run.Status = result.Status is NalvaStatuses.Cancelled or NalvaStatuses.Paused
                        ? NalvaStatuses.Paused
                        : NalvaStatuses.Failed;
                    run.Error = result.Error ?? result.Summary;
                    stop = true;
                    break;
                }
            }

            run.ContextJson = context.ToJsonString();
            run.StepLogJson = log.ToJsonString();
            run.Summary = string.Join(" ", summaries).Trim();
            if (stop)
                break;
        }

        if (run.Status == NalvaStatuses.Running)
        {
            run.Status = NalvaStatuses.Completed;
            run.CompletedAt = DateTime.UtcNow;
        }
        run.Summary = string.Join(" ", summaries).Trim();
        run.StepLogJson = log.ToJsonString();
        run.ContextJson = context.ToJsonString();
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<(NalvaWorkflowStepWrite Step, NalvaExecutionResult Result, string? Skip)>> RunSequentialAsync(
        IReadOnlyList<NalvaWorkflowStepWrite> steps,
        NalvaWorkflowRun run,
        JsonObject context,
        NalvaWorkflowRunOptions options,
        CancellationToken cancellationToken)
    {
        var results = new List<(NalvaWorkflowStepWrite, NalvaExecutionResult, string?)>();
        foreach (var step in steps)
        {
            if (!Allow(step, context, out var skip))
            {
                results.Add((step, Empty(step), skip));
                continue;
            }
            results.Add((step, await ExecuteStepAsync(_engine, step, run, context, options, cancellationToken), null));
            if (results[^1].Item2.Status != NalvaStatuses.Completed)
                break;
        }
        return results;
    }

    private async Task<IReadOnlyList<(NalvaWorkflowStepWrite Step, NalvaExecutionResult Result, string? Skip)>> RunParallelAsync(
        IReadOnlyList<NalvaWorkflowStepWrite> steps,
        NalvaWorkflowRun run,
        JsonObject context,
        NalvaWorkflowRunOptions options,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var snapshot = context.ToJsonString();
        var tasks = steps.Select(async step =>
        {
            if (!Allow(step, context, out var skip))
                return (step, Empty(step), skip);
            using var scope = _scopes!.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantId(tenantId);
            var engine = scope.ServiceProvider.GetRequiredService<INalvaExecutionEngine>();
            var local = ParseContext(snapshot);
            return (step, await ExecuteStepAsync(engine, step, run, local, options, cancellationToken), (string?)null);
        });
        return await Task.WhenAll(tasks);
    }

    private static async Task<NalvaExecutionResult> ExecuteStepAsync(
        INalvaExecutionEngine engine,
        NalvaWorkflowStepWrite step,
        NalvaWorkflowRun run,
        JsonObject context,
        NalvaWorkflowRunOptions options,
        CancellationToken cancellationToken)
    {
        return await engine.ExecuteAsync(new NalvaExecuteRequest
        {
            AgentKey = step.AgentKey,
            Objective = step.Objective,
            ArgumentsJson = context.ToJsonString(),
            Tools = step.Tools,
            WorkflowRunId = run.Id,
            DryRun = options.DryRun || run.IsDryRun,
            CorrelationId = run.CorrelationId
        }, cancellationToken);
    }

    private static bool Allow(NalvaWorkflowStepWrite step, JsonObject context, out string? skip)
    {
        skip = null;
        if (!NalvaConditionEvaluator.TryEvaluate(step.Condition, context, out var allowed, out var error))
            throw new BadRequestException(error ?? "The condition is invalid.");
        if (allowed)
            return true;
        skip = "Condition was false.";
        return false;
    }

    private static NalvaExecutionResult Empty(NalvaWorkflowStepWrite step) =>
        new()
        {
            Status = NalvaStatuses.Completed,
            AgentKey = step.AgentKey,
            Summary = "Skipped.",
            ResultJson = "{}"
        };

    private static JsonObject ParseContext(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new JsonObject();
        try
        {
            return JsonNode.Parse(json) as JsonObject ?? new JsonObject { ["input"] = json };
        }
        catch (JsonException)
        {
            return new JsonObject { ["input"] = NalvaGuard.Clip(json, 500) };
        }
    }

    private static NalvaWorkflowRunDto Map(NalvaWorkflowRun run, string name) =>
        new(run.Id, run.DefinitionId, name, run.Status, run.CurrentStep, run.Summary, run.Error, run.StartedAt, run.CompletedAt, run.TriggerSource, run.IsDryRun, run.EstimatedCostUsd);
}
