using System.Diagnostics;
using System.Text.Json;
using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.Agents.Services;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.AI.Services;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.AI.Agents.Runtime;

public sealed class AgentToolExecutor : IAgentToolExecutor
{
    private readonly IAiToolRegistry _registry;
    private readonly IAiPermissionValidator _permissions;
    private readonly IAgentArgumentExtractor _args;
    private readonly IAgentSelfCorrector _corrector;
    private readonly IAgentActionLogger _logger;
    private readonly ILogger<AgentToolExecutor> _log;

    public AgentToolExecutor(
        IAiToolRegistry registry,
        IAiPermissionValidator permissions,
        IAgentArgumentExtractor args,
        IAgentSelfCorrector corrector,
        IAgentActionLogger logger,
        ILogger<AgentToolExecutor> log)
    {
        _registry = registry;
        _permissions = permissions;
        _args = args;
        _corrector = corrector;
        _logger = logger;
        _log = log;
    }

    public async Task<AgentToolExecutionResult> ExecuteAsync(
        AgentToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var permission = _permissions.ValidateTool(request.ToolName);
        if (!permission.Allowed)
        {
            sw.Stop();
            await _logger.LogAsync(new AgentActionLogEntry
            {
                Intent = request.Intent,
                ToolName = request.ToolName.ToString(),
                ExecutionTimeMs = sw.ElapsedMilliseconds,
                Success = false,
                FailureReason = permission.DenialReason,
                SessionId = request.SessionId,
                WorkflowId = request.WorkflowId,
                StepKey = request.StepKey
            }, cancellationToken);

            return new AgentToolExecutionResult
            {
                PermissionDenied = true,
                DenialReason = permission.DenialReason,
                DurationMs = sw.ElapsedMilliseconds,
                State = request.State,
                ToolResult = new AiToolResult
                {
                    ToolName = request.ToolName.ToString(),
                    Success = false,
                    Summary = permission.DenialReason ?? "Permission denied."
                },
                Correction = new AgentSelfCorrectionResult
                {
                    FailureKind = AgentFailureKind.PermissionDenied,
                    NeedsClarification = true,
                    ClarificationMessage = permission.DenialReason
                }
            };
        }

        var tool = _registry.AllTools.FirstOrDefault(t => t.ToolName == request.ToolName);
        if (tool is null)
        {
            sw.Stop();
            return new AgentToolExecutionResult
            {
                DurationMs = sw.ElapsedMilliseconds,
                State = request.State,
                ToolResult = new AiToolResult
                {
                    ToolName = request.ToolName.ToString(),
                    Success = false,
                    Summary = $"Tool {request.ToolName} is not registered."
                }
            };
        }

        var schema = tool.ParameterSchemaJson ?? AgentToolSchemas.For(request.ToolName) ?? "{}";
        JsonElement args;
        try
        {
            args = request.PreextractedArgs
                ?? await _args.ExtractAsync(
                    request.ToolName,
                    schema,
                    request.Message,
                    request.State,
                    request.Page,
                    request.Language,
                    cancellationToken);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Argument extraction failed for {Tool}", request.ToolName);
            args = JsonDocument.Parse("{}").RootElement.Clone();
        }

        var execContext = new AiCopilotExecutionContext
        {
            Request = new AiChatRequest(
                request.Message,
                request.Page.Module,
                null,
                request.Page.CustomerId,
                request.Page.OrderId,
                request.Page.InvoiceId,
                request.Page.ProjectId),
            Page = request.Page,
            Intent = request.Intent,
            SessionId = request.SessionId,
            Memory = request.Memory,
            Message = request.Message,
            Language = request.Language,
            ToolArgs = args,
            ExecutionState = request.State
        };

        var (result, exception) = await RunToolAsync(tool, execContext, args, cancellationToken);
        ApplyStateFromResult(request.State, result);

        var correction = _corrector.Analyze(request.ToolName, result, exception, args, request.Language);
        var retried = false;

        if (!result.Success && correction.ShouldRetry && correction.AlternateTool is AiToolName searchTool
            && correction.FailureKind == AgentFailureKind.NotFound)
        {
            // Search then retry original once.
            var searchReq = new AgentToolExecutionRequest
            {
                ToolName = searchTool,
                Message = request.Message,
                Language = request.Language,
                Intent = AiCopilotIntent.ActionRead,
                Page = request.Page,
                Memory = request.Memory,
                SessionId = request.SessionId,
                State = request.State,
                WorkflowId = request.WorkflowId,
                StepKey = request.StepKey
            };
            var searchResult = await ExecuteOnceAsync(searchReq, cancellationToken);
            ApplyStateFromResult(request.State, searchResult.ToolResult);

            if (searchResult.ToolResult.Success)
            {
                retried = true;
                execContext.ExecutionState = request.State;
                var retryArgs = await _args.ExtractAsync(
                    request.ToolName,
                    schema,
                    request.Message,
                    request.State,
                    request.Page,
                    request.Language,
                    cancellationToken);
                execContext.ToolArgs = retryArgs;
                (result, exception) = await RunToolAsync(tool, execContext, retryArgs, cancellationToken);
                ApplyStateFromResult(request.State, result);
                correction = _corrector.Analyze(request.ToolName, result, exception, retryArgs, request.Language);
            }
        }
        else if (!result.Success && correction is { ShouldRetry: true, FailureKind: AgentFailureKind.Transient })
        {
            retried = true;
            (result, exception) = await RunToolAsync(tool, execContext, args, cancellationToken);
            ApplyStateFromResult(request.State, result);
            correction = _corrector.Analyze(request.ToolName, result, exception, args, request.Language);
        }

        sw.Stop();
        await _logger.LogAsync(new AgentActionLogEntry
        {
            Intent = request.Intent,
            ToolName = request.ToolName.ToString(),
            ExecutionTimeMs = sw.ElapsedMilliseconds,
            Success = result.Success,
            FailureReason = result.Success ? null : result.Summary,
            SessionId = request.SessionId,
            WorkflowId = request.WorkflowId,
            StepKey = request.StepKey
        }, cancellationToken);

        return new AgentToolExecutionResult
        {
            ToolResult = result,
            State = request.State,
            Correction = result.Success ? null : correction,
            DurationMs = sw.ElapsedMilliseconds,
            Retried = retried
        };
    }

