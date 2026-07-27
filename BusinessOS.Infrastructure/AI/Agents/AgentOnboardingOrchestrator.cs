using System.Text.Json;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.Agents.Enums;
using BusinessOS.Application.Features.Agents.Services;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Services;
using BusinessOS.Application.Features.Onboarding.DTOs;
using BusinessOS.Application.Features.Onboarding.Services;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.AI.Agents;

public sealed class AgentOnboardingOrchestrator : IAgentOnboardingOrchestrator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static readonly string[] StepKeys =
    [
        "welcome",
        "business_name",
        "industry",
        "size",
        "currency",
        "country_timezone",
        "tax",
        "warehouse",
        "categories",
        "confirm_apply"
    ];

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAgentPersonaService _personaService;
    private readonly IAgentPlanner _planner;
    private readonly IAgentWorkflowService _workflowService;
    private readonly IOnboardingService _onboardingService;
    private readonly IAiMemoryService _memoryService;
    private readonly ILogger<AgentOnboardingOrchestrator> _logger;

    public AgentOnboardingOrchestrator(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IAgentPersonaService personaService,
        IAgentPlanner planner,
        IAgentWorkflowService workflowService,
        IOnboardingService onboardingService,
        IAiMemoryService memoryService,
        ILogger<AgentOnboardingOrchestrator> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _personaService = personaService;
        _planner = planner;
        _workflowService = workflowService;
        _onboardingService = onboardingService;
        _memoryService = memoryService;
        _logger = logger;
    }

    public async Task<AgentOnboardingResponse> StartAsync(
        AgentOnboardingStartRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var agentKey = AgentKeys.Normalize(request.AgentKey ?? AgentKeys.Sophia);
        var language = AgentLanguages.Normalize(request.Language);
        var persona = await _personaService.ResolvePersonaAsync(agentKey, cancellationToken);

        var sessionId = await _memoryService.GetOrCreateSessionAsync(
            new AiChatRequest("Start onboarding"),
            request.SessionId,
            cancellationToken);

        var plan = _planner.PlanOnboarding(agentKey, language);
        var workflow = await _workflowService.CreateFromPlanAsync(plan, userId, sessionId, cancellationToken);
        workflow = await _workflowService.StartAsync(workflow.Id, cancellationToken);
        await _workflowService.BeginStepAsync(workflow.Id, "welcome", null, cancellationToken);
        await _workflowService.CompleteStepAsync(workflow.Id, "welcome", "Welcome delivered", cancellationToken);
        await _workflowService.BeginStepAsync(workflow.Id, "business_name", null, cancellationToken);

        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["language"] = language
        };

        await PersistOnboardingMemoryAsync(sessionId, 1, data, agentKey, language, cancellationToken);
        await _workflowService.UpdateProgressJsonAsync(
            workflow.Id,
            JsonSerializer.Serialize(data, JsonOptions),
            cancellationToken);

        var reply = $"Hi — I'm {persona.DisplayName}, your Senior Business Analyst. Let's set up your business together. First, what's your business name?";

        var refreshed = await _workflowService.GetAsync(workflow.Id, cancellationToken) ?? workflow;

        return BuildResponse(reply, persona, sessionId, refreshed, currentStep: 1, stepKey: "business_name", data, false);
    }

    public async Task<AgentOnboardingResponse> ContinueAsync(
        AgentOnboardingContinueRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var agentKey = AgentKeys.Normalize(request.AgentKey ?? AgentKeys.Sophia);
        var persona = await _personaService.ResolvePersonaAsync(agentKey, cancellationToken);

        var sessionId = request.SessionId
            ?? await _memoryService.GetOrCreateSessionAsync(
                new AiChatRequest(request.Message),
                null,
                cancellationToken);

        var memory = await _memoryService.LoadAsync(sessionId, cancellationToken);
        var language = AgentLanguages.Normalize(request.Language ?? memory.PreferredLanguage);
        var data = ParseData(memory.OnboardingDataJson);
        data["language"] = language;

        var currentStep = memory.OnboardingStep ?? 1;
        if (currentStep < 1) currentStep = 1;

        var workflowId = request.WorkflowId;
        AgentWorkflowDto? workflow = null;

        if (workflowId is not null)
            workflow = await _workflowService.GetAsync(workflowId.Value, cancellationToken);

        if (workflow is null)
        {
            var plan = _planner.PlanOnboarding(agentKey, language);
            workflow = await _workflowService.CreateFromPlanAsync(plan, userId, sessionId, cancellationToken);
            workflow = await _workflowService.StartAsync(workflow.Id, cancellationToken);
            workflowId = workflow.Id;
        }

        var answer = request.Message.Trim();
        var stepKey = StepKeys[Math.Clamp(currentStep, 0, StepKeys.Length - 1)];

        ApplyAnswer(stepKey, answer, data);

        if (currentStep is >= 1 and <= 8)
        {
            await SafeCompleteStepAsync(workflow.Id, stepKey, answer, cancellationToken);
        }

        // Persist profile as soon as we have enough core fields.
        if (HasEnoughForProfile(data))
        {
            await TrySaveBusinessProfileAsync(data, cancellationToken);
        }

        var nextStep = currentStep + 1;
        string reply;
        var isComplete = false;

        if (nextStep >= StepKeys.Length - 1)
        {
            // confirm_apply
            await SafeBeginStepAsync(workflow.Id, "confirm_apply", null, cancellationToken);
            await ApplyTenantDefaultsAsync(data, cancellationToken);
            await TrySaveBusinessProfileAsync(data, cancellationToken);
            await _onboardingService.CompleteAsync(cancellationToken);
            await _workflowService.CompleteStepAsync(workflow.Id, "confirm_apply", "Profile applied", cancellationToken);
            await _workflowService.CompleteAsync(workflow.Id, "Onboarding complete", cancellationToken);

            nextStep = StepKeys.Length - 1;
            isComplete = true;
            reply = $"Perfect — I've saved your business profile and defaults. I'm {persona.DisplayName}, ready to help with day-to-day work whenever you need me.";
        }
        else
        {
            var nextKey = StepKeys[nextStep];
            await SafeBeginStepAsync(workflow.Id, nextKey, null, cancellationToken);
            reply = BuildPrompt(nextKey, language, persona.DisplayName, data);
        }

        await PersistOnboardingMemoryAsync(sessionId, nextStep, data, agentKey, language, cancellationToken);
        await _workflowService.UpdateProgressJsonAsync(
            workflow.Id,
            JsonSerializer.Serialize(data, JsonOptions),
            cancellationToken);

        var refreshed = await _workflowService.GetAsync(workflow.Id, cancellationToken) ?? workflow;
        var responseStepKey = isComplete ? "confirm_apply" : StepKeys[Math.Clamp(nextStep, 0, StepKeys.Length - 1)];

        _logger.LogInformation(
            "Onboarding continue session {SessionId} step {Step} complete={Complete}",
            sessionId,
            nextStep,
            isComplete);

        return BuildResponse(reply, persona, sessionId, refreshed, nextStep, responseStepKey, data, isComplete);
    }

    public async Task<AgentOnboardingResponse?> GetCurrentStateAsync(
        Guid? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        if (sessionId is null)
            return null;

        var memory = await _memoryService.LoadAsync(sessionId.Value, cancellationToken);
        if (memory.OnboardingStep is null && string.IsNullOrWhiteSpace(memory.OnboardingDataJson))
            return null;

        var agentKey = AgentKeys.Normalize(memory.PreferredAgentKey);
        var persona = await _personaService.ResolvePersonaAsync(agentKey, cancellationToken);
        var data = ParseData(memory.OnboardingDataJson);
        var step = memory.OnboardingStep ?? 0;
        var stepKey = StepKeys[Math.Clamp(step, 0, StepKeys.Length - 1)];
        var language = AgentLanguages.Normalize(memory.PreferredLanguage ?? data.GetValueOrDefault("language"));
        var isComplete = step >= StepKeys.Length - 1 && HasEnoughForProfile(data);

        return new AgentOnboardingResponse
        {
            Reply = BuildPrompt(stepKey, language, persona.DisplayName, data),
            SpokenReply = null,
            AgentKey = persona.Key,
            AgentDisplayName = persona.DisplayName,
            SessionId = sessionId,
            WorkflowId = null,
            CurrentStep = step,
            StepKey = stepKey,
            IsComplete = isComplete,
            CollectedData = data
        };
    }

    private async Task TrySaveBusinessProfileAsync(
        Dictionary<string, string?> data,
        CancellationToken cancellationToken)
    {
        if (!HasEnoughForProfile(data))
            return;

        var name = data.GetValueOrDefault("business_name")?.Trim() ?? "My Business";
        var industry = data.GetValueOrDefault("industry")?.Trim() ?? "General";
        var currency = data.GetValueOrDefault("currency")?.Trim() ?? "USD";
        var timezone = data.GetValueOrDefault("timezone")?.Trim()
            ?? data.GetValueOrDefault("country_timezone")?.Trim()
            ?? "UTC";
        var size = data.GetValueOrDefault("size");
        var description = string.IsNullOrWhiteSpace(size) ? null : $"Business size: {size}";

        await _onboardingService.SaveBusinessProfileAsync(
            new SaveOnboardingBusinessProfileRequest(
                name,
                null,
                null,
                industry,
                description,
                currency,
                timezone),
            cancellationToken);
    }

    private async Task ApplyTenantDefaultsAsync(
        Dictionary<string, string?> data,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId is null)
            return;

        var settings = await _context.TenantSettings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);

        if (settings is null)
        {
            settings = new TenantSettings
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value
            };
            _context.TenantSettings.Add(settings);
        }

        if (decimal.TryParse(data.GetValueOrDefault("tax"), out var taxRate))
            settings.TaxRate = Math.Clamp(taxRate, 0, 100);

        if (!string.IsNullOrWhiteSpace(data.GetValueOrDefault("currency")))
            settings.Currency = data["currency"]!.Trim();

        if (!string.IsNullOrWhiteSpace(data.GetValueOrDefault("timezone")))
            settings.Timezone = data["timezone"]!.Trim();

        settings.InventoryAlertsEnabled = true;
        settings.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static void ApplyAnswer(string stepKey, string answer, Dictionary<string, string?> data)
    {
        switch (stepKey)
        {
            case "business_name":
                data["business_name"] = answer;
                break;
            case "industry":
                data["industry"] = answer;
                break;
            case "size":
                data["size"] = answer;
                break;
            case "currency":
                data["currency"] = NormalizeCurrency(answer);
                break;
            case "country_timezone":
                data["country_timezone"] = answer;
                data["country"] = answer;
                data["timezone"] = InferTimezone(answer);
                break;
            case "tax":
                data["tax"] = ExtractNumber(answer) ?? answer;
                break;
            case "warehouse":
                data["warehouse"] = answer;
                break;
            case "categories":
                data["categories"] = answer;
                break;
            case "confirm_apply":
                data["confirmed"] = answer;
                break;
        }
    }

    private static string BuildPrompt(
        string stepKey,
        string language,
        string displayName,
        IReadOnlyDictionary<string, string?> data) =>
        stepKey switch
        {
            "welcome" => $"Hi — I'm {displayName}. Let's get your business set up.",
            "business_name" => "What's your business name?",
            "industry" => "What industry are you in? (e.g. retail, manufacturing, services)",
            "size" => "Roughly how large is the team? (e.g. 1–5, 6–20, 20+)",
            "currency" => "Which currency should we use? (e.g. PKR, USD, EUR)",
            "country_timezone" => "Which country and timezone should we use?",
            "tax" => "What's your default tax rate? (percent, e.g. 17)",
            "warehouse" => "Do you have a main warehouse or location? What should we call it?",
            "categories" => "List a few starting product categories (comma-separated).",
            "confirm_apply" => $"I'll apply this profile: {Summarize(data)}. Shall I confirm?",
            _ => "Let's continue…"
        };

    private static string Summarize(IReadOnlyDictionary<string, string?> data)
    {
        var parts = new List<string>();
        if (data.TryGetValue("business_name", out var n) && !string.IsNullOrWhiteSpace(n)) parts.Add(n!);
        if (data.TryGetValue("industry", out var i) && !string.IsNullOrWhiteSpace(i)) parts.Add(i!);
        if (data.TryGetValue("currency", out var c) && !string.IsNullOrWhiteSpace(c)) parts.Add(c!);
        return parts.Count == 0 ? "your business settings" : string.Join(" · ", parts);
    }

    private static bool HasEnoughForProfile(IReadOnlyDictionary<string, string?> data) =>
        !string.IsNullOrWhiteSpace(data.GetValueOrDefault("business_name"))
        && !string.IsNullOrWhiteSpace(data.GetValueOrDefault("industry"))
        && !string.IsNullOrWhiteSpace(data.GetValueOrDefault("currency"));

    private async Task PersistOnboardingMemoryAsync(
        Guid sessionId,
        int step,
        Dictionary<string, string?> data,
        string agentKey,
        string language,
        CancellationToken cancellationToken)
    {
        var session = await _context.AiConversationSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session is null)
            return;

        AiMemoryStateDto existing = new();
        if (!string.IsNullOrWhiteSpace(session.MemoryJson))
        {
            try
            {
                existing = JsonSerializer.Deserialize<AiMemoryStateDto>(session.MemoryJson, JsonOptions) ?? new();
            }
            catch
            {
                existing = new();
            }
        }

        var memory = new AiMemoryStateDto
        {
            SelectedCustomerId = existing.SelectedCustomerId,
            SelectedCustomerName = existing.SelectedCustomerName,
            SelectedProjectId = existing.SelectedProjectId,
            SelectedOrderId = existing.SelectedOrderId,
            SelectedInvoiceId = existing.SelectedInvoiceId,
            LastIntent = "Onboarding",
            LastAnalyticsQuery = existing.LastAnalyticsQuery,
            RecentTurns = existing.RecentTurns,
            PreferredLanguage = language,
            PreferredAgentKey = agentKey,
            OnboardingStep = step,
            OnboardingDataJson = JsonSerializer.Serialize(data, JsonOptions),
            TonePreference = existing.TonePreference ?? "employee"
        };

        session.MemoryJson = JsonSerializer.Serialize(memory, JsonOptions);
        session.LastActivityAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SafeBeginStepAsync(Guid workflowId, string stepKey, string? message, CancellationToken ct)
    {
        try { await _workflowService.BeginStepAsync(workflowId, stepKey, message, ct); }
        catch (Exception ex) { _logger.LogDebug(ex, "BeginStep {Step} skipped", stepKey); }
    }

    private async Task SafeCompleteStepAsync(Guid workflowId, string stepKey, string? message, CancellationToken ct)
    {
        try { await _workflowService.CompleteStepAsync(workflowId, stepKey, message, ct); }
        catch (Exception ex) { _logger.LogDebug(ex, "CompleteStep {Step} skipped", stepKey); }
    }

    private static AgentOnboardingResponse BuildResponse(
        string reply,
        AgentPersonaDto persona,
        Guid sessionId,
        AgentWorkflowDto workflow,
        int currentStep,
        string stepKey,
        IReadOnlyDictionary<string, string?> data,
        bool isComplete) =>
        new()
        {
            Reply = reply,
            SpokenReply = reply,
            AgentKey = persona.Key,
            AgentDisplayName = persona.DisplayName,
            SessionId = sessionId,
            WorkflowId = workflow.Id,
            CurrentStep = currentStep,
            StepKey = stepKey,
            IsComplete = isComplete,
            CollectedData = data,
            Suggestions = isComplete
                ?
                [
                    new() { Label = "Inventory summary", Message = "Show my inventory summary" },
                    new() { Label = "Low stock", Message = "What products are low in stock?" }
                ]
                : [],
            WorkflowSteps = workflow.Steps
        };

    private static Dictionary<string, string?> ParseData(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(json, JsonOptions)
                   ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string NormalizeCurrency(string answer)
    {
        var upper = answer.Trim().ToUpperInvariant();
        if (upper.Contains("PKR") || upper.Contains("RUPEE")) return "PKR";
        if (upper.Contains("EUR") || upper.Contains("EURO")) return "EUR";
        if (upper.Contains("GBP") || upper.Contains("POUND")) return "GBP";
        if (upper.Contains("USD") || upper.Contains("DOLLAR")) return "USD";
        if (upper.Length is >= 3 and <= 5 && upper.All(char.IsLetter))
            return upper[..Math.Min(3, upper.Length)];
        return "USD";
    }

    private static string InferTimezone(string answer)
    {
        var lower = answer.ToLowerInvariant();
        if (lower.Contains("pakistan") || lower.Contains("karachi") || lower.Contains("pkr"))
            return "Asia/Karachi";
        if (lower.Contains("india") || lower.Contains("mumbai") || lower.Contains("delhi"))
            return "Asia/Kolkata";
        if (lower.Contains("uk") || lower.Contains("london") || lower.Contains("britain"))
            return "Europe/London";
        if (lower.Contains("uae") || lower.Contains("dubai"))
            return "Asia/Dubai";
        if (lower.Contains("utc"))
            return "UTC";
        return "UTC";
    }

    private static string? ExtractNumber(string answer)
    {
        var digits = new string(answer.Where(c => char.IsDigit(c) || c == '.').ToArray());
        return string.IsNullOrWhiteSpace(digits) ? null : digits;
    }

    private string RequireUserId() =>
        _currentUser.UserId ?? throw new InvalidOperationException("User context is required.");
}
