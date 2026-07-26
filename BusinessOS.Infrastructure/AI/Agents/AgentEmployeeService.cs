using System.Runtime.CompilerServices;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.Agents.Enums;
using BusinessOS.Application.Features.Agents.Services;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.AI.Services;
using BusinessOS.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.AI.Agents;

public sealed class AgentEmployeeService : IAgentEmployeeService
{
    private readonly IAgentPersonaService _personaService;
    private readonly IVoicePreferenceService _voicePreferenceService;
    private readonly IAgentOnboardingOrchestrator _onboardingOrchestrator;
    private readonly IAgentPlanner _planner;
    private readonly IAgentWorkflowService _workflowService;
    private readonly IAiCopilotOrchestrator _copilot;
    private readonly IAiIntentDetector _intentDetector;
    private readonly IAiMemoryService _memoryService;
    private readonly IAiContextService _contextService;
    private readonly IAiInsightService _insightService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AgentEmployeeService> _logger;

    public AgentEmployeeService(
        IAgentPersonaService personaService,
        IVoicePreferenceService voicePreferenceService,
        IAgentOnboardingOrchestrator onboardingOrchestrator,
        IAgentPlanner planner,
        IAgentWorkflowService workflowService,
        IAiCopilotOrchestrator copilot,
        IAiIntentDetector intentDetector,
        IAiMemoryService memoryService,
        IAiContextService contextService,
        IAiInsightService insightService,
        ICurrentUserService currentUser,
        ILogger<AgentEmployeeService> logger)
    {
        _personaService = personaService;
        _voicePreferenceService = voicePreferenceService;
        _onboardingOrchestrator = onboardingOrchestrator;
        _planner = planner;
        _workflowService = workflowService;
        _copilot = copilot;
        _intentDetector = intentDetector;
        _memoryService = memoryService;
        _contextService = contextService;
        _insightService = insightService;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<AgentChatResponse> ChatAsync(
        AgentChatRequest request,
        CancellationToken cancellationToken = default)
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
            if (LooksLikeOnboardingStart(request.Message))
            {
                var started = await _onboardingOrchestrator.StartAsync(
                    new AgentOnboardingStartRequest
                    {
                        AgentKey = agentKey,
                        Language = language,
                        SessionId = request.SessionId
                    },
                    cancellationToken);
                return MapOnboardingToChat(started);
            }

            var continued = await _onboardingOrchestrator.ContinueAsync(
                new AgentOnboardingContinueRequest
                {
                    Message = request.Message,
                    AgentKey = agentKey,
                    Language = language,
                    SessionId = request.SessionId,
                    WorkflowId = request.WorkflowId
                },
                cancellationToken);
            return MapOnboardingToChat(continued);
        }

        var copilotRequest = new AiCopilotChatRequest(
            request.Message,
            request.CurrentPage,
            request.SearchQuery,
            request.CustomerId,
            request.OrderId,
            request.InvoiceId,
            request.ProjectId,
            request.SessionId,
            request.Stream,
            agentKey,
            language,
            request.PreferEmployeeTone,
            request.WorkflowId);

        var page = _contextService.BuildPageContext(
            new AiChatRequest(request.Message, request.CurrentPage, request.SearchQuery,
                request.CustomerId, request.OrderId, request.InvoiceId, request.ProjectId));

        Guid? sessionId = request.SessionId;
        AiMemoryStateDto memory = new();
        if (sessionId is not null)
            memory = await _memoryService.LoadAsync(sessionId.Value, cancellationToken);

        var intent = _intentDetector.Detect(request.Message, page, memory);

        AgentWorkflowDto? workflow = null;
        IReadOnlyList<AgentWorkflowStepDto> workflowSteps = [];

        if (_planner.RequiresWorkflow(intent.Intent, request.Message, memory))
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("User context is required.");

            var plan = _planner.Plan(agentKey, intent.Intent, request.Message, page, memory);
            workflow = await _workflowService.CreateFromPlanAsync(plan, userId, sessionId, cancellationToken);
            workflow = await _workflowService.StartAsync(workflow.Id, cancellationToken);

            foreach (var step in plan.Steps.OrderBy(s => s.SortOrder))
            {
                await _workflowService.BeginStepAsync(workflow.Id, step.StepKey, step.Title, cancellationToken);
            }

