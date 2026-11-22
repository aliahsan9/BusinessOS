using System.Text.Json;
using System.Text.Json.Nodes;

namespace BusinessOS.Application.Features.Nalva;

/// <summary>Structured agent-to-agent communication contract (Phase 5F).</summary>
public static class NalvaAgentResultStatus
{
    public const string Completed = "completed";
    public const string PartiallyCompleted = "partially_completed";
    public const string Failed = "failed";
    public const string Blocked = "blocked";
    public const string AwaitingApproval = "awaiting_approval";
}

public sealed class NalvaAgentTaskEnvelope
{
    public Guid TaskId { get; init; }
    public Guid? WorkflowId { get; init; }
    public Guid? WorkflowRunId { get; init; }
    public Guid TenantId { get; init; }
    public string? RequestingUserId { get; init; }
    public IReadOnlyList<string> AuthorizationPermissions { get; init; } = [];
    public string AssignedAgent { get; init; } = "";
    public string Objective { get; init; } = "";
    public JsonObject InputData { get; init; } = new();
    public string ExpectedOutputSchema { get; init; } = "NalvaAgentResultEnvelope";
    public IReadOnlyList<string> Dependencies { get; init; } = [];
    public IReadOnlyList<string> Permissions { get; init; } = [];
    public IReadOnlyList<string> ApprovalRequirements { get; init; } = [];
    public DateTime? DeadlineUtc { get; init; }
    public int TimeoutSeconds { get; init; } = NalvaLimits.MaxExecutionSeconds;
    public NalvaRetryPolicy RetryPolicy { get; init; } = new();
    public string ExecutionStatus { get; init; } = NalvaStatuses.Queued;
    public string CorrelationId { get; init; } = "";
    public string IdempotencyKey { get; init; } = "";
}

public sealed class NalvaRetryPolicy
{
    public int MaxAttempts { get; init; } = NalvaLimits.MaxRetries;
    public bool IdempotentOnly { get; init; } = true;
    public bool EscalateAfterExhaustion { get; init; } = true;
}

