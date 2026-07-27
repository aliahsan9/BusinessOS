using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.Agents.Enums;
using BusinessOS.Application.Features.Agents.Services;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.AI.Services;
using BusinessOS.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.AI.Agents.Runtime;

public sealed class AgentRuntimeOrchestrator : IAgentRuntimeOrchestrator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IAgentIntentParser _intentParser;
    private readonly IAgentPlanner _planner;
    private readonly IAgentWorkflowService _workflows;
    private readonly IAgentToolExecutor _executor;
    private readonly IAiCopilotOrchestrator _copilot;
    private readonly IAiContextService _contextService;
    private readonly IAiMemoryService _memory;
    private readonly IAiPermissionValidator _permissions;
    private readonly IAiInsightService _insights;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AgentRuntimeOrchestrator> _logger;

    public AgentRuntimeOrchestrator(
        IAgentIntentParser intentParser,
        IAgentPlanner planner,
        IAgentWorkflowService workflows,
        IAgentToolExecutor executor,
        IAiCopilotOrchestrator copilot,
        IAiContextService contextService,
        IAiMemoryService memory,
        IAiPermissionValidator permissions,
        IAiInsightService insights,
        ICurrentUserService currentUser,
        ILogger<AgentRuntimeOrchestrator> logger)
    {
        _intentParser = intentParser;
        _planner = planner;
        _workflows = workflows;
        _executor = executor;
        _copilot = copilot;
        _contextService = contextService;
        _memory = memory;
        _permissions = permissions;
        _insights = insights;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async IAsyncEnumerable<AgentStreamChunkDto> RunStreamAsync(
        AgentChatRequest request,
        string agentKey,
        string language,
        AgentPersonaDto persona,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return Status("Thinking…");

        var chatRequest = new AiChatRequest(
            request.Message,
            request.CurrentPage,
            request.SearchQuery,
            request.CustomerId,
            request.OrderId,
            request.InvoiceId,
            request.ProjectId);

        var page = _contextService.BuildPageContext(chatRequest);
        var sessionId = await _memory.GetOrCreateSessionAsync(chatRequest, request.SessionId, cancellationToken);
        var memory = await _memory.LoadAsync(sessionId, cancellationToken);

        yield return Status("Planning…");

        var intent = _intentParser.Parse(request.Message, page, memory, language);
        var permission = _permissions.ValidateIntent(intent.Intent, intent.SuggestedTools);
        if (!permission.Allowed)
        {
            var denied = BuildResponse(
                persona,
                agentKey,
                language,
                intent.Intent,
                sessionId,
                permission.DenialReason ?? "Permission denied.",
                permissionDenied: true);
            yield return new AgentStreamChunkDto { Type = "final", Content = denied.Reply, FinalResponse = denied };
            yield break;
        }

        // Conversational / help / dashboard / document search → existing copilot (preserves /api/ai parity).
        if (!ShouldUseEmployeeRuntime(intent))
        {
            yield return Status("Consulting business data…");
            var copilotRequest = new AiCopilotChatRequest(
                request.Message,
                request.CurrentPage,
                request.SearchQuery,
                request.CustomerId,
                request.OrderId,
                request.InvoiceId,
                request.ProjectId,
                sessionId,
                request.Stream,
                agentKey,
                language,
                PreferEmployeeTone: true,
                request.WorkflowId);

            var copilotResponse = await _copilot.ProcessAsync(copilotRequest, cancellationToken);
            var mapped = MapCopilot(copilotResponse, persona, agentKey, language);
            foreach (var word in mapped.Reply.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                yield return new AgentStreamChunkDto { Type = "token", Content = word + " " };
                await Task.Delay(8, cancellationToken);
            }
            yield return new AgentStreamChunkDto { Type = "final", Content = mapped.Reply, FinalResponse = mapped };
            yield break;
        }

        var state = new AgentExecutionState
        {
            CustomerId = request.CustomerId ?? page.CustomerId ?? memory.SelectedCustomerId,
            CustomerName = memory.SelectedCustomerName,
            OrderId = request.OrderId ?? page.OrderId ?? memory.SelectedOrderId,
            InvoiceId = request.InvoiceId ?? page.InvoiceId ?? memory.SelectedInvoiceId
        };

        AgentWorkflowDto? workflow = null;
        var toolsUsed = new List<string>();
        var suggestions = new List<AiSuggestionDto>();
        AiActionResultDto? lastAction = null;
        var summaries = new List<string>();
        string? clarification = null;

        var useWorkflow = _planner.RequiresWorkflow(intent.Intent, request.Message, memory)
            || LooksLikeMultiStep(request.Message);

        IReadOnlyList<(string StepKey, string Title, AiToolName? Tool)> steps;
        if (useWorkflow)
        {
            var plan = _planner.Plan(agentKey, intent.Intent, request.Message, page, memory);
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("User context is required.");
            workflow = await _workflows.CreateFromPlanAsync(plan, userId, sessionId, cancellationToken);
            workflow = await _workflows.StartAsync(workflow.Id, cancellationToken);

            yield return new AgentStreamChunkDto
            {
                Type = "status",
                Content = "Executing workflow…",
                WorkflowId = workflow.Id
            };

            steps = plan.Steps
                .OrderBy(s => s.SortOrder)
                .Select(s => (s.StepKey, s.Title, s.ToolName))
                .ToList();
        }
        else
        {
            var primary = intent.SuggestedTools.FirstOrDefault();
            if (primary == default && intent.SuggestedTools.Count == 0)
            {
                // Fall back to copilot if we somehow got here without tools.
                var copilotResponse = await _copilot.ProcessAsync(
                    new AiCopilotChatRequest(
                        request.Message, request.CurrentPage, request.SearchQuery,
                        request.CustomerId, request.OrderId, request.InvoiceId, request.ProjectId,
                        sessionId, false, agentKey, language, true, null),
                    cancellationToken);
                var mapped = MapCopilot(copilotResponse, persona, agentKey, language);
                yield return new AgentStreamChunkDto { Type = "final", Content = mapped.Reply, FinalResponse = mapped };
                yield break;
            }

            steps =
            [
                ("execute", DescribeTool(primary), primary)
            ];
        }

        foreach (var (stepKey, title, toolName) in steps)
        {
            if (toolName is null)
            {
                if (workflow is not null)
                {
                    var skipped = await _workflows.SkipStepAsync(workflow.Id, stepKey, "Summary step", cancellationToken);
                    yield return new AgentStreamChunkDto
                    {
                        Type = "workflow_step",
                        Content = title,
                        WorkflowId = workflow.Id,
                        WorkflowStep = skipped
                    };
                }
                continue;
            }

            yield return Status(title);

            if (workflow is not null)
            {
                var began = await _workflows.BeginStepAsync(workflow.Id, stepKey, title, cancellationToken);
                yield return new AgentStreamChunkDto
                {
                    Type = "workflow_step",
                    Content = title,
                    WorkflowId = workflow.Id,
                    WorkflowStep = began
                };
            }

            yield return new AgentStreamChunkDto
            {
                Type = "tool",
                Content = $"Calling {toolName}…",
                ToolName = toolName.ToString(),
                WorkflowId = workflow?.Id
            };

            yield return Status("Validating…");
            yield return Status("Executing…");

            AgentToolExecutionResult? exec = null;
            Exception? stepError = null;
            try
            {
                exec = await _executor.ExecuteAsync(new AgentToolExecutionRequest
                {
                    ToolName = toolName.Value,
                    Message = request.Message,
                    Language = language,
                    Intent = intent.Intent,
                    Page = page,
                    Memory = memory,
                    SessionId = sessionId,
                    State = state,
                    WorkflowId = workflow?.Id,
                    StepKey = stepKey,
                    StepTitle = title
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                stepError = ex;
                _logger.LogError(ex, "Tool step {Step} failed hard", stepKey);
            }

            if (stepError is not null)
            {
                if (workflow is not null)
                    await _workflows.FailStepAsync(workflow.Id, stepKey, stepError.Message, cancellationToken);
                clarification = stepError.Message;
                break;
            }

            if (exec is null)
            {
                clarification = "Tool execution returned no result.";
                break;
            }

            state = exec.State;
            toolsUsed.Add(exec.ToolResult.ToolName);
            if (exec.ToolResult.ActionResult is not null)
                lastAction = exec.ToolResult.ActionResult;
            if (!string.IsNullOrWhiteSpace(exec.ToolResult.Summary))
                summaries.Add(exec.ToolResult.Summary);

            yield return new AgentStreamChunkDto
            {
                Type = "tool",
                Content = exec.ToolResult.Summary,
                ToolName = exec.ToolResult.ToolName,
                WorkflowId = workflow?.Id
            };

            if (exec.PermissionDenied || exec.Correction?.FailureKind == AgentFailureKind.PermissionDenied)
            {
                clarification = exec.DenialReason ?? exec.Correction?.ClarificationMessage;
                if (workflow is not null)
                    await _workflows.FailStepAsync(workflow.Id, stepKey, clarification ?? "Permission denied", cancellationToken);
                break;
            }

            if (!exec.ToolResult.Success)
            {
                if (exec.Correction?.Suggestions.Count > 0)
                    suggestions.AddRange(exec.Correction.Suggestions);

                clarification = exec.Correction?.ClarificationMessage
                    ?? exec.Correction?.SuggestedFixMessage
                    ?? exec.ToolResult.Summary;

                if (workflow is not null)
                    await _workflows.FailStepAsync(workflow.Id, stepKey, clarification, cancellationToken);

                // Stop multi-step on failure pending clarification.
                break;
            }

            yield return Status("Saving…");

            if (workflow is not null)
            {
                await _workflows.UpdateProgressJsonAsync(
                    workflow.Id,
                    JsonSerializer.Serialize(state, JsonOptions),
                    cancellationToken);

                var completed = await _workflows.CompleteStepAsync(
                    workflow.Id,
                    stepKey,
                    exec.ToolResult.Summary,
                    cancellationToken);

                yield return new AgentStreamChunkDto
                {
                    Type = "workflow_step",
                    Content = exec.ToolResult.Summary,
                    WorkflowId = workflow.Id,
                    WorkflowStep = completed
                };
            }
        }

        if (workflow is not null)
        {
            if (clarification is null)
                await _workflows.CompleteAsync(workflow.Id, string.Join(" | ", summaries.Take(3)), cancellationToken);
            else
                await _workflows.FailAsync(workflow.Id, clarification, cancellationToken);

            workflow = await _workflows.GetAsync(workflow.Id, cancellationToken);
        }

        yield return Status("Completed");

        var reply = BuildReply(summaries, clarification, language, lastAction);
        var spoken = BuildSpoken(reply, language, lastAction);

        try
        {
            var proactive = await _insights.GetProactiveInsightsAsync(cancellationToken);
            foreach (var insight in proactive.Where(i =>
                         i.Severity.Equals("high", StringComparison.OrdinalIgnoreCase)).Take(2))
            {
                suggestions.Add(new AiSuggestionDto
                {
                    Label = insight.Title,
                    Message = insight.Message
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Proactive insights skipped");
        }

        await _memory.UpdateAsync(sessionId, chatRequest, intent.Intent, request.Message, reply, cancellationToken);

        var response = new AgentChatResponse
        {
            Reply = reply,
            SpokenReply = spoken,
            SessionId = sessionId,
            AgentKey = agentKey,
            AgentDisplayName = persona.DisplayName,
            Intent = intent.Intent,
            WorkflowId = workflow?.Id,
            WorkflowSteps = workflow?.Steps ?? [],
            ToolsUsed = toolsUsed,
            Suggestions = suggestions,
            ActionResult = lastAction,
            PermissionDenied = clarification is not null && clarification.Contains("permission", StringComparison.OrdinalIgnoreCase)
        };

        foreach (var word in reply.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            yield return new AgentStreamChunkDto { Type = "token", Content = word + " " };
            await Task.Delay(8, cancellationToken);
        }

        yield return new AgentStreamChunkDto
        {
            Type = "final",
            Content = reply,
            FinalResponse = response,
            WorkflowId = workflow?.Id
        };
    }

    private static bool ShouldUseEmployeeRuntime(AiIntentDetectionResult intent)
    {
        if (intent.Intent is AiCopilotIntent.ActionCreate
            or AiCopilotIntent.ActionRead
            or AiCopilotIntent.ReportGeneration
            or AiCopilotIntent.Workflow
            or AiCopilotIntent.Recommendation)
        {
            return intent.SuggestedTools.Count > 0
                || intent.Intent is AiCopilotIntent.ReportGeneration or AiCopilotIntent.Workflow;
        }

        // Explicit employee write tools even if intent drifted.
        return intent.SuggestedTools.Any(IsEmployeeWriteTool);
    }

    private static bool IsEmployeeWriteTool(AiToolName tool) => tool is
        AiToolName.CreateCustomer or AiToolName.UpdateCustomer or AiToolName.DeleteCustomer
        or AiToolName.SearchCustomer or AiToolName.CreateProduct or AiToolName.UpdateProduct
        or AiToolName.DeleteProduct or AiToolName.SearchProduct or AiToolName.AdjustInventory
        or AiToolName.ReceiveStock or AiToolName.CreateSale or AiToolName.CreateInvoice
        or AiToolName.CancelInvoice or AiToolName.SearchInvoice or AiToolName.CreatePurchaseOrder
        or AiToolName.CreatePurchaseOrderDraft or AiToolName.ApprovePurchaseOrder
        or AiToolName.ReceivePurchase or AiToolName.CreateSupplier or AiToolName.UpdateSupplier
        or AiToolName.DeleteSupplier or AiToolName.SearchSupplier or AiToolName.ShowProfit
        or AiToolName.UpdateCompanyProfile or AiToolName.UpdateTaxDefaults
        or AiToolName.GenerateInventoryReport or AiToolName.GenerateSalesReport
        or AiToolName.GetInventorySummary or AiToolName.GetLowStock
        or AiToolName.GetPurchaseRecommendations;

    private static bool LooksLikeMultiStep(string message)
    {
        var text = message.ToLowerInvariant();
        return text.Contains(" then ")
            || text.Contains(" and then ")
            || text.Contains("after that")
            || (text.Contains("create customer") && text.Contains("invoice"))
            || (text.Contains("create customer") && text.Contains("sale"));
    }

    private static string DescribeTool(AiToolName tool) => tool switch
    {
        AiToolName.CreateCustomer => "Creating customer",
        AiToolName.UpdateCustomer => "Updating customer",
        AiToolName.DeleteCustomer => "Deleting customer",
        AiToolName.SearchCustomer => "Searching customers",
        AiToolName.CreateProduct => "Creating product",
        AiToolName.CreateSale => "Creating sale",
        AiToolName.CreateInvoice => "Creating invoice",
        AiToolName.AdjustInventory => "Adjusting inventory",
        AiToolName.ReceiveStock => "Receiving stock",
        AiToolName.CreatePurchaseOrder => "Creating purchase order",
        AiToolName.CreateSupplier => "Creating supplier",
        _ => $"Running {tool}"
    };

    private static string BuildReply(
        IReadOnlyList<string> summaries,
        string? clarification,
        string language,
        AiActionResultDto? action)
    {
        if (!string.IsNullOrWhiteSpace(clarification))
            return clarification;

        if (summaries.Count > 0)
            return string.Join("\n", summaries);

        if (action?.Success == true)
            return action.Message;

        return "Done.";
    }

    private static string BuildSpoken(string reply, string language, AiActionResultDto? action)
    {
        if (action is { Success: true } && !string.IsNullOrWhiteSpace(action.Message))
            return action.Message;
        // Prefer first line for TTS.
        var first = reply.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? reply;
        return first.Length > 280 ? first[..280] : first;
    }

    private static AgentChatResponse BuildResponse(
        AgentPersonaDto persona,
        string agentKey,
        string language,
        AiCopilotIntent intent,
        Guid sessionId,
        string reply,
        bool permissionDenied = false) =>
        new()
        {
            Reply = reply,
            SpokenReply = reply,
            SessionId = sessionId,
            AgentKey = agentKey,
            AgentDisplayName = persona.DisplayName,
            Intent = intent,
            PermissionDenied = permissionDenied
        };

    private static AgentChatResponse MapCopilot(
        AiCopilotChatResponse c,
        AgentPersonaDto persona,
        string agentKey,
        string language)
    {
        var reply = c.Reply ?? "";
        if (!string.IsNullOrWhiteSpace(persona.DisplayName))
        {
            reply = reply
                .Replace("BusinessOS AI Copilot", persona.DisplayName, StringComparison.OrdinalIgnoreCase)
                .Replace("BusinessOS AI", persona.DisplayName, StringComparison.OrdinalIgnoreCase);
        }

        return new AgentChatResponse
        {
            Reply = reply,
            SpokenReply = c.SpokenReply ?? reply,
            SessionId = c.SessionId,
            AgentKey = c.AgentKey ?? agentKey,
            AgentDisplayName = persona.DisplayName,
            Intent = c.Intent,
            WorkflowId = c.WorkflowId,
            WorkflowSteps = c.WorkflowSteps.Select(s => new AgentWorkflowStepDto
            {
                Id = s.Id,
                StepKey = s.StepKey,
                Title = s.Title,
                Status = Enum.TryParse<AgentWorkflowStepStatus>(s.Status, true, out var st)
                    ? st
                    : AgentWorkflowStepStatus.Completed,
                SortOrder = s.SortOrder,
                Message = s.Message,
                StartedAt = s.StartedAt,
                CompletedAt = s.CompletedAt
            }).ToList(),
            ToolsUsed = c.ToolsUsed,
            Citations = c.Citations,
            Suggestions = c.Suggestions,
            QuickActions = c.QuickActions,
            SearchResults = c.SearchResults,
            Sources = c.Sources,
            ActionResult = c.ActionResult,
            PermissionDenied = c.PermissionDenied
        };
    }

    private static AgentStreamChunkDto Status(string content) =>
        new() { Type = "status", Content = content };
}
