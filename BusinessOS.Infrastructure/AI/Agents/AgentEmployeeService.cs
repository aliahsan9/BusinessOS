using System.Runtime.CompilerServices;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.Agents.Enums;
using BusinessOS.Application.Features.Agents.Services;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.AI.Services;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.AI.Agents;

public sealed class AgentEmployeeService : IAgentEmployeeService
{
    private readonly IAgentPersonaService _personaService;
    private readonly IVoicePreferenceService _voicePreferenceService;
    private readonly IAgentOnboardingOrchestrator _onboardingOrchestrator;
    private readonly IAgentRuntimeOrchestrator _runtime;
    private readonly IAgentWorkflowService _workflowService;
    private readonly IAiMemoryService _memoryService;
    private readonly IAiInsightService _insightService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AgentEmployeeService> _logger;

    public AgentEmployeeService(
        IAgentPersonaService personaService,
        IVoicePreferenceService voicePreferenceService,
        IAgentOnboardingOrchestrator onboardingOrchestrator,
        IAgentRuntimeOrchestrator runtime,
        IAgentWorkflowService workflowService,
        IAiMemoryService memoryService,
        IAiInsightService insightService,
        ICurrentUserService currentUser,
        ILogger<AgentEmployeeService> logger)
    {
        _personaService = personaService;
        _voicePreferenceService = voicePreferenceService;
        _onboardingOrchestrator = onboardingOrchestrator;
        _runtime = runtime;
        _workflowService = workflowService;
        _memoryService = memoryService;
        _insightService = insightService;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<AgentChatResponse> ChatAsync(
        AgentChatRequest request,
        CancellationToken cancellationToken = default)
    {
        AgentChatResponse? final = null;
        await foreach (var chunk in ChatStreamAsync(request, cancellationToken))
        {
            if (chunk.Type == "final" && chunk.FinalResponse is not null)
                final = chunk.FinalResponse;
        }

        return final ?? new AgentChatResponse
        {
            Reply = "I couldn't complete that request.",
            SpokenReply = "I couldn't complete that request.",
            AgentKey = AgentKeys.Sophia,
            AgentDisplayName = "Sophia",
            Intent = AiCopilotIntent.Unknown
        };
    }

    public async IAsyncEnumerable<AgentStreamChunkDto> ChatStreamAsync(
        AgentChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var voice = await _voicePreferenceService.GetAsync(cancellationToken);
        var agentKey = AgentKeys.Normalize(
            request.AgentKey
            ?? voice.PreferredAgentKey
            ?? await _personaService.GetDefaultAgentKeyAsync(cancellationToken));
        var language = AgentLanguages.Normalize(request.Language ?? voice.Language);

        var persona = await _personaService.ResolvePersonaAsync(agentKey, cancellationToken);

        if (await ShouldRouteToOnboardingAsync(request, cancellationToken))
        {
            yield return new AgentStreamChunkDto { Type = "status", Content = "Starting onboarding…" };

            AgentOnboardingResponse onboarding;
            if (LooksLikeOnboardingStart(request.Message))
            {
                onboarding = await _onboardingOrchestrator.StartAsync(
                    new AgentOnboardingStartRequest
                    {
                        AgentKey = agentKey,
                        Language = language,
                        SessionId = request.SessionId
                    },
                    cancellationToken);
            }
            else
            {
                onboarding = await _onboardingOrchestrator.ContinueAsync(
                    new AgentOnboardingContinueRequest
                    {
                        Message = request.Message,
                        AgentKey = agentKey,
                        Language = language,
                        SessionId = request.SessionId,
                        WorkflowId = request.WorkflowId
                    },
                    cancellationToken);
            }

            var mapped = MapOnboardingToChat(onboarding);
            foreach (var step in mapped.WorkflowSteps)
            {
                yield return new AgentStreamChunkDto
                {
                    Type = "workflow_step",
                    Content = step.Title,
                    WorkflowStep = step,
                    WorkflowId = mapped.WorkflowId
                };
            }

            yield return new AgentStreamChunkDto
            {
                Type = "final",
                Content = mapped.Reply,
                FinalResponse = mapped,
                WorkflowId = mapped.WorkflowId
            };
            yield break;
        }

        await foreach (var chunk in _runtime.RunStreamAsync(
                           request, agentKey, language, persona, cancellationToken))
        {
            yield return chunk;
        }
    }

    public Task<IReadOnlyList<AgentEmployeeDto>> ListEmployeesAsync(
        CancellationToken cancellationToken = default) =>
        _personaService.ListEmployeesAsync(cancellationToken);

    public Task<VoicePreferenceDto> GetVoicePreferencesAsync(
        CancellationToken cancellationToken = default) =>
        _voicePreferenceService.GetAsync(cancellationToken);

    public Task<VoicePreferenceDto> SaveVoicePreferencesAsync(
        SaveVoicePreferenceRequest request,
        CancellationToken cancellationToken = default) =>
        _voicePreferenceService.SaveAsync(request, cancellationToken);

    public Task<AgentOnboardingResponse> StartOnboardingWithAgentAsync(
        AgentOnboardingStartRequest request,
        CancellationToken cancellationToken = default) =>
        _onboardingOrchestrator.StartAsync(request, cancellationToken);

    public Task<AgentOnboardingResponse> ContinueOnboardingAsync(
        AgentOnboardingContinueRequest request,
        CancellationToken cancellationToken = default) =>
        _onboardingOrchestrator.ContinueAsync(request, cancellationToken);

    public Task<AgentWorkflowDto?> GetWorkflowAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default) =>
        _workflowService.GetAsync(workflowId, cancellationToken);