public sealed class NalvaAgentResultEnvelope
{
    public Guid TaskId { get; init; }
    public Guid? WorkflowRunId { get; init; }
    public Guid ExecutionId { get; init; }
    public string AgentKey { get; init; } = "";
    public string Status { get; init; } = NalvaAgentResultStatus.Failed;
    public bool Verified { get; init; }
    public JsonObject Results { get; init; } = new();
    public IReadOnlyList<NalvaEvidenceRef> Evidence { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> Limitations { get; init; } = [];
    public IReadOnlyList<NalvaFollowUpTask> FollowUpTasks { get; init; } = [];
    public NalvaUsageMetadata Usage { get; init; } = new();
    public string Summary { get; init; } = "";
    public DateTime CompletedAtUtc { get; init; } = DateTime.UtcNow;
}

public sealed class NalvaEvidenceRef
{
    public string Kind { get; init; } = "";
    public string? EntityType { get; init; }
    public Guid? EntityId { get; init; }
    public string? Reference { get; init; }
    public bool Verified { get; init; }
}

public sealed class NalvaFollowUpTask
{
    public string AgentKey { get; init; } = "";
    public string Objective { get; init; } = "";
    public IReadOnlyList<string> Tools { get; init; } = [];
}

public sealed class NalvaUsageMetadata
{
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public decimal EstimatedCostUsd { get; init; }
    public bool CostIsEstimate { get; init; } = true;
    public int ToolCallCount { get; init; }
    public int DurationMs { get; init; }
}

public static class NalvaCollaborationProtocol
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static readonly HashSet<string> ValidResultStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        NalvaAgentResultStatus.Completed,
        NalvaAgentResultStatus.PartiallyCompleted,
        NalvaAgentResultStatus.Failed,
        NalvaAgentResultStatus.Blocked,
        NalvaAgentResultStatus.AwaitingApproval
    };

    public static bool TryValidateTask(NalvaAgentTaskEnvelope task, out string error)
    {
        error = "";
        if (task.TaskId == Guid.Empty)
        {
            error = "TaskId is required.";
            return false;
        }
        if (task.TenantId == Guid.Empty)
        {
            error = "TenantId is required.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(task.AssignedAgent) || !NalvaAgentCatalog.TryGet(task.AssignedAgent, out var agent) || agent is null)
        {
            error = "Assigned agent is unknown or inactive in the catalog.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(task.Objective))
        {
            error = "Objective is required.";
            return false;
        }
        if (task.TimeoutSeconds is < 1 or > 600)
        {
            error = "Timeout must be between 1 and 600 seconds.";
            return false;
        }
        if (task.RetryPolicy.MaxAttempts is < 1 or > 10)
        {
            error = "Retry policy max attempts must be between 1 and 10.";
            return false;
        }
        foreach (var permission in task.Permissions)
        {
            if (!agent.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase)
                && !agent.Tools.Contains(permission, StringComparer.OrdinalIgnoreCase))
            {
                // Permissions may be tool names or permission codes granted to the agent.
                if (!agent.Permissions.Any(p => string.Equals(p, permission, StringComparison.OrdinalIgnoreCase)))
                {
                    // Allow empty permission lists; reject only unknown tool grants when Permissions looks like tools.
                    if (NalvaToolCatalog.TryGet(permission, out _) && !agent.Tools.Contains(permission, StringComparer.OrdinalIgnoreCase))
                    {
                        error = $"Agent '{agent.Key}' is not granted tool '{permission}'.";
                        return false;
                    }
                }
            }
        }
        return true;
    }

    public static bool TryValidateResult(NalvaAgentResultEnvelope result, out string error)
    {
        error = "";
        if (result.TaskId == Guid.Empty)
        {
            error = "Result TaskId is required.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(result.AgentKey))
        {
            error = "Result AgentKey is required.";
            return false;
        }
        if (!ValidResultStatuses.Contains(result.Status))
        {
            error = "Result status is not recognized.";
            return false;
        }
        if (result.Status == NalvaAgentResultStatus.Completed && !result.Verified)
        {
            error = "Completed results must be marked verified against backend records.";
            return false;
        }
        if (result.Status == NalvaAgentResultStatus.Completed && result.Errors.Count > 0)
        {
            error = "Completed results cannot include unresolved errors.";
            return false;
        }
        return true;
    }

    public static NalvaAgentResultEnvelope FromExecution(
        Guid taskId,
        Guid? workflowRunId,
        NalvaExecutionResult execution,
        JsonObject? results = null,
        IEnumerable<NalvaEvidenceRef>? evidence = null,
        IEnumerable<string>? limitations = null)
    {
        var status = execution.Status switch
        {
            NalvaStatuses.Completed => NalvaAgentResultStatus.Completed,
            NalvaStatuses.AwaitingApproval => NalvaAgentResultStatus.AwaitingApproval,
            NalvaStatuses.Cancelled => NalvaAgentResultStatus.Blocked,
            NalvaStatuses.Paused => NalvaAgentResultStatus.Blocked,
            NalvaStatuses.Failed or NalvaStatuses.DeadLetter => NalvaAgentResultStatus.Failed,
            _ => NalvaAgentResultStatus.PartiallyCompleted
        };

        var verified = status == NalvaAgentResultStatus.Completed
            && string.IsNullOrWhiteSpace(execution.Error)
            && !string.Equals(execution.ErrorClass, NalvaErrorClass.Uncertain, StringComparison.OrdinalIgnoreCase);

        var parsed = results ?? ParseObject(execution.ResultJson);
        var errors = new List<string>();
        if (!string.IsNullOrWhiteSpace(execution.Error))
            errors.Add(execution.Error);

        return new NalvaAgentResultEnvelope
        {
            TaskId = taskId,
            WorkflowRunId = workflowRunId,
            ExecutionId = execution.ExecutionId,
            AgentKey = execution.AgentKey,
            Status = status,
            Verified = verified,
            Results = parsed,
            Evidence = evidence?.ToList() ?? ExtractEvidence(parsed),
            Errors = errors,
            Limitations = limitations?.ToList() ?? [],
            FollowUpTasks = [],
            Usage = new NalvaUsageMetadata
            {
                InputTokens = execution.InputTokens,
                OutputTokens = execution.OutputTokens,
                EstimatedCostUsd = execution.EstimatedCostUsd,
                CostIsEstimate = execution.CostIsEstimate,
                ToolCallCount = execution.Tools.Count,
                DurationMs = 0
            },
            Summary = execution.Summary ?? "",
            CompletedAtUtc = DateTime.UtcNow
        };
    }

    public static string Serialize(NalvaAgentResultEnvelope envelope) =>
        JsonSerializer.Serialize(envelope, JsonOptions);

    public static bool TryParseResult(string? json, out NalvaAgentResultEnvelope? envelope, out string error)
    {
        envelope = null;
        error = "";
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Result JSON is empty.";
            return false;
        }
        try
        {
            envelope = JsonSerializer.Deserialize<NalvaAgentResultEnvelope>(json, JsonOptions);
            if (envelope is null)
            {
                error = "Result JSON could not be deserialized.";
                return false;
            }
            return TryValidateResult(envelope, out error);
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Downstream agents must not treat unverified peer output as fact.
    /// Verified slices are copied; everything else is marked untrusted.
    /// </summary>
    public static JsonObject TrustBoundary(NalvaAgentResultEnvelope peer)
    {
        var payload = new JsonObject
        {
            ["fromAgent"] = peer.AgentKey,
            ["status"] = peer.Status,
            ["verified"] = peer.Verified,
            ["summary"] = peer.Summary,
            ["untrusted"] = !peer.Verified
        };
        if (peer.Verified)
            payload["results"] = peer.Results.DeepClone();
        else
            payload["unverifiedHint"] = "Peer output is not verified. Re-query authoritative records before acting.";
        if (peer.Limitations.Count > 0)
            payload["limitations"] = new JsonArray(peer.Limitations.Select(x => (JsonNode)x).ToArray());
        if (peer.Errors.Count > 0)
            payload["errors"] = new JsonArray(peer.Errors.Select(x => (JsonNode)x).ToArray());
        return payload;
    }

    public static bool AgentsMayEscalatePermissions(string fromAgent, string toAgent) => false;

    public static bool MaySelfApprove(string agentKey, string approvalAction) => false;

    private static JsonObject ParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new JsonObject();
        try
        {
            return JsonNode.Parse(json) as JsonObject ?? new JsonObject { ["raw"] = json };
        }
        catch (JsonException)
        {
            return new JsonObject { ["raw"] = NalvaGuard.Clip(json, 500) };
        }
    }

    private static List<NalvaEvidenceRef> ExtractEvidence(JsonObject data)
    {
        var list = new List<NalvaEvidenceRef>();
        foreach (var key in new[] { "leadId", "customerId", "orderId", "invoiceId", "ticketId", "purchaseOrderId", "workItemId", "approvalId" })
        {
            if (data.TryGetPropertyValue(key, out var node) && Guid.TryParse(node?.GetValue<string>(), out var id))
            {
                list.Add(new NalvaEvidenceRef
                {
                    Kind = key,
                    EntityType = key.Replace("Id", "", StringComparison.Ordinal),
                    EntityId = id,
                    Verified = data["verified"]?.GetValue<bool?>() == true || data["performed"]?.GetValue<bool?>() == true
                });
            }
        }
        return list;
    }
}

public sealed class NalvaObjectiveMatch
{
    public string WorkflowKey { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public double Confidence { get; init; }
    public IReadOnlyList<string> RequiredAgents { get; init; } = [];
    public IReadOnlyList<string> ClarifyingQuestions { get; init; } = [];
    public bool CanStart { get; init; }
    public string? BlockReason { get; init; }
}

public static class NalvaObjectiveRouter
{
    private static readonly (string WorkflowKey, string[] Phrases, string[] Agents, string[] Clarify)[] Routes =
    [
        ("lead-to-customer-collaboration",
            ["qualified lead", "find leads", "lead generation", "prepare outreach", "lead to customer", "prospect", "outreach"],
            ["ceo", "sales", "marketing", "support", "finance"],
            ["How many leads should we research?", "Which product or service should outreach focus on?"]),
        ("order-to-fulfillment",
            ["order fulfillment", "fulfill the order", "customer order", "process order", "coordinate the complete order"],
            ["support", "sales", "procurement", "finance", "ceo", "ecommerce"],
            ["Which order id should we process?"]),
        ("low-stock-to-procurement",
            ["low stock", "procurement", "reorder", "purchase order", "supplier", "out of stock", "analyze our low-stock",
             "restock", "inventory check", "prepare purchase", "kam stock", "stock khatam", "purchase order tayyar",
             "انوینٹری", "خریداری", "اسٹاک"],
            ["inventory", "procurement", "finance", "ceo"],
            []),
        ("complaint-to-resolution",
            ["complaint", "unresolved customer", "customer complaint", "resolve ticket", "refund dispute", "support escalation"],
            ["support", "procurement", "finance", "ceo"],
            ["Which ticket or customer complaint should we open?"]),
        ("weekly-business-review",
            ["weekly business", "weekly report", "business performance", "business review", "executive review", "performance report"],
            ["sales", "marketing", "support", "procurement", "finance", "analyst", "ceo"],
            []),
        ("overdue-payment-followup",
            ["outstanding invoice", "overdue invoice", "payment reminder", "receivable", "customer reminder"],
            ["finance", "sales", "ceo"],
            []),
        ("customer-acquisition",
            ["customer acquisition", "acquire customers", "grow pipeline"],
            ["marketing", "sales", "ceo"],
            []),
        ("navla-marketing-campaign",
            ["marketing campaign", "social campaign", "content campaign"],
            ["ceo", "sales", "marketing", "social"],
            ["What campaign objective should marketing pursue?"])
    ];

    public static NalvaObjectiveMatch Match(string objective)
    {
        var text = objective?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(text))
        {
            return new NalvaObjectiveMatch
            {
                CanStart = false,
                BlockReason = "Provide a business objective.",
                ClarifyingQuestions = ["What business outcome should Navla coordinate?"]
            };
        }

        NalvaObjectiveMatch? best = null;
        foreach (var route in Routes)
        {
            var hits = route.Phrases.Count(p => text.Contains(p, StringComparison.OrdinalIgnoreCase));
            if (hits == 0)
                continue;
            var confidence = Math.Min(0.95, 0.35 + hits * 0.2);
            if (best is null || confidence > best.Confidence)
            {
                NalvaWorkflowCatalog.TryGet(route.WorkflowKey, out var template);
                best = new NalvaObjectiveMatch
                {
                    WorkflowKey = route.WorkflowKey,
                    Name = template?.Name ?? route.WorkflowKey,
                    Description = template?.Description ?? "",
                    Confidence = confidence,
                    RequiredAgents = route.Agents,
                    ClarifyingQuestions = route.Clarify,
                    CanStart = template is not null && confidence >= 0.45,
                    BlockReason = template is null ? "Workflow template is missing from the catalog." : null
                };
            }
        }

        if (best is not null)
            return best;

        return new NalvaObjectiveMatch
        {
            WorkflowKey = "executive-reporting",
            Name = "Executive reporting",
            Description = "Fallback: summarize recorded company totals when no specialized workflow matches.",
            Confidence = 0.25,
            RequiredAgents = ["ceo", "analyst"],
            ClarifyingQuestions =
            [
                "Should this be a lead generation, order fulfillment, procurement, complaint, or weekly review workflow?"
            ],
            CanStart = false,
            BlockReason = "Objective is ambiguous. Clarify the desired workflow before starting."
        };
    }
}

public sealed class NalvaObjectivePlanRequest
{
    public string Objective { get; set; } = "";
    public string? ArgumentsJson { get; set; }
    public bool StartImmediately { get; set; }
    public bool ActivateMissingAgents { get; set; } = true;
    public bool ActivateWorkflow { get; set; } = true;
    public string? IdempotencyKey { get; set; }
}

public sealed record NalvaObjectivePlanDto(
    string Objective,
    string WorkflowKey,
    string WorkflowName,
    string Description,
    double Confidence,
    IReadOnlyList<string> RequiredAgents,
    IReadOnlyList<string> MissingAgents,
    IReadOnlyList<NalvaWorkflowStepWrite> Steps,
    IReadOnlyList<string> ClarifyingQuestions,
    bool CanStart,
    string? BlockReason,
    Guid? WorkflowDefinitionId,
    Guid? WorkflowRunId,
    string? RunStatus,
    string? ReportSummary);

public sealed record NalvaWorkforceBoardDto(
    NalvaOverviewDto Overview,
    IReadOnlyList<NalvaAgentCardDto> Agents,
    IReadOnlyList<NalvaWorkflowRunDto> ActiveWorkflows,
    IReadOnlyList<NalvaWorkflowRunDto> RecentWorkflows,
    IReadOnlyList<NalvaTaskDto> FailedOrBlockedTasks,
    IReadOnlyList<NalvaApprovalDto> PendingApprovals,
    IReadOnlyList<NalvaExecutionDto> RecentExecutions,
    IReadOnlyList<NalvaCollaborationWorkflowInfo> CollaborationWorkflows,
    decimal EstimatedCostUsdThisMonth,
    bool CostIsEstimate);

public sealed record NalvaCollaborationWorkflowInfo(
    string Key,
    string Name,
    string Description,
    string Category,
    IReadOnlyList<string> Agents,
    bool IsActive,
    Guid? DefinitionId);

public sealed record NalvaWorkflowDetailDto(
    NalvaWorkflowRunDto Run,
    IReadOnlyList<NalvaWorkflowStepWrite> Steps,
    string ContextJson,
    string StepLogJson,
    IReadOnlyList<NalvaExecutionDto> Executions,
    IReadOnlyList<NalvaApprovalDto> Approvals,
    IReadOnlyList<NalvaAgentResultEnvelope> AgentResults,
    string FinalReport);

public interface INalvaCollaborationService
{
    Task<NalvaObjectivePlanDto> PlanObjectiveAsync(NalvaObjectivePlanRequest request, CancellationToken cancellationToken = default);
    Task<NalvaObjectivePlanDto> StartObjectiveAsync(NalvaObjectivePlanRequest request, CancellationToken cancellationToken = default);
    Task<NalvaWorkforceBoardDto> GetWorkforceBoardAsync(CancellationToken cancellationToken = default);
    Task<NalvaWorkflowDetailDto> GetWorkflowDetailAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NalvaMemoryDto>> GetSharedContextAsync(string? kind, CancellationToken cancellationToken = default);
    Task<NalvaMemoryDto> StoreSharedDecisionAsync(string title, string content, Guid? executionId, CancellationToken cancellationToken = default);
}
