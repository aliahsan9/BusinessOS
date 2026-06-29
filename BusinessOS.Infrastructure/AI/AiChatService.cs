using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Services;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Application.Features.Tenant.Services;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.AI;

public sealed class AiChatService : IAiChatService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFeatureFlagService _featureFlags;
    private readonly ITenantLimitService _limitService;
    private readonly IAiContextService _contextService;
    private readonly IAiRetrievalService _retrievalService;
    private readonly IAiActionService _actionService;
    private readonly IAiPromptBuilder _promptBuilder;
    private readonly ILlmChatClient _llmChat;
    private readonly ILogger<AiChatService> _logger;

    public AiChatService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IFeatureFlagService featureFlags,
        ITenantLimitService limitService,
        IAiContextService contextService,
        IAiRetrievalService retrievalService,
        IAiActionService actionService,
        IAiPromptBuilder promptBuilder,
        ILlmChatClient llmChat,
        ILogger<AiChatService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _featureFlags = featureFlags;
        _limitService = limitService;
        _contextService = contextService;
        _retrievalService = retrievalService;
        _actionService = actionService;
        _promptBuilder = promptBuilder;
        _llmChat = llmChat;
        _logger = logger;
    }

    public async Task<AiChatResponse> ChatAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default)
    {
        await _featureFlags.EnsureFeatureEnabledAsync(FeatureFlags.AiAssistant, cancellationToken);
        await _limitService.EnsureWithinLimitAsync("ai", cancellationToken);

        var message = request.Message.Trim();
        var page = _contextService.BuildPageContext(request);

        var searchQuery = request.SearchQuery?.Trim();
        var searchResults = string.IsNullOrWhiteSpace(searchQuery)
            ? []
            : await SearchAsync(searchQuery, cancellationToken);

        AiActionResultDto? actionResult = null;
        if (!string.IsNullOrWhiteSpace(message))
        {
            actionResult = await _actionService.TryExecuteAsync(message, page, cancellationToken);
        }

        AiContextDto context;
        string reply;

        if (actionResult is { Success: true })
        {
            context = new AiContextDto
            {
                User = new AiUserContextDto
                {
                    UserId = _currentUserService.UserId ?? "unknown",
                    Email = _currentUserService.Email,
                    Roles = _currentUserService.Roles
                },
                Page = page
            };
            reply = actionResult.Message;
            if (actionResult.Route is not null)
            {
                reply += $" Open: {actionResult.Route}";
            }
        }
        else if (!string.IsNullOrWhiteSpace(message))
        {
            var intent = AiMessageAnalyzer.Classify(message);
            if (AiMessageAnalyzer.RequiresRetrieval(intent))
            {
                context = await _retrievalService.RetrieveAsync(request, cancellationToken);
            }
            else
            {
                context = new AiContextDto
                {
                    User = new AiUserContextDto
                    {
                        UserId = _currentUserService.UserId ?? "unknown",
                        Email = _currentUserService.Email,
                        Roles = _currentUserService.Roles
                    },
                    Page = page
                };
            }

            reply = await ResolveReplyAsync(message, context, intent, actionResult, cancellationToken);
        }
        else
        {
            context = new AiContextDto
            {
                User = new AiUserContextDto { UserId = _currentUserService.UserId ?? "unknown" },
                Page = page
            };
            reply = string.Empty;
        }

        var sources = _retrievalService.BuildSources(context);
        var suggestions = GetContextSuggestions(page);
        var quickActions = GetQuickActions();

        if (!string.IsNullOrWhiteSpace(message))
        {
            await SaveConversationAsync(message, reply, cancellationToken);
            await _limitService.IncrementAiUsageAsync(cancellationToken);
        }

        return new AiChatResponse
        {
            Reply = reply,
            Suggestions = suggestions,
            QuickActions = quickActions,
            SearchResults = searchResults,
            Sources = sources,
            ActionResult = actionResult
        };
    }

    private async Task<string> ResolveReplyAsync(
        string message,
        AiContextDto context,
        AiMessageIntent intent,
        AiActionResultDto? failedAction,
        CancellationToken cancellationToken)
    {
        if (failedAction is { Success: false })
        {
            return failedAction.Message;
        }

        if (intent is AiMessageIntent.Conversational)
        {
            return AiNaturalReplyBuilder.BuildConversationalReply(message, context.Page);
        }

        if (intent is AiMessageIntent.Help)
        {
            return AiNaturalReplyBuilder.BuildHelpReply(message);
        }

        if (_llmChat.IsConfigured
            && _currentUserService.UserId is not null
            && _currentUserService.TenantId is not null)
        {
            try
            {
                var systemPrompt = _promptBuilder.BuildSystemPrompt();
                var userPrompt = _promptBuilder.BuildUserPrompt(message, context);

                var llmReply = await _llmChat.GenerateReplyAsync(
                    _currentUserService.TenantId.Value,
                    _currentUserService.UserId,
                    systemPrompt,
                    userPrompt,
                    cancellationToken);

                if (!string.IsNullOrWhiteSpace(llmReply))
                {
                    return llmReply.Trim();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LLM request failed; falling back to natural language reply");
            }
        }

        return AiNaturalReplyBuilder.BuildBusinessReply(message, context);
    }

    private async Task SaveConversationAsync(
        string prompt,
        string response,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var tenantId = _currentUserService.TenantId;

            if (userId is null || tenantId is null)
                return;

            _context.AIConversations.Add(new AIConversation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value,
                UserId = userId,
                Prompt = prompt.Length > 4000 ? prompt[..4000] : prompt,
                Response = response.Length > 8000 ? response[..8000] : response
            });

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist AI conversation");
        }
    }

    private async Task<IReadOnlyList<AiSearchResultDto>> SearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var term = query.ToLowerInvariant();
        var results = new List<AiSearchResultDto>();

        var customers = await _context.Customers
            .Where(x =>
                (x.FirstName + " " + x.LastName).ToLower().Contains(term)
                || x.Email.ToLower().Contains(term))
            .OrderBy(x => x.FirstName)
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

        var orders = await _context.Orders
            .Where(x => x.OrderNumber.ToLower().Contains(term))
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
            .Select(x => new { x.Id, x.OrderNumber, x.Status })
            .ToListAsync(cancellationToken);

        results.AddRange(orders.Select(o => new AiSearchResultDto
        {
            Type = "Project",
            Id = o.Id.ToString(),
            Title = o.OrderNumber,
            Subtitle = o.Status.ToString(),
            Route = $"/orders/{o.Id}"
        }));

        var invoices = await _context.Invoices
            .Where(x => x.InvoiceNumber.ToLower().Contains(term))
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
            .Select(x => new { x.Id, x.InvoiceNumber, x.Status })
            .ToListAsync(cancellationToken);

        results.AddRange(invoices.Select(i => new AiSearchResultDto
        {
            Type = "Invoice",
            Id = i.Id.ToString(),
            Title = i.InvoiceNumber,
            Subtitle = i.Status.ToString(),
            Route = $"/invoices/{i.Id}"
        }));

        return results.Take(15).ToList();
    }

    private static IReadOnlyList<AiSuggestionDto> GetContextSuggestions(AiPageContextDto page)
    {
        var module = page.Module;

        if (module is "customers" || page.Url.Contains("customer", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new() { Label = "Summarize this customer", Message = "Summarize this customer" },
                new() { Label = "Show customer revenue", Message = "Show customer revenue" },
                new() { Label = "Show unpaid invoices", Message = "Show unpaid invoices for this customer" }
            ];
        }

        if (module is "orders" || page.Url.Contains("order", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new() { Label = "Project progress", Message = "What is the project progress?" },
                new() { Label = "Delayed tasks", Message = "Show delayed tasks" },
                new() { Label = "Team workload", Message = "Show team workload" }
            ];
        }

        if (module is "invoices" || page.Url.Contains("invoice", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new() { Label = "Outstanding invoices", Message = "Show overdue invoices" },
                new() { Label = "Revenue this month", Message = "Which customers generated highest revenue?" }
            ];
        }

        return
        [
            new() { Label = "Getting started", Message = "Help me get started with BusinessOS" },
            new() { Label = "Create customer", Message = "Create customer" },
            new() { Label = "Outstanding invoices", Message = "Show overdue invoices" }
        ];
    }

    private static IReadOnlyList<AiQuickActionDto> GetQuickActions() =>
    [
        new() { Label = "Create Customer", Route = "/customers/new", Icon = "🤝" },
        new() { Label = "Create Project", Route = "/orders/new", Icon = "📋" },
        new() { Label = "Create Task", Route = "/orders/new", Icon = "✅" },
        new() { Label = "Create Invoice", Route = "/invoices", Icon = "🧾" },
        new() { Label = "Open Analytics", Route = "/analytics", Icon = "📈" }
    ];
}
