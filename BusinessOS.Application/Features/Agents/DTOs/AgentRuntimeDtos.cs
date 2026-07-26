using System.Text.Json;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;

namespace BusinessOS.Application.Features.Agents.DTOs;

/// <summary>
/// Mutable bag of entity IDs and values passed between workflow steps.
/// Serialized into <c>AgentWorkflowRun.ProgressJson</c>.
/// </summary>
public sealed class AgentExecutionState
{
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? ProductId { get; set; }
    public string? ProductName { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? InvoiceId { get; set; }
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public Guid? CategoryId { get; set; }
    public Dictionary<string, string?> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public void Set(string key, string? value) => Values[key] = value;

    public string? Get(string key) =>
        Values.TryGetValue(key, out var v) ? v : null;
}

public enum AgentFailureKind
{
    None = 0,
    ValidationMissingFields = 1,
    DuplicateEntity = 2,
    NotFound = 3,
    PermissionDenied = 4,
    BusinessRule = 5,
    Transient = 6,
    Unknown = 7
}

public sealed class AgentSelfCorrectionResult
{
    public bool ShouldRetry { get; init; }
    public bool NeedsClarification { get; init; }
    public AgentFailureKind FailureKind { get; init; }
    public string? ClarificationMessage { get; init; }
    public string? SuggestedFixMessage { get; init; }
    public AiToolName? AlternateTool { get; init; }
    public JsonElement? RevisedArgs { get; init; }
    public IReadOnlyList<string> MissingFields { get; init; } = [];
    public IReadOnlyList<AiSuggestionDto> Suggestions { get; init; } = [];
}

public sealed class AgentToolExecutionRequest
{
    public AiToolName ToolName { get; init; }
    public string Message { get; init; } = default!;
    public string Language { get; init; } = "en";
    public AiCopilotIntent Intent { get; init; }
    public AiPageContextDto Page { get; init; } = new();
    public AiMemoryStateDto Memory { get; init; } = new();
    public Guid SessionId { get; init; }
    public AgentExecutionState State { get; init; } = new();
    public JsonElement? PreextractedArgs { get; init; }
    public string? StepKey { get; init; }
    public string? StepTitle { get; init; }
    public Guid? WorkflowId { get; init; }
}

public sealed class AgentToolExecutionResult
{
    public AiToolResult ToolResult { get; init; } = new() { ToolName = "Unknown", Summary = "" };
    public AgentExecutionState State { get; init; } = new();
    public AgentSelfCorrectionResult? Correction { get; init; }
    public bool PermissionDenied { get; init; }
    public string? DenialReason { get; init; }
    public long DurationMs { get; init; }
    public bool Retried { get; init; }
}

public sealed class AgentActionLogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public AiCopilotIntent Intent { get; init; }
    public string? ToolName { get; init; }
    public long ExecutionTimeMs { get; init; }
    public bool Success { get; init; }
    public string? FailureReason { get; init; }
    public Guid? SessionId { get; init; }
    public Guid? WorkflowId { get; init; }
    public string? StepKey { get; init; }
}
