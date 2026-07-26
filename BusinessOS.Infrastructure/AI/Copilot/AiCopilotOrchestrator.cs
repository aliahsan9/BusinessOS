using System.Diagnostics;
using System.Text;
using System.Text.Json;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.AI.Services;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Application.Features.Tenant.Services;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.AI.Copilot;

public sealed class AiCopilotOrchestrator : IAiCopilotOrchestrator
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IFeatureFlagService _featureFlags;
    private readonly ITenantLimitService _limitService;
    private readonly IAiContextService _contextService;
    private readonly IAiIntentDetector _intentDetector;
    private readonly IAiPermissionValidator _permissionValidator;
    private readonly IAiToolRegistry _toolRegistry;
    private readonly IAiMemoryService _memoryService;
    private readonly IAiVectorRagService _ragService;
    private readonly IAiObservabilityService _observability;
    private readonly IAiInsightService _insightService;
    private readonly IAiPromptBuilder _promptBuilder;
    private readonly ILlmChatClient _llmChat;
    private readonly ILogger<AiCopilotOrchestrator> _logger;

    public AiCopilotOrchestrator(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IFeatureFlagService featureFlags,
        ITenantLimitService limitService,
        IAiContextService contextService,
        IAiIntentDetector intentDetector,
        IAiPermissionValidator permissionValidator,
        IAiToolRegistry toolRegistry,
        IAiMemoryService memoryService,
        IAiVectorRagService ragService,
        IAiObservabilityService observability,
        IAiInsightService insightService,
        IAiPromptBuilder promptBuilder,
        ILlmChatClient llmChat,
        ILogger<AiCopilotOrchestrator> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _featureFlags = featureFlags;
        _limitService = limitService;
        _contextService = contextService;
        _intentDetector = intentDetector;
        _permissionValidator = permissionValidator;
        _toolRegistry = toolRegistry;
        _memoryService = memoryService;
        _ragService = ragService;
        _observability = observability;
        _insightService = insightService;
        _promptBuilder = promptBuilder;
        _llmChat = llmChat;
        _logger = logger;
    }

    public async Task<AiCopilotChatResponse> ProcessAsync(
        AiCopilotChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await ExecutePipelineAsync(request, stream: false, cancellationToken);
        return result.Response;
    }

    public async IAsyncEnumerable<AiStreamChunkDto> ProcessStreamAsync(
        AiCopilotChatRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await ProcessAsync(request, cancellationToken);

        foreach (var word in response.Reply.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            yield return new AiStreamChunkDto { Type = "token", Content = word + " " };
            await Task.Delay(12, cancellationToken);
        }

        yield return new AiStreamChunkDto { Type = "done", FinalResponse = response };
    }

    private async Task<PipelineResult> ExecutePipelineAsync(
        AiCopilotChatRequest request,
        bool stream,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        await _featureFlags.EnsureFeatureEnabledAsync(FeatureFlags.AiAssistant, cancellationToken);
        await _limitService.EnsureWithinLimitAsync("ai", cancellationToken);

        var chatRequest = ToChatRequest(request);
        var message = request.Message.Trim();
        var page = _contextService.BuildPageContext(chatRequest);
        var sessionId = await _memoryService.GetOrCreateSessionAsync(chatRequest, request.SessionId, cancellationToken);
        var memory = await _memoryService.LoadAsync(sessionId, cancellationToken);

        var intentResult = _intentDetector.Detect(message, page, memory);
        var permission = _permissionValidator.ValidateIntent(intentResult.Intent, intentResult.SuggestedTools);

        if (!permission.Allowed)
        {
            sw.Stop();
            var denied = BuildDeniedResponse(intentResult.Intent, sessionId, permission, sw.ElapsedMilliseconds);
            await _observability.LogAsync(sessionId, intentResult.Intent, message, [], [], (int)sw.ElapsedMilliseconds, null, false, permission.DenialReason, cancellationToken);
            return new PipelineResult { Response = denied };
        }

        if (intentResult.Intent is AiCopilotIntent.DashboardInsight)
        {
            var dashboard = await _insightService.GetDashboardCopilotAsync(cancellationToken);
            sw.Stop();
            var reply = $"{dashboard.Summary}\n\n" + string.Join("\n", dashboard.Insights.Select(i => $"• {i.Title}: {i.Message}"));
            var response = await FinalizeAsync(request, chatRequest, sessionId, intentResult.Intent, message, reply, [], [], null, sw.ElapsedMilliseconds, null, stream, cancellationToken);
            return response;
        }

        // Conversational / Help: still run suggested grounding tools (e.g. sales advice with live numbers).
        var tools = intentResult.Intent is AiCopilotIntent.Conversational or AiCopilotIntent.Help
            ? _toolRegistry.SelectTools(intentResult, message, page, memory)
                .Where(t => intentResult.SuggestedTools.Contains(t.ToolName))
                .ToList()
            : _toolRegistry.SelectTools(intentResult, message, page, memory).ToList();

        var toolResults = new List<AiToolResult>();
        var toolsUsed = new List<string>();
        var citations = new List<AiCitationDto>();
        AiActionResultDto? actionResult = null;

        foreach (var tool in tools)
        {
            var toolPermission = _permissionValidator.ValidateTool(tool.ToolName);
            if (!toolPermission.Allowed)
                continue;

            var execContext = new AiCopilotExecutionContext
            {
                Request = chatRequest,
                Page = page,
                Intent = intentResult.Intent,
                SessionId = sessionId,
                Memory = memory,
                Message = message
            };

            try
            {
                var result = await tool.ExecuteAsync(execContext, cancellationToken);
                toolResults.Add(result);
                toolsUsed.Add(result.ToolName);
                citations.AddRange(result.Citations);
                actionResult ??= result.ActionResult;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Tool {Tool} failed", tool.ToolName);
            }
        }

        if (intentResult.Intent is AiCopilotIntent.DocumentSearch && !toolsUsed.Contains(AiToolName.SearchDocuments.ToString()))
        {
            var ragCitations = await _ragService.SearchAsync(message, null, 5, cancellationToken);
            citations.AddRange(ragCitations);
            toolResults.Add(new AiToolResult
            {
                ToolName = AiToolName.SearchDocuments.ToString(),
                Summary = ragCitations.Count == 0 ? "No documents found." : $"Found {ragCitations.Count} document(s).",
                Citations = ragCitations
            });
            toolsUsed.Add(AiToolName.SearchDocuments.ToString());
        }
        else if (ShouldEnrichWithRag(intentResult.Intent, message) && citations.Count == 0)
        {
            try
            {
                var ragCitations = await _ragService.SearchAsync(message, null, 3, cancellationToken);
                if (ragCitations.Count > 0)
                {
                    citations.AddRange(ragCitations);
                    toolResults.Add(new AiToolResult
                    {
                        ToolName = AiToolName.SearchDocuments.ToString(),
                        Summary = $"Related knowledge: {ragCitations.Count} document excerpt(s).",
                        Citations = ragCitations
                    });
                    toolsUsed.Add(AiToolName.SearchDocuments.ToString());
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Optional RAG enrichment skipped");
            }
        }

        if (intentResult.Intent is AiCopilotIntent.Conversational or AiCopilotIntent.Help)
        {
            var (chatReply, chatTokens) = await GenerateConversationalReplyAsync(
                message, page, intentResult.Intent, toolResults, citations, cancellationToken);
            sw.Stop();
            return await FinalizeAsync(request, chatRequest, sessionId, intentResult.Intent, message, chatReply, toolsUsed, citations, null, sw.ElapsedMilliseconds, chatTokens, stream, cancellationToken);
        }

        // Factual intents with zero usable tool output must not invent numbers.
        if (IsFactualIntent(intentResult.Intent)
            && (toolResults.Count == 0 || toolResults.All(r => string.IsNullOrWhiteSpace(r.Summary) || IsEmptyDataSummary(r.Summary))))
        {
            sw.Stop();
            var refusal = AiCopilotResponseBuilder.BuildNoDataReply(message, intentResult.Intent, citations);
            return await FinalizeAsync(request, chatRequest, sessionId, intentResult.Intent, message, refusal, toolsUsed, citations, actionResult, sw.ElapsedMilliseconds, null, stream, cancellationToken);
        }

        sw.Stop();
        var (replyText, tokenUsage, streamTokens) = await GenerateReplyAsync(message, page, memory, intentResult.Intent, toolResults, citations, stream, cancellationToken);
        return await FinalizeAsync(request, chatRequest, sessionId, intentResult.Intent, message, replyText, toolsUsed, citations, actionResult, sw.ElapsedMilliseconds, tokenUsage, stream, cancellationToken, streamTokens);
    }

    private async Task<(string Reply, int? TokenUsage)> GenerateConversationalReplyAsync(
        string message,
        AiPageContextDto page,
        AiCopilotIntent intent,
        IReadOnlyList<AiToolResult> toolResults,
        IReadOnlyList<AiCitationDto> citations,
        CancellationToken cancellationToken)
    {
        var fallback = intent is AiCopilotIntent.Help
            ? AiNaturalReplyBuilder.BuildHelpReply(message)
            : AiNaturalReplyBuilder.BuildConversationalReply(message, page);

        if (toolResults.Count > 0 && intent is AiCopilotIntent.Conversational)
        {
            var groundedFallback = AiCopilotResponseBuilder.BuildGroundedAdviceReply(message, toolResults, citations);
            if (!string.IsNullOrWhiteSpace(groundedFallback))
                fallback = groundedFallback;
        }

        if (!_llmChat.IsConfigured || _currentUser.UserId is null || _currentUser.TenantId is null)
            return (fallback, null);

        try
        {
            var systemPrompt = BuildConversationalSystemPrompt();
            var userPrompt = toolResults.Count > 0
                ? AiCopilotResponseBuilder.BuildLlmUserPrompt(message, page, new AiMemoryStateDto(), toolResults, citations)
                : message;
            var toolPayload = toolResults.Select(r => new { r.ToolName, r.Summary, r.Data }).Cast<object>().ToList();
            var (llmReply, usage) = await _llmChat.GenerateWithToolsAsync(
                _currentUser.TenantId.Value,
                _currentUser.UserId,
                systemPrompt,
                userPrompt,
                toolPayload,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(llmReply))
                return (llmReply.Trim(), usage);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Conversational LLM generation failed");
        }

        return (fallback, null);
    }

    private static bool IsFactualIntent(AiCopilotIntent intent) =>
        intent is AiCopilotIntent.Analytics
            or AiCopilotIntent.BusinessIntelligence
            or AiCopilotIntent.ActionRead
            or AiCopilotIntent.DocumentSearch
            or AiCopilotIntent.FollowUp;

    private static bool ShouldEnrichWithRag(AiCopilotIntent intent, string message)
    {
        if (intent is AiCopilotIntent.DocumentSearch)
            return false;

        return intent is AiCopilotIntent.Analytics or AiCopilotIntent.BusinessIntelligence or AiCopilotIntent.ActionRead
            || message.Contains("according to", StringComparison.OrdinalIgnoreCase)
            || message.Contains("policy", StringComparison.OrdinalIgnoreCase)
            || message.Contains("document", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEmptyDataSummary(string summary)
    {
        var lower = summary.ToLowerInvariant();
        return lower.Contains("no product sales found")
            || lower.Contains("no documents found")
            || lower.Contains("no matching")
            || lower.Contains("couldn't find")
            || lower.Contains("could not find")
            || lower.Contains("0 products sold across 0 orders");
    }

    private async Task<(string Reply, int? TokenUsage, IAsyncEnumerable<string>? Stream)> GenerateReplyAsync(
        string message,
        AiPageContextDto page,
        AiMemoryStateDto memory,
        AiCopilotIntent intent,
        IReadOnlyList<AiToolResult> toolResults,
        IReadOnlyList<AiCitationDto> citations,
        bool stream,
        CancellationToken cancellationToken)
    {
        if (toolResults.Count > 0 && toolResults.All(r => !string.IsNullOrWhiteSpace(r.Summary)))
        {
            var directReply = AiCopilotResponseBuilder.BuildFromTools(message, toolResults, citations, memory);
            if (!string.IsNullOrWhiteSpace(directReply) && (!_llmChat.IsConfigured || intent is AiCopilotIntent.ActionCreate))
                return (directReply, null, null);
        }

        if (!_llmChat.IsConfigured || _currentUser.UserId is null || _currentUser.TenantId is null)
        {
            var fallback = toolResults.Count > 0
                ? AiCopilotResponseBuilder.BuildFromTools(message, toolResults, citations, memory)
                : AiNaturalReplyBuilder.BuildBusinessReply(message, new AiContextDto { Page = page, User = BuildUserContext() });
            return (fallback, null, null);
        }

        var systemPrompt = BuildCopilotSystemPrompt();
        var userPrompt = AiCopilotResponseBuilder.BuildLlmUserPrompt(message, page, memory, toolResults, citations);

        if (stream)
        {
            var tokens = _llmChat.StreamReplyAsync(_currentUser.TenantId.Value, _currentUser.UserId, systemPrompt, userPrompt, cancellationToken);
            var sb = new StringBuilder();
            return ("", null, ReadStream(tokens, sb));
        }

        try
        {
            var toolPayload = toolResults.Select(r => new { r.ToolName, r.Summary, r.Data }).Cast<object>().ToList();
            var (llmReply, usage) = await _llmChat.GenerateWithToolsAsync(
                _currentUser.TenantId.Value,
                _currentUser.UserId,
                systemPrompt,
                userPrompt,
                toolPayload,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(llmReply))
                return (llmReply.Trim(), usage, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM generation failed");
        }

        return (AiCopilotResponseBuilder.BuildFromTools(message, toolResults, citations, memory), null, null);
    }

    private static async IAsyncEnumerable<string> ReadStream(
        IAsyncEnumerable<string> source,
        StringBuilder accumulator)
    {
        await foreach (var token in source)
        {
            accumulator.Append(token);
            yield return token;
        }
    }

    private async Task<PipelineResult> FinalizeAsync(
        AiCopilotChatRequest request,
        AiChatRequest chatRequest,
        Guid sessionId,
        AiCopilotIntent intent,
        string message,
        string reply,
        IReadOnlyList<string> toolsUsed,
        IReadOnlyList<AiCitationDto> citations,
        AiActionResultDto? actionResult,
        long executionMs,
        int? tokenUsage,
        bool stream,
        CancellationToken cancellationToken,
        IAsyncEnumerable<string>? streamTokens = null)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            await SaveConversationAsync(sessionId, message, reply, intent, toolsUsed, citations, (int)executionMs, tokenUsage, cancellationToken);
            await _memoryService.UpdateAsync(sessionId, chatRequest, intent, message, reply, cancellationToken);
            await _limitService.IncrementAiUsageAsync(cancellationToken);
        }

        await _observability.LogAsync(sessionId, intent, message, toolsUsed, citations, (int)executionMs, tokenUsage, true, null, cancellationToken);

        var response = new AiCopilotChatResponse
        {
            Reply = reply,
            SessionId = sessionId,
            Intent = intent,
            ToolsUsed = toolsUsed,
            Citations = citations,
            Suggestions = GetSuggestions(chatRequest),
            QuickActions = GetQuickActions(),
            SearchResults = await SearchAsync(request.SearchQuery, cancellationToken),
            Sources = new AiRetrievedSourcesDto
            {
                Customers = toolsUsed.Contains(nameof(AiToolName.GetCustomers)) ? 1 : 0,
                Invoices = toolsUsed.Contains(nameof(AiToolName.GetInvoices)) ? 1 : 0,
                Orders = toolsUsed.Contains(nameof(AiToolName.GetProjects)) ? 1 : 0,
                Projects = toolsUsed.Contains(nameof(AiToolName.GetTasks)) ? 1 : 0
            },
            ActionResult = actionResult,
            Diagnostics = new AiCopilotDiagnosticsDto
            {
                Intent = intent.ToString(),
                ToolsUsed = toolsUsed,
                ExecutionTimeMs = (int)executionMs,
                TokenUsage = tokenUsage,
                RetrievedDocuments = citations.Count,
                UsedLlm = _llmChat.IsConfigured
            }
        };

        return new PipelineResult { Response = response, StreamTokens = streamTokens };
    }

    private async Task SaveConversationAsync(
        Guid sessionId,
        string prompt,
        string response,
        AiCopilotIntent intent,
        IReadOnlyList<string> toolsUsed,
        IReadOnlyList<AiCitationDto> citations,
        int executionMs,
        int? tokenUsage,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        var tenantId = _currentUser.TenantId;
        if (userId is null || tenantId is null)
            return;

        _context.AIConversations.Add(new AIConversation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            UserId = userId,
            SessionId = sessionId,
            Prompt = prompt.Length > 4000 ? prompt[..4000] : prompt,
            Response = response.Length > 8000 ? response[..8000] : response,
            Intent = intent.ToString(),
            ToolsUsedJson = JsonSerializer.Serialize(toolsUsed, JsonOptions),
            CitationsJson = JsonSerializer.Serialize(citations, JsonOptions),
            ExecutionTimeMs = executionMs,
            TokenUsage = tokenUsage
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static AiCopilotChatResponse BuildDeniedResponse(
        AiCopilotIntent intent,
        Guid sessionId,
        AiPermissionCheckResult permission,
        long executionMs) =>
        new()
        {
            Reply = permission.DenialReason ?? "You don't have permission to access this information.",
            SessionId = sessionId,
            Intent = intent,
            PermissionDenied = true,
            Diagnostics = new AiCopilotDiagnosticsDto
            {
                Intent = intent.ToString(),
                ExecutionTimeMs = (int)executionMs
            }
        };

    private static AiChatRequest ToChatRequest(AiCopilotChatRequest request) =>
        new(request.Message, request.CurrentPage, request.SearchQuery, request.CustomerId, request.OrderId, request.InvoiceId, request.ProjectId);

    private AiUserContextDto BuildUserContext() =>
        new()
        {
            UserId = _currentUser.UserId ?? "unknown",
            Email = _currentUser.Email,
            Roles = _currentUser.Roles
        };

    private static string BuildConversationalSystemPrompt() =>
        """
        You are BusinessOS AI Copilot — a warm, sharp business assistant that sounds like ChatGPT:
        clear, conversational, and useful in short paragraphs or bullets.

        Grounding rules (strict):
        - If live tool results are provided, weave those exact numbers into your advice. Never invent or round inventively.
        - If tool results say there is no data, say so plainly and give general best practices without fake metrics.
        - Never invent customers, invoices, products, revenue figures, or statuses.
        - Distinguish clearly between "from your data" and "general recommendation".

        For strategy questions (increase sales, trends, product focus), give concrete next steps.
        Keep answers concise and actionable.
        """;

    private static string BuildCopilotSystemPrompt() =>
        """
        You are BusinessOS AI Copilot, an enterprise business assistant.

        Strict grounding rules:
        - Answer using ONLY the tool results, citations, and business context provided below.
        - Never invent numbers, customers, invoices, products, or statuses.
        - If tool results are empty or say no data was found, say you don't have that data yet — do not guess.
        - Prefer concise, ChatGPT-style answers with short paragraphs or bullets.
        - When document citations are provided, reference them naturally (e.g. "According to …").
        - For follow-ups, respect conversation memory context.
        - When discussing trends, label directional outlook as based on recent history, not guaranteed forecasts.
        """;

    private async Task<IReadOnlyList<AiSearchResultDto>> SearchAsync(string? searchQuery, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
            return [];

        var term = searchQuery.Trim().ToLowerInvariant();
        var results = new List<AiSearchResultDto>();

        var customers = await _context.Customers
            .Where(x => (x.FirstName + " " + x.LastName).ToLower().Contains(term) || x.Email.ToLower().Contains(term))
            .Take(5)
            .Select(x => new { x.Id, x.FirstName, x.LastName, x.Email })
            .ToListAsync(cancellationToken);

        results.AddRange(customers.Select(c => new AiSearchResultDto
        {
            Type = "Customer",
            Id = c.Id.ToString(),
            Title = $"{c.FirstName} {c.LastName}".Trim(),
            Subtitle = c.Email,
            Route = $"/customers/{c.Id}"
        }));

        return results;
    }

    private static IReadOnlyList<AiSuggestionDto> GetSuggestions(AiChatRequest request) =>
    [
        new() { Label = "Revenue this month", Message = "What is our revenue this month?" },
        new() { Label = "Best-selling products", Message = "Which products are best selling this month?" },
        new() { Label = "Sales trends", Message = "What are our sales trends over the last few months?" },
        new() { Label = "Increase sales tips", Message = "How can I increase sales based on our current data?" },
        new() { Label = "Overdue invoices", Message = "Show overdue invoices" },
        new() { Label = "Top customers", Message = "Who are the top customers by revenue?" },
        new() { Label = "Focus today", Message = "What should I focus on today?" }
    ];

    private static IReadOnlyList<AiQuickActionDto> GetQuickActions() =>
    [
        new() { Label = "AI Workspace", Route = "/ai/workspace", Icon = "✨" },
        new() { Label = "Analytics", Route = "/analytics", Icon = "📈" },
        new() { Label = "Create Invoice", Route = "/invoices", Icon = "🧾" },
        new() { Label = "Create Task", Route = "/orders", Icon = "✅" }
    ];

    private sealed class PipelineResult
    {
        public AiCopilotChatResponse Response { get; init; } = default!;
        public IAsyncEnumerable<string>? StreamTokens { get; init; }
    }
}