    public async Task<IReadOnlyList<AgentWorkflowSummaryDto>> ListRecentWorkflowsAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId
            ?? throw new InvalidOperationException("User context is required.");
        return await _workflowService.ListRecentAsync(userId, limit, cancellationToken);
    }

    public async Task<AskSophiaSuggestionsDto> GetAskSophiaSuggestionsAsync(
        CancellationToken cancellationToken = default)
    {
        var persona = await _personaService.ResolvePersonaAsync(AgentKeys.Sophia, cancellationToken);

        var suggestions = new List<AskSophiaSuggestionDto>
        {
            new()
            {
                Label = "Inventory summary",
                Message = "Show my inventory summary",
                Category = "inventory",
                AgentKey = AgentKeys.Adam,
                Icon = "bi-box-seam"
            },
            new()
            {
                Label = "Low stock alerts",
                Message = "Which products are low in stock?",
                Category = "inventory",
                AgentKey = AgentKeys.Adam,
                Icon = "bi-exclamation-triangle"
            },
            new()
            {
                Label = "Sales report",
                Message = "Generate a sales report for this month",
                Category = "sales",
                AgentKey = AgentKeys.Emma,
                Icon = "bi-graph-up-arrow"
            },
            new()
            {
                Label = "What should I buy?",
                Message = "What should I buy or reorder?",
                Category = "recommendations",
                AgentKey = AgentKeys.Adam,
                Icon = "bi-cart3"
            },
            new()
            {
                Label = "Create a customer",
                Message = "Create a new customer",
                Category = "customers",
                AgentKey = AgentKeys.Sophia,
                Icon = "bi-person-plus"
            }
        };

        try
        {
            var insights = await _insightService.GetProactiveInsightsAsync(cancellationToken);
            foreach (var insight in insights.Take(4))
            {
                suggestions.Add(new AskSophiaSuggestionDto
                {
                    Label = insight.Title,
                    Message = insight.Message,
                    Category = insight.Type,
                    AgentKey = AgentKeys.Sophia,
                    Icon = insight.Severity.Equals("high", StringComparison.OrdinalIgnoreCase)
                        ? "bi-exclamation-circle"
                        : "bi-lightbulb"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Proactive insights unavailable for Ask Sophia");
        }

        var greeting = $"Hi — I'm {persona.DisplayName}. How can I help your business today?";

        return new AskSophiaSuggestionsDto
        {
            Greeting = greeting,
            AgentKey = persona.Key,
            AgentDisplayName = persona.DisplayName,
            Suggestions = suggestions
        };
    }

    private async Task<bool> ShouldRouteToOnboardingAsync(
        AgentChatRequest request,
        CancellationToken cancellationToken)
    {
        if (LooksLikeOnboardingStart(request.Message))
            return true;

        if (request.SessionId is null)
            return false;

        var memory = await _memoryService.LoadAsync(request.SessionId.Value, cancellationToken);
        if (memory.OnboardingStep is > 0 and < 9)
            return true;

        if (!string.IsNullOrWhiteSpace(memory.OnboardingDataJson)
            && memory.LastIntent?.Equals("Onboarding", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        var state = await _onboardingOrchestrator.GetCurrentStateAsync(request.SessionId, cancellationToken);
        return state is { IsComplete: false };
    }

    private static bool LooksLikeOnboardingStart(string message)
    {
        var text = message.Trim().ToLowerInvariant();
        return text.Contains("onboard")
            || text.Contains("setup company")
            || text.Contains("set up my business")
            || text.Contains("set up company")
            || text.Contains("set up our business")
            || text.Contains("configure my business")
            || text.Contains("start setup");
    }

    private static AgentChatResponse MapOnboardingToChat(AgentOnboardingResponse o) => new()
    {
        Reply = o.Reply,
        SpokenReply = o.SpokenReply ?? o.Reply,
        SessionId = o.SessionId,
        AgentKey = o.AgentKey,
        AgentDisplayName = o.AgentDisplayName,
        Intent = AiCopilotIntent.Onboarding,
        WorkflowId = o.WorkflowId,
        WorkflowSteps = o.WorkflowSteps,
        Suggestions = o.Suggestions
    };
}
