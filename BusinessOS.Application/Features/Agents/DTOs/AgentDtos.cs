using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Features.Agents.DTOs;

// ── Employee catalog ──────────────────────────────────────────────────────────

public sealed class AgentEmployeeDto
{
    public string Key { get; init; } = default!;
    public string DisplayName { get; init; } = default!;
    public string RoleTitle { get; init; } = default!;
    public string Specialty { get; init; } = default!;
    public string DefaultLanguage { get; init; } = "en";
    public bool IsDefault { get; init; }
    public bool IsActive { get; init; } = true;
    public string? AvatarHint { get; init; }
}

public sealed class AgentPersonaDto
{
    public string Key { get; init; } = default!;
    public string DisplayName { get; init; } = default!;
    public string RoleTitle { get; init; } = default!;
    public string Specialty { get; init; } = default!;
    public string SystemPersonaPrompt { get; init; } = default!;
    public string DefaultLanguage { get; init; } = "en";
    public bool IsDefault { get; init; }
}

// ── Chat with agent ───────────────────────────────────────────────────────────

public sealed class AgentChatRequest
{
    public string Message { get; init; } = default!;
    public string? AgentKey { get; init; }
    public string? Language { get; init; }
    public string? CurrentPage { get; init; }
    public string? SearchQuery { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? OrderId { get; init; }
    public Guid? InvoiceId { get; init; }
    public Guid? ProjectId { get; init; }
    public Guid? SessionId { get; init; }
    public Guid? WorkflowId { get; init; }
    public bool PreferEmployeeTone { get; init; } = true;
    public bool Stream { get; init; }
}

public sealed class AgentChatResponse
{
    public string Reply { get; init; } = default!;
    public string? SpokenReply { get; init; }
    public Guid? SessionId { get; init; }
    public string AgentKey { get; init; } = default!;
    public string AgentDisplayName { get; init; } = default!;
    public AiCopilotIntent Intent { get; init; }
    public Guid? WorkflowId { get; init; }
    public IReadOnlyList<AgentWorkflowStepDto> WorkflowSteps { get; init; } = [];
    public IReadOnlyList<string> ToolsUsed { get; init; } = [];
    public IReadOnlyList<AiCitationDto> Citations { get; init; } = [];
    public IReadOnlyList<AiSuggestionDto> Suggestions { get; init; } = [];
    public IReadOnlyList<AiQuickActionDto> QuickActions { get; init; } = [];
    public IReadOnlyList<AiSearchResultDto> SearchResults { get; init; } = [];
    public AiRetrievedSourcesDto Sources { get; init; } = new();
    public AiActionResultDto? ActionResult { get; init; }
    public bool PermissionDenied { get; init; }
}

/// <summary>
/// Streaming chunk for agent chat, optionally carrying workflow step updates.
/// </summary>
public sealed class AgentStreamChunkDto
{
    /// <summary>
    /// token | status | workflow_step | final | error
    /// </summary>
    public string Type { get; init; } = "token";

    public string? Content { get; init; }

    public AgentWorkflowStepDto? WorkflowStep { get; init; }

    public Guid? WorkflowId { get; init; }