    private async Task<AgentToolExecutionResult> ExecuteOnceAsync(
        AgentToolExecutionRequest request,
        CancellationToken cancellationToken)
    {
        // Simplified path without nested self-correction to avoid infinite loops.
        var tool = _registry.AllTools.FirstOrDefault(t => t.ToolName == request.ToolName);
        if (tool is null)
        {
            return new AgentToolExecutionResult
            {
                State = request.State,
                ToolResult = new AiToolResult
                {
                    ToolName = request.ToolName.ToString(),
                    Success = false,
                    Summary = "Tool not found."
                }
            };
        }

        var schema = tool.ParameterSchemaJson ?? AgentToolSchemas.For(request.ToolName) ?? "{}";
        var args = await _args.ExtractAsync(
            request.ToolName, schema, request.Message, request.State, request.Page, request.Language, cancellationToken);

        var execContext = new AiCopilotExecutionContext
        {
            Request = new AiChatRequest(request.Message, request.Page.Module, null,
                request.Page.CustomerId, request.Page.OrderId, request.Page.InvoiceId, request.Page.ProjectId),
            Page = request.Page,
            Intent = request.Intent,
            SessionId = request.SessionId,
            Memory = request.Memory,
            Message = request.Message,
            Language = request.Language,
            ToolArgs = args,
            ExecutionState = request.State
        };

        var (result, _) = await RunToolAsync(tool, execContext, args, cancellationToken);
        return new AgentToolExecutionResult { ToolResult = result, State = request.State };
    }

    private static async Task<(AiToolResult Result, Exception? Error)> RunToolAsync(
        IAiTool tool,
        AiCopilotExecutionContext context,
        JsonElement args,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = string.IsNullOrWhiteSpace(tool.ParameterSchemaJson) && AgentToolSchemas.For(tool.ToolName) is null
                ? await tool.ExecuteAsync(context, cancellationToken)
                : await tool.ExecuteWithArgsAsync(context, args, cancellationToken);
            return (result, null);
        }
        catch (Exception ex)
        {
            return (new AiToolResult
            {
                ToolName = tool.ToolName.ToString(),
                Success = false,
                Summary = ex.Message
            }, ex);
        }
    }

    private static void ApplyStateFromResult(AgentExecutionState state, AiToolResult result)
    {
        if (result.ActionResult is null)
            return;

        var entityType = result.ActionResult.EntityType ?? "";
        var id = result.ActionResult.EntityId;
        if (id is null)
            return;

        switch (entityType.ToLowerInvariant())
        {
            case "customer":
                state.CustomerId = id;
                state.CustomerName = ExtractName(result.Summary);
                break;
            case "product":
                state.ProductId = id;
                state.ProductName = ExtractName(result.Summary);
                break;
            case "order":
            case "sale":
                state.OrderId = id;
                break;
            case "invoice":
                state.InvoiceId = id;
                break;
            case "supplier":
                state.SupplierId = id;
                state.SupplierName = ExtractName(result.Summary);
                break;
            case "purchaseorder":
            case "purchase_order":
                state.PurchaseOrderId = id;
                break;
        }
    }

    private static string? ExtractName(string summary)
    {
        var start = summary.IndexOf('"');
        var end = summary.LastIndexOf('"');
        if (start >= 0 && end > start)
            return summary[(start + 1)..end];
        return null;
    }
}