            copilotRequest = copilotRequest with { WorkflowId = workflow.Id };
        }

        var copilotResponse = await _copilot.ProcessAsync(copilotRequest, cancellationToken);

        if (workflow is not null)
        {
            foreach (var step in workflow.Steps.OrderBy(s => s.SortOrder))
            {
                try
                {
                    await _workflowService.CompleteStepAsync(
                        workflow.Id,
                        step.StepKey,
                        "Completed via agent chat",
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not complete workflow step {Step}", step.StepKey);
                }
            }

            await _workflowService.CompleteAsync(
                workflow.Id,
                Truncate(copilotResponse.Reply, 500),
                cancellationToken);

            var refreshed = await _workflowService.GetAsync(workflow.Id, cancellationToken);
            workflowSteps = refreshed?.Steps ?? workflow.Steps;
        }
        else if (copilotResponse.WorkflowSteps.Count > 0)
        {
            workflowSteps = copilotResponse.WorkflowSteps.Select(s => new AgentWorkflowStepDto
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
            }).ToList();
        }

        return new AgentChatResponse
        {
            Reply = copilotResponse.Reply,
            SpokenReply = copilotResponse.SpokenReply ?? copilotResponse.Reply,
            SessionId = copilotResponse.SessionId,
            AgentKey = copilotResponse.AgentKey ?? persona.Key,
            AgentDisplayName = copilotResponse.AgentDisplayName ?? persona.DisplayName,
            Intent = copilotResponse.Intent,
            WorkflowId = workflow?.Id ?? copilotResponse.WorkflowId,
            WorkflowSteps = workflowSteps,
            ToolsUsed = copilotResponse.ToolsUsed,
            Citations = copilotResponse.Citations,
            Suggestions = copilotResponse.Suggestions,
            QuickActions = copilotResponse.QuickActions,
            SearchResults = copilotResponse.SearchResults,
            Sources = copilotResponse.Sources,
            ActionResult = copilotResponse.ActionResult,
            PermissionDenied = copilotResponse.PermissionDenied
        };
    }

    public async IAsyncEnumerable<AgentStreamChunkDto> ChatStreamAsync(
        AgentChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new AgentStreamChunkDto { Type = "status", Content = "Thinking…" };

        AgentChatResponse? final = null;
        Exception? error = null;

        try
        {
            final = await ChatAsync(new AgentChatRequest
            {
                Message = request.Message,
                AgentKey = request.AgentKey,
                Language = request.Language,
                CurrentPage = request.CurrentPage,
                SearchQuery = request.SearchQuery,
                CustomerId = request.CustomerId,
                OrderId = request.OrderId,
                InvoiceId = request.InvoiceId,
                ProjectId = request.ProjectId,
                SessionId = request.SessionId,
                WorkflowId = request.WorkflowId,
                PreferEmployeeTone = request.PreferEmployeeTone,
                Stream = true
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            error = ex;
            _logger.LogError(ex, "Agent chat stream failed");
        }

        if (error is not null)
        {
            yield return new AgentStreamChunkDto { Type = "error", Content = error.Message };
            yield break;
        }

        if (final is null)
            yield break;

        if (final.WorkflowId is not null)
        {
            yield return new AgentStreamChunkDto
            {
                Type = "status",
                Content = "Running workflow…",
                WorkflowId = final.WorkflowId
            };

            foreach (var step in final.WorkflowSteps)
            {
                yield return new AgentStreamChunkDto
                {
                    Type = "workflow_step",
                    Content = step.Title,
                    WorkflowStep = step,
                    WorkflowId = final.WorkflowId
                };
            }
        }

        foreach (var word in final.Reply.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            yield return new AgentStreamChunkDto { Type = "token", Content = word + " " };
            await Task.Delay(12, cancellationToken);
        }

        yield return new AgentStreamChunkDto
        {
            Type = "final",
            Content = final.Reply,
            FinalResponse = final,
            WorkflowId = final.WorkflowId
        };
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
        var voice = await _voicePreferenceService.GetAsync(cancellationToken);
        var language = AgentLanguages.Normalize(voice.Language);

        var suggestions = new List<AskSophiaSuggestionDto>
        {
            new()
            {
                Label = language == AgentLanguages.Urdu ? "انوینٹری خلاصہ" : "Inventory summary",
                Message = "Show my inventory summary",
                Category = "inventory",
                AgentKey = AgentKeys.Adam,
                Icon = "📦"
            },
            new()
            {
                Label = language == AgentLanguages.Urdu ? "کم اسٹاک" : "Low stock alerts",
                Message = "Which products are low in stock?",
                Category = "inventory",
                AgentKey = AgentKeys.Adam,
                Icon = "⚠️"
            },
            new()
            {
                Label = language == AgentLanguages.Urdu ? "سیلز رپورٹ" : "Sales report",
                Message = "Generate a sales report for this month",
                Category = "sales",
                AgentKey = AgentKeys.Emma,
                Icon = "📈"
            },
            new()
            {
                Label = language == AgentLanguages.Urdu ? "خریداری تجاویز" : "What should I buy?",
                Message = "What should I buy or reorder?",
                Category = "recommendations",
                AgentKey = AgentKeys.Adam,
                Icon = "🛒"
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
                    Icon = insight.Severity.Equals("high", StringComparison.OrdinalIgnoreCase) ? "🔴" : "💡"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Proactive insights unavailable for Ask Sophia");
        }

        var greeting = language == AgentLanguages.Urdu
            ? $"السلام علیکم — میں {persona.DisplayName} ہوں۔ آج کس میں مدد کر سکتی ہوں؟"
            : $"Hi — I'm {persona.DisplayName}. How can I help your business today?";

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

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";
}