    public AgentChatResponse? FinalResponse { get; init; }
}

// ── Voice preferences ─────────────────────────────────────────────────────────

public sealed class VoicePreferenceDto
{
    public Guid Id { get; init; }
    public string Language { get; init; } = "en";
    public AgentVoiceLanguage VoiceLanguage { get; init; } = AgentVoiceLanguage.En;
    public string VoiceName { get; init; } = "default";
    public double SpeechRate { get; init; } = 1.0;
    public double Pitch { get; init; } = 1.0;
    public bool ContinuousListening { get; init; }
    public bool AutoSpeak { get; init; } = true;
    public string? PreferredAgentKey { get; init; }
}

public sealed class SaveVoicePreferenceRequest
{
    public string Language { get; init; } = "en";
    public string VoiceName { get; init; } = "default";
    public double SpeechRate { get; init; } = 1.0;
    public double Pitch { get; init; } = 1.0;
    public bool ContinuousListening { get; init; }
    public bool AutoSpeak { get; init; } = true;
    public string? PreferredAgentKey { get; init; }
}

// ── Workflow progress ─────────────────────────────────────────────────────────

public sealed class AgentWorkflowStepDto
{
    public Guid Id { get; init; }
    public string StepKey { get; init; } = default!;
    public string Title { get; init; } = default!;
    public AgentWorkflowStepStatus Status { get; init; }
    public int SortOrder { get; init; }
    public string? Message { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}

public sealed class AgentWorkflowDto
{
    public Guid Id { get; init; }
    public string AgentKey { get; init; } = default!;
    public string? AgentDisplayName { get; init; }
    public string Title { get; init; } = default!;
    public AgentWorkflowStatus Status { get; init; }
    public int CurrentStepIndex { get; init; }
    public string? ResultSummary { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? SessionId { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public IReadOnlyList<AgentWorkflowStepDto> Steps { get; init; } = [];
}

public sealed class AgentWorkflowSummaryDto
{
    public Guid Id { get; init; }
    public string AgentKey { get; init; } = default!;
    public string Title { get; init; } = default!;
    public AgentWorkflowStatus Status { get; init; }
    public int CurrentStepIndex { get; init; }
    public int TotalSteps { get; init; }
    public string? ResultSummary { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}

public sealed class AgentWorkflowPlanDto
{
    public string Title { get; init; } = default!;
    public string AgentKey { get; init; } = default!;
    public AiCopilotIntent Intent { get; init; }
    public IReadOnlyList<AgentPlannedStepDto> Steps { get; init; } = [];
}

public sealed class AgentPlannedStepDto
{
    public string StepKey { get; init; } = default!;
    public string Title { get; init; } = default!;
    public int SortOrder { get; init; }
    public AiToolName? ToolName { get; init; }
}

// ── Onboarding conversation ───────────────────────────────────────────────────

public sealed class AgentOnboardingStartRequest
{
    public string? AgentKey { get; init; }
    public string? Language { get; init; }
    public Guid? SessionId { get; init; }
}

public sealed class AgentOnboardingContinueRequest
{
    public string Message { get; init; } = default!;
    public string? AgentKey { get; init; }
    public string? Language { get; init; }
    public Guid? SessionId { get; init; }
    public Guid? WorkflowId { get; init; }
}

public sealed class AgentOnboardingResponse
{
    public string Reply { get; init; } = default!;
    public string? SpokenReply { get; init; }
    public string AgentKey { get; init; } = default!;
    public string AgentDisplayName { get; init; } = default!;
    public Guid? SessionId { get; init; }
    public Guid? WorkflowId { get; init; }
    public int CurrentStep { get; init; }
    public string? StepKey { get; init; }
    public bool IsComplete { get; init; }
    public IReadOnlyDictionary<string, string?> CollectedData { get; init; }
        = new Dictionary<string, string?>();
    public IReadOnlyList<AiSuggestionDto> Suggestions { get; init; } = [];
    public IReadOnlyList<AgentWorkflowStepDto> WorkflowSteps { get; init; } = [];
}

// ── Ask Sophia (dashboard) ────────────────────────────────────────────────────

public sealed class AskSophiaSuggestionDto
{
    public string Label { get; init; } = default!;
    public string Message { get; init; } = default!;
    public string? Category { get; init; }
    public string? AgentKey { get; init; }
    public string? Icon { get; init; }
}

public sealed class AskSophiaSuggestionsDto
{
    public string Greeting { get; init; } = default!;
    public string AgentKey { get; init; } = default!;
    public string AgentDisplayName { get; init; } = default!;
    public IReadOnlyList<AskSophiaSuggestionDto> Suggestions { get; init; } = [];
}
