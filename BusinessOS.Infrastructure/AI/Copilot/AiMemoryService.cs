using System.Text.Json;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.AI.ConversationMemory;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.AI.Services;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.AI.Copilot;

public sealed class AiMemoryService : IAiMemoryService
{
    private const int MaxContentLength = 16000;
    private const int MaxListLimit = 100;
    private const int MaxMessagePage = 200;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAiContextService _contextService;
    private readonly AiMemoryOptions _memoryOptions;
    private readonly IAiConversationSummarizer? _summarizer;
    private readonly ILogger<AiMemoryService> _logger;

    public AiMemoryService(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IAiContextService contextService,
        IOptions<AiMemoryOptions> memoryOptions,
        ILogger<AiMemoryService> logger,
        IAiConversationSummarizer? summarizer = null)
    {
        _context = context;
        _currentUser = currentUser;
        _contextService = contextService;
        _memoryOptions = memoryOptions.Value;
        _logger = logger;
        _summarizer = summarizer;
    }

    public async Task<AiMemoryStateDto> LoadAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var identity = RequireIdentity();
        var session = await _context.AiConversationSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.Id == sessionId && s.UserId == identity.UserId && s.IsActive,
                cancellationToken);

        if (session is null)
            return new AiMemoryStateDto();

        var context = await BuildContextInternalAsync(session, cancellationToken);
        var stored = TryParseMemory(session.MemoryJson);

        string? customerName = stored?.SelectedCustomerName;
        if (session.SelectedCustomerId is not null && customerName is null)
        {
            customerName = await _context.Customers
                .Where(c => c.Id == session.SelectedCustomerId)
                .Select(c => (c.FirstName + " " + c.LastName).Trim())
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new AiMemoryStateDto
        {
            SelectedCustomerId = session.SelectedCustomerId ?? stored?.SelectedCustomerId,
            SelectedCustomerName = customerName,
            SelectedProjectId = session.SelectedProjectId ?? stored?.SelectedProjectId,
            SelectedOrderId = session.SelectedOrderId ?? stored?.SelectedOrderId,
            SelectedInvoiceId = session.SelectedInvoiceId ?? stored?.SelectedInvoiceId,
            LastIntent = stored?.LastIntent,
            LastAnalyticsQuery = stored?.LastAnalyticsQuery,
            RecentTurns = context.RecentTurns,
            ConversationSummary = context.Summary,
            HistoryTokenEstimate = context.HistoryTokenEstimate,
            PreferredLanguage = stored?.PreferredLanguage,
            PreferredAgentKey = stored?.PreferredAgentKey,
            OnboardingStep = stored?.OnboardingStep,
            OnboardingDataJson = stored?.OnboardingDataJson,
            TonePreference = stored?.TonePreference,
            LastClientRequestId = stored?.LastClientRequestId,
            PendingActionJson = stored?.PendingActionJson
        };
    }

    public async Task SetPendingActionAsync(
        Guid sessionId,
        string? pendingActionJson,
        CancellationToken cancellationToken = default)
    {
        var identity = RequireIdentity();
        var session = await _context.AiConversationSessions
            .FirstOrDefaultAsync(
                s => s.Id == sessionId && s.UserId == identity.UserId && s.IsActive,
                cancellationToken);
        if (session is null)
            return;

        var prior = TryParseMemory(session.MemoryJson) ?? new AiMemoryStateDto();
        var memory = new AiMemoryStateDto
        {
            SelectedCustomerId = prior.SelectedCustomerId,
            SelectedCustomerName = prior.SelectedCustomerName,
            SelectedProjectId = prior.SelectedProjectId,
            SelectedOrderId = prior.SelectedOrderId,
            SelectedInvoiceId = prior.SelectedInvoiceId,
            LastIntent = prior.LastIntent,
            LastAnalyticsQuery = prior.LastAnalyticsQuery,
            PreferredLanguage = prior.PreferredLanguage,
            PreferredAgentKey = prior.PreferredAgentKey,
            OnboardingStep = prior.OnboardingStep,
            OnboardingDataJson = prior.OnboardingDataJson,
            TonePreference = prior.TonePreference,
            LastClientRequestId = prior.LastClientRequestId,
            PendingActionJson = string.IsNullOrWhiteSpace(pendingActionJson) ? null : pendingActionJson
        };

        session.MemoryJson = JsonSerializer.Serialize(memory, JsonOptions);
        session.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<ConversationContext> GetContextAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        LoadContextAsync(conversationId, cancellationToken);

    public async Task<Guid> GetOrCreateSessionAsync(
        AiChatRequest request,
        Guid? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        var identity = RequireIdentity();

        if (sessionId is not null)
        {
            var exists = await _context.AiConversationSessions
                .AnyAsync(
                    s => s.Id == sessionId && s.UserId == identity.UserId && s.IsActive,
                    cancellationToken);
            if (exists)
                return sessionId.Value;
        }

        return await CreateSessionCoreAsync(request.Message, request, cancellationToken);
    }

    public Task<Guid> CreateSessionAsync(string? title = null, CancellationToken cancellationToken = default) =>
        CreateSessionCoreAsync(title, request: null, cancellationToken);

    public Task UpdateAsync(
        Guid sessionId,
        AiChatRequest request,
        AiCopilotIntent intent,
        string userMessage,
        string assistantReply,
        CancellationToken cancellationToken = default) =>
        CompleteTurnAsync(
            sessionId,
            request,
            intent,
            userMessage,
            assistantReply,
            cancellationToken: cancellationToken);

    public async Task CompleteTurnAsync(
        Guid sessionId,
        AiChatRequest request,
        AiCopilotIntent intent,
        string userMessage,
        string assistantReply,
        IReadOnlyList<string>? toolsUsed = null,
        IReadOnlyList<AiCitationDto>? citations = null,
        int? executionMs = null,
        int? tokenUsage = null,
        string? clientRequestId = null,
        IReadOnlyList<AiToolResult>? toolResults = null,
        CancellationToken cancellationToken = default,
        bool updatePendingAction = false,
        string? pendingActionJson = null)
    {
        var identity = RequireIdentity();
        if (string.IsNullOrWhiteSpace(userMessage) && string.IsNullOrWhiteSpace(assistantReply))
            return;

        if (!string.IsNullOrWhiteSpace(clientRequestId))
        {
            var existing = await FindTurnByClientRequestIdAsync(sessionId, clientRequestId, cancellationToken);
            if (existing is not null)
                return;
        }

        var session = await _context.AiConversationSessions
            .FirstOrDefaultAsync(
                s => s.Id == sessionId && s.UserId == identity.UserId && s.IsActive,
                cancellationToken);
        if (session is null)
            return;

        var page = _contextService.BuildPageContext(request);
        session.LastActivityAt = DateTime.UtcNow;
        session.CurrentPage = page.Url;
        session.SelectedCustomerId = page.CustomerId ?? session.SelectedCustomerId;
        session.SelectedOrderId = page.OrderId ?? session.SelectedOrderId;
        session.SelectedInvoiceId = page.InvoiceId ?? session.SelectedInvoiceId;
        session.SelectedProjectId = page.ProjectId ?? session.SelectedProjectId;
        session.UpdatedAt = DateTime.UtcNow;

        if (session.Title == "New conversation" && !string.IsNullOrWhiteSpace(userMessage))
            session.Title = ConversationTitleFormatter.FromFirstUserMessage(userMessage);

        var customerName = await ResolveCustomerNameFromMessage(userMessage, session.SelectedCustomerId, cancellationToken);
        var prior = TryParseMemory(session.MemoryJson);

        var memory = new AiMemoryStateDto
        {
            SelectedCustomerId = session.SelectedCustomerId,
            SelectedCustomerName = customerName,
            SelectedProjectId = session.SelectedProjectId,
            SelectedOrderId = session.SelectedOrderId,
            SelectedInvoiceId = session.SelectedInvoiceId,
            LastIntent = intent.ToString(),
            LastAnalyticsQuery = intent is AiCopilotIntent.Analytics ? userMessage : prior?.LastAnalyticsQuery,
            PreferredLanguage = prior?.PreferredLanguage,
            PreferredAgentKey = prior?.PreferredAgentKey,
            OnboardingStep = prior?.OnboardingStep,
            OnboardingDataJson = prior?.OnboardingDataJson,
            TonePreference = prior?.TonePreference,
            LastClientRequestId = clientRequestId ?? prior?.LastClientRequestId,
            PendingActionJson = updatePendingAction ? pendingActionJson : prior?.PendingActionJson
        };

        session.MemoryJson = JsonSerializer.Serialize(memory, JsonOptions);

        var nextSequence = await NextSequenceAsync(sessionId, cancellationToken);
        var prompt = Truncate(userMessage, 4000);
        var response = Truncate(assistantReply, 8000);

        var turn = new AIConversation
        {
            Id = Guid.NewGuid(),
            TenantId = identity.TenantId,
            UserId = identity.UserId,
            SessionId = sessionId,
            Prompt = prompt,
            Response = response,
            Intent = intent.ToString(),
            ToolsUsedJson = JsonSerializer.Serialize(toolsUsed ?? [], JsonOptions),
            CitationsJson = JsonSerializer.Serialize(citations ?? [], JsonOptions),
            ExecutionTimeMs = executionMs,
            TokenUsage = tokenUsage
        };
        _context.AIConversations.Add(turn);

        var messages = new List<AiMessage>();
        if (!string.IsNullOrWhiteSpace(userMessage))
        {
            messages.Add(CreateMessage(
                identity,
                sessionId,
                AiMessageRoles.User,
                userMessage,
                nextSequence++,
                turn.Id,
                BuildMetadata(intent.ToString(), null, clientRequestId)));
        }

        foreach (var tool in SanitizeToolResults(toolResults))
        {
            messages.Add(CreateMessage(
                identity,
                sessionId,
                AiMessageRoles.Tool,
                tool.Content,
                nextSequence++,
                turn.Id,
                BuildMetadata(intent.ToString(), tool.ToolName, null)));
        }

        if (!string.IsNullOrWhiteSpace(assistantReply))
        {
            messages.Add(CreateMessage(
                identity,
                sessionId,
                AiMessageRoles.Assistant,
                assistantReply,
                nextSequence,
                turn.Id,
                BuildMetadata(intent.ToString(), null, clientRequestId)));
        }

        _context.AiMessages.AddRange(messages);
        await _context.SaveChangesAsync(cancellationToken);

        if (_summarizer is not null)
        {
            try
            {
                await _summarizer.TrySummarizeAsync(sessionId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Conversation summarization failed for {SessionId}", sessionId);
            }
        }
    }

    public async Task AppendMessageAsync(
        Guid conversationId,
        ChatMessageInput message,
        CancellationToken cancellationToken = default)
    {
        var identity = RequireIdentity();
        if (string.IsNullOrWhiteSpace(message.Content))
            return;

        var role = message.Role.Trim().ToLowerInvariant();
        if (!AiMessageRoles.IsKnown(role) || role == AiMessageRoles.System)
            throw new InvalidOperationException("Unsupported or privileged message role.");

        var session = await _context.AiConversationSessions
            .FirstOrDefaultAsync(
                s => s.Id == conversationId && s.UserId == identity.UserId && s.IsActive,
                cancellationToken)
            ?? throw new InvalidOperationException("Conversation was not found.");

        var sequence = await NextSequenceAsync(conversationId, cancellationToken);
        _context.AiMessages.Add(CreateMessage(
            identity,
            conversationId,
            role,
            message.Content,
            sequence,
            null,
            message.MetadataJson));

        session.LastActivityAt = DateTime.UtcNow;
        session.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateSummaryAsync(
        Guid conversationId,
        string summary,
        int summarizedThroughSequence,
        CancellationToken cancellationToken = default)
    {
        var identity = RequireIdentity();
        var session = await _context.AiConversationSessions
            .FirstOrDefaultAsync(
                s => s.Id == conversationId && s.UserId == identity.UserId && s.IsActive,
                cancellationToken);
        if (session is null)
            return;

        session.Summary = Truncate(summary, 8000);
        session.LastSummarizedAt = DateTime.UtcNow;
        session.LastSummarizedMessageSequence = summarizedThroughSequence;
        session.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<AiConversationTurnLookupDto?> FindTurnByClientRequestIdAsync(
        Guid sessionId,
        string clientRequestId,
        CancellationToken cancellationToken = default)
    {
        var identity = RequireIdentity();
        if (string.IsNullOrWhiteSpace(clientRequestId))
            return null;

        var owned = await _context.AiConversationSessions
            .AsNoTracking()
            .AnyAsync(s => s.Id == sessionId && s.UserId == identity.UserId, cancellationToken);
        if (!owned)
            return null;

        var needle = "\"clientRequestId\":\"" + clientRequestId.Replace("\"", "") + "\"";
        var matches = await _context.AiMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == sessionId
                        && m.UserId == identity.UserId
                        && m.MetadataJson != null
                        && m.MetadataJson.Contains(clientRequestId))
            .OrderBy(m => m.SequenceNumber)
            .ToListAsync(cancellationToken);

        var user = matches.FirstOrDefault(m =>
            m.Role == AiMessageRoles.User && m.MetadataJson!.Contains(needle, StringComparison.Ordinal));
        var assistant = matches.LastOrDefault(m =>
            m.Role == AiMessageRoles.Assistant && m.MetadataJson != null
            && m.MetadataJson.Contains(clientRequestId));

        if (user is null || assistant is null)
            return null;

        return new AiConversationTurnLookupDto
        {
            UserMessage = user.Content,
            AssistantReply = assistant.Content
        };
    }

    public async Task<AiConversationSessionDto?> GetSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var identity = RequireIdentity();
        return await _context.AiConversationSessions
            .AsNoTracking()
            .Where(s => s.Id == sessionId && s.UserId == identity.UserId && s.IsActive)
            .Select(s => new AiConversationSessionDto
            {
                Id = s.Id,
                Title = s.Title,
                LastActivityAt = s.LastActivityAt,
                MessageCount = s.Messages.Count,
                Summary = s.Summary
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<IReadOnlyList<AiConversationSessionDto>> ListSessionsAsync(
        int limit,
        CancellationToken cancellationToken = default) =>
        ListSessionsAsync(0, limit, cancellationToken);

    public async Task<IReadOnlyList<AiConversationSessionDto>> ListSessionsAsync(
        int skip,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var identity = RequireIdentity();
        var take = Math.Clamp(limit <= 0 ? 20 : limit, 1, MaxListLimit);
        var offset = Math.Max(0, skip);

        return await _context.AiConversationSessions
            .AsNoTracking()
            .Where(s => s.UserId == identity.UserId && s.IsActive)
            .OrderByDescending(s => s.LastActivityAt)
            .Skip(offset)
            .Take(take)
            .Select(s => new AiConversationSessionDto
            {
                Id = s.Id,
                Title = s.Title,
                LastActivityAt = s.LastActivityAt,
                MessageCount = s.Messages.Count,
                Summary = s.Summary
            })
            .ToListAsync(cancellationToken);
    }

    public Task<IReadOnlyList<AiConversationMessageDto>> GetSessionMessagesAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default) =>
        GetSessionMessagesAsync(sessionId, 0, MaxMessagePage, cancellationToken);

    public async Task<IReadOnlyList<AiConversationMessageDto>> GetSessionMessagesAsync(
        Guid sessionId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var identity = RequireIdentity();
        var owned = await _context.AiConversationSessions
            .AsNoTracking()
            .AnyAsync(s => s.Id == sessionId && s.UserId == identity.UserId, cancellationToken);
        if (!owned)
            return [];

        var page = Math.Clamp(take <= 0 ? 50 : take, 1, MaxMessagePage);
        var offset = Math.Max(0, skip);

        var messages = await _context.AIConversations
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId && m.UserId == identity.UserId)
            .OrderBy(m => m.CreatedAt)
            .Skip(offset)
            .Take(page)
            .ToListAsync(cancellationToken);

        return messages.Select(m => new AiConversationMessageDto
        {
            Id = m.Id,
            Prompt = m.Prompt,
            Response = m.Response,
            Intent = m.Intent,
            ToolsUsed = ParseJsonList(m.ToolsUsedJson),
            Citations = ParseCitations(m.CitationsJson),
            CreatedAt = m.CreatedAt
        }).ToList();
    }

    public async Task<bool> DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var identity = RequireIdentity();
        var session = await _context.AiConversationSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == identity.UserId, cancellationToken);

        if (session is null)
            return false;

        session.IsActive = false;
        session.IsDeleted = true;
        session.LastActivityAt = DateTime.UtcNow;
        session.UpdatedAt = DateTime.UtcNow;

        var messages = await _context.AiMessages
            .Where(m => m.ConversationId == sessionId && m.UserId == identity.UserId)
            .ToListAsync(cancellationToken);
        foreach (var message in messages)
        {
            message.IsDeleted = true;
            message.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    internal async Task<IReadOnlyList<AiMemoryTurnDto>> LoadChronologicalMessagesAsync(
        Guid conversationId,
        string userId,
        CancellationToken cancellationToken)
    {
        var stored = await _context.AiMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.UserId == userId)
            .OrderBy(m => m.SequenceNumber)
            .Select(m => new AiMemoryTurnDto
            {
                Role = m.Role,
                Content = m.Content,
                SequenceNumber = m.SequenceNumber
            })
            .ToListAsync(cancellationToken);

        if (stored.Count > 0)
            return stored;

        var turns = await _context.AIConversations
            .AsNoTracking()
            .Where(m => m.SessionId == conversationId && m.UserId == userId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        var fallback = new List<AiMemoryTurnDto>();
        var sequence = 1;
        foreach (var turn in turns)
        {
            fallback.Add(new AiMemoryTurnDto { Role = AiMessageRoles.User, Content = turn.Prompt, SequenceNumber = sequence++ });
            fallback.Add(new AiMemoryTurnDto { Role = AiMessageRoles.Assistant, Content = turn.Response, SequenceNumber = sequence++ });
        }

        return fallback;
    }

    private async Task<ConversationContext> LoadContextAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        var identity = RequireIdentity();
        var session = await _context.AiConversationSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.Id == conversationId && s.UserId == identity.UserId && s.IsActive,
                cancellationToken);

        if (session is null)
            return new ConversationContext();

        return await BuildContextInternalAsync(session, cancellationToken);
    }

    private async Task<ConversationContext> BuildContextInternalAsync(
        AiConversationSession session,
        CancellationToken cancellationToken)
    {
        var messages = await LoadChronologicalMessagesAsync(session.Id, session.UserId, cancellationToken);
        return ConversationContextBuilder.Build(
            session.Summary,
            session.LastSummarizedMessageSequence,
            messages,
            _memoryOptions);
    }

    private async Task<Guid> CreateSessionCoreAsync(
        string? titleSource,
        AiChatRequest? request,
        CancellationToken cancellationToken)
    {
        var identity = RequireIdentity();
        var page = request is null ? new AiPageContextDto() : _contextService.BuildPageContext(request);
        var session = new AiConversationSession
        {
            Id = Guid.NewGuid(),
            TenantId = identity.TenantId,
            UserId = identity.UserId,
            Title = string.IsNullOrWhiteSpace(titleSource)
                ? "New conversation"
                : ConversationTitleFormatter.FromFirstUserMessage(titleSource),
            CurrentPage = page.Url,
            SelectedCustomerId = page.CustomerId,
            SelectedOrderId = page.OrderId,
            SelectedInvoiceId = page.InvoiceId,
            SelectedProjectId = page.ProjectId,
            LastActivityAt = DateTime.UtcNow
        };

        _context.AiConversationSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);
        return session.Id;
    }

    private async Task<int> NextSequenceAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        var max = await _context.AiMessages
            .Where(m => m.ConversationId == conversationId)
            .Select(m => (int?)m.SequenceNumber)
            .MaxAsync(cancellationToken);
        return (max ?? 0) + 1;
    }

    private static AiMessage CreateMessage(
        (Guid TenantId, string UserId) identity,
        Guid conversationId,
        string role,
        string content,
        int sequence,
        Guid? turnId,
        string? metadataJson) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = identity.TenantId,
            UserId = identity.UserId,
            ConversationId = conversationId,
            Role = role,
            Content = Truncate(content, MaxContentLength),
            SequenceNumber = sequence,
            TokenCount = ConversationTokenEstimator.EstimateMessageTokens(role, content),
            MetadataJson = metadataJson,
            TurnId = turnId
        };

    private static string? BuildMetadata(string? intent, string? toolName, string? clientRequestId)
    {
        var payload = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(intent))
            payload["intent"] = intent;
        if (!string.IsNullOrWhiteSpace(toolName))
            payload["toolName"] = toolName;
        if (!string.IsNullOrWhiteSpace(clientRequestId))
            payload["clientRequestId"] = clientRequestId;
        return payload.Count == 0 ? null : JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static IReadOnlyList<(string ToolName, string Content)> SanitizeToolResults(IReadOnlyList<AiToolResult>? toolResults)
    {
        if (toolResults is null || toolResults.Count == 0)
            return [];

        return toolResults
            .Where(r => !string.IsNullOrWhiteSpace(r.Summary))
            .Select(r => (r.ToolName, Content: Truncate($"Historical tool result ({r.ToolName}; not live data): {r.Summary}", 2000)))
            .ToList();
    }

    private (Guid TenantId, string UserId) RequireIdentity()
    {
        var userId = _currentUser.UserId ?? throw new InvalidOperationException("User is required.");
        var tenantId = _currentUser.TenantId ?? throw new InvalidOperationException("Tenant is required.");
        return (tenantId, userId);
    }

    private static AiMemoryStateDto? TryParseMemory(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<AiMemoryStateDto>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> ResolveCustomerNameFromMessage(
        string message,
        Guid? currentCustomerId,
        CancellationToken cancellationToken)
    {
        if (currentCustomerId is not null)
        {
            return await _context.Customers
                .Where(c => c.Id == currentCustomerId)
                .Select(c => (c.FirstName + " " + c.LastName).Trim())
                .FirstOrDefaultAsync(cancellationToken);
        }

        var term = message.Trim();
        if (term.Length < 2)
            return null;

        return await _context.Customers
            .Where(c => (c.FirstName + " " + c.LastName).Contains(term) || c.Email.Contains(term))
            .Select(c => new { Name = (c.FirstName + " " + c.LastName).Trim(), c.Id })
            .OrderBy(c => c.Name)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static IReadOnlyList<string> ParseJsonList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyList<AiCitationDto> ParseCitations(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<AiCitationDto>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";
}
