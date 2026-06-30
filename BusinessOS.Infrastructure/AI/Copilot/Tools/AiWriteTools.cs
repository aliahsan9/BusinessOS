using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.AI.Services;

namespace BusinessOS.Infrastructure.AI.Copilot.Tools;

public sealed class CreateCustomerTool : AiToolBase
{
    private readonly IAiActionService _actions;

    public CreateCustomerTool(IAiActionService actions) => _actions = actions;

    public override AiToolName ToolName => AiToolName.CreateCustomer;
    public override string Description => "Create a new customer from natural language.";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        intent is AiCopilotIntent.ActionCreate && ContainsAny(message, "customer");

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        var result = await _actions.TryExecuteAsync(context.Message, context.Page, cancellationToken);
        if (result is null)
            return new AiToolResult { ToolName = ToolName.ToString(), Success = false, Summary = "Could not parse customer creation request." };

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Success = result.Success,
            Summary = result.Message,
            ActionResult = result
        };
    }
}

public sealed class CreateProjectTool : AiToolBase
{
    private readonly IAiActionService _actions;

    public CreateProjectTool(IAiActionService actions) => _actions = actions;

    public override AiToolName ToolName => AiToolName.CreateProject;
    public override string Description => "Create a new project/order.";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        intent is AiCopilotIntent.ActionCreate && ContainsAny(message, "project", "order");

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        var result = await _actions.TryExecuteAsync(context.Message, context.Page, cancellationToken);
        if (result is null)
            return new AiToolResult { ToolName = ToolName.ToString(), Success = false, Summary = "Could not parse project creation request." };

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Success = result.Success,
            Summary = result.Message,
            ActionResult = result
        };
    }
}

public sealed class CreateTaskTool : AiToolBase
{
    private readonly IAiActionService _actions;

    public CreateTaskTool(IAiActionService actions) => _actions = actions;

    public override AiToolName ToolName => AiToolName.CreateTask;
    public override string Description => "Create a task for a project or team.";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        intent is AiCopilotIntent.ActionCreate && ContainsAny(message, "task");

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        var result = await _actions.TryExecuteAsync(context.Message, context.Page, cancellationToken);
        if (result is null)
            return new AiToolResult { ToolName = ToolName.ToString(), Success = false, Summary = "Could not parse task creation request." };

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Success = result.Success,
            Summary = result.Message,
            ActionResult = result
        };
    }
}

public sealed class CreateInvoiceTool : AiToolBase
{
    private readonly IAiActionService _actions;

    public CreateInvoiceTool(IAiActionService actions) => _actions = actions;

    public override AiToolName ToolName => AiToolName.CreateInvoice;
    public override string Description => "Create an invoice for a customer or order.";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        intent is AiCopilotIntent.ActionCreate && ContainsAny(message, "invoice");

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        var result = await _actions.TryExecuteAsync(context.Message, context.Page, cancellationToken);
        if (result is null)
            return new AiToolResult { ToolName = ToolName.ToString(), Success = false, Summary = "Could not parse invoice creation request." };

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Success = result.Success,
            Summary = result.Message,
            ActionResult = result
        };
    }
}

public sealed class SearchDocumentsTool : AiToolBase
{
    private readonly IAiVectorRagService _rag;

    public SearchDocumentsTool(IAiVectorRagService rag) => _rag = rag;

    public override AiToolName ToolName => AiToolName.SearchDocuments;
    public override string Description => "Search business documentation, policies, contracts, and uploaded files.";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        intent is AiCopilotIntent.DocumentSearch;

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        var citations = await _rag.SearchAsync(context.Message, documentType: null, top: 5, cancellationToken);
        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Citations = citations,
            Data = citations,
            Summary = citations.Count == 0
                ? "No matching documents found in your knowledge base."
                : $"Found {citations.Count} relevant document(s)."
        };
    }
}
