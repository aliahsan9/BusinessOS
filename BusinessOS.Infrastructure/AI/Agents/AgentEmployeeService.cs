using System.Runtime.CompilerServices;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.Agents;
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
            AgentDisplayName = "Navla AI",
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
        var voiceLanguage = AgentLanguages.Coalesce(voice.Language);
        var language = ConversationLanguage.ResolveTurn(
            request.Message,
            request.Language,
            memoryLanguage: null,
            voiceLanguage);

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
                        SessionId = request.SessionId ?? request.ConversationId
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
                        SessionId = request.SessionId ?? request.ConversationId,
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
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId
            ?? throw new InvalidOperationException("User context is required.");
        return _workflowService.GetForUserAsync(workflowId, userId, cancellationToken);
    }

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

        var suggestions = new List<AskSophiaSuggestionDto>();
        var canAnalytics = CanSee(PermissionCodes.AnalyticsView, PermissionCodes.FinanceView)
            || HasRole("Owner", "Admin", "Manager", "Accountant");
        var canInventory = CanSee(PermissionCodes.InventoryView, PermissionCodes.ProductView)
            || HasRole("Owner", "Admin", "Manager");
        var canFinance = CanSee(PermissionCodes.FinanceView, PermissionCodes.InvoiceView)
            || HasRole("Owner", "Admin", "Manager", "Accountant");
        var canReports = CanSee(PermissionCodes.ReportView) || HasRole("Owner", "Admin", "Manager", "Accountant");

        if (canAnalytics)
        {
            suggestions.Add(Suggestion("Analyze my business performance", "Analyze my business performance", "performance"));
            suggestions.Add(Suggestion("Show my top-selling products", "Show my top-selling products", "sales"));
            suggestions.Add(Suggestion("Compare this month with last month", "Compare this month with last month", "sales"));
        }

        if (canInventory)
            suggestions.Add(Suggestion("Which products need restocking?", "Which products need restocking?", "inventory"));

        if (canFinance)
            suggestions.Add(Suggestion("Show outstanding customer payments", "Show outstanding customer payments", "finance"));

        if (canReports)
            suggestions.Add(Suggestion("Generate my weekly report", "Generate my weekly sales report", "reports"));

        if (suggestions.Count == 0)
            suggestions.Add(Suggestion("What can you help with?", "What can you help me with?", "help"));

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
            _logger.LogDebug(ex, "Proactive insights unavailable for Ask Navla");
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

    private static AskSophiaSuggestionDto Suggestion(string label, string message, string category) =>
        new()
        {
            Label = label,
            Message = message,
            Category = category,
            AgentKey = AgentKeys.Sophia,
            Icon = "bi-chat-dots"
        };

    private bool CanSee(params string[] permissions) =>
        permissions.Any(permission => _currentUser.HasPermission(permission));

    private bool HasRole(params string[] roles) =>
        _currentUser.Roles.Any(role => roles.Contains(role, StringComparer.OrdinalIgnoreCase));

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
