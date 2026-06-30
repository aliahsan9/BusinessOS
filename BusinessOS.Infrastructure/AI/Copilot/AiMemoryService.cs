using System.Text.Json;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.AI.Services;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.AI.Copilot;

public sealed class AiMemoryService : IAiMemoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAiContextService _contextService;

    public AiMemoryService(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IAiContextService contextService)
    {
        _context = context;
        _currentUser = currentUser;
        _contextService = contextService;
    }

    public async Task<AiMemoryStateDto> LoadAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _context.AiConversationSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session is null)
            return new AiMemoryStateDto();

        var recentMessages = await _context.AIConversations
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(6)
            .ToListAsync(cancellationToken);

        var turns = recentMessages
            .OrderBy(m => m.CreatedAt)
            .SelectMany(m => new[]
            {
                new AiMemoryTurnDto { Role = "user", Content = m.Prompt },
                new AiMemoryTurnDto { Role = "assistant", Content = m.Response }
            })
            .ToList();

        AiMemoryStateDto? stored = null;
        if (!string.IsNullOrWhiteSpace(session.MemoryJson))
        {
            stored = JsonSerializer.Deserialize<AiMemoryStateDto>(session.MemoryJson, JsonOptions);
        }

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
            RecentTurns = turns
        };
    }

    public async Task<Guid> GetOrCreateSessionAsync(
        AiChatRequest request,
        Guid? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId ?? throw new InvalidOperationException("User is required.");
        var tenantId = _currentUser.TenantId ?? throw new InvalidOperationException("Tenant is required.");

        if (sessionId is not null)
        {
            var exists = await _context.AiConversationSessions
                .AnyAsync(s => s.Id == sessionId && s.UserId == userId, cancellationToken);
            if (exists)
                return sessionId.Value;
        }

        var page = _contextService.BuildPageContext(request);
        var session = new AiConversationSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            Title = Truncate(request.Message, 60),
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

    public async Task UpdateAsync(
        Guid sessionId,
        AiChatRequest request,
        AiCopilotIntent intent,
        string userMessage,
        string assistantReply,
        CancellationToken cancellationToken = default)
    {
        var session = await _context.AiConversationSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session is null)
            return;

        var page = _contextService.BuildPageContext(request);
        session.LastActivityAt = DateTime.UtcNow;
        session.CurrentPage = page.Url;
        session.SelectedCustomerId = page.CustomerId ?? session.SelectedCustomerId;
        session.SelectedOrderId = page.OrderId ?? session.SelectedOrderId;
        session.SelectedInvoiceId = page.InvoiceId ?? session.SelectedInvoiceId;
        session.SelectedProjectId = page.ProjectId ?? session.SelectedProjectId;

        var customerName = await ResolveCustomerNameFromMessage(userMessage, session.SelectedCustomerId, cancellationToken);

        var memory = new AiMemoryStateDto
        {
            SelectedCustomerId = session.SelectedCustomerId,
            SelectedCustomerName = customerName,
            SelectedProjectId = session.SelectedProjectId,
            SelectedOrderId = session.SelectedOrderId,
            SelectedInvoiceId = session.SelectedInvoiceId,
            LastIntent = intent.ToString(),
            LastAnalyticsQuery = intent is AiCopilotIntent.Analytics ? userMessage : null
        };

        session.MemoryJson = JsonSerializer.Serialize(memory, JsonOptions);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AiConversationSessionDto>> ListSessionsAsync(int limit, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId ?? throw new InvalidOperationException("User is required.");

        return await _context.AiConversationSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.IsActive)
            .OrderByDescending(s => s.LastActivityAt)
            .Take(limit)
            .Select(s => new AiConversationSessionDto
            {
                Id = s.Id,
                Title = s.Title,
                LastActivityAt = s.LastActivityAt,
                MessageCount = s.Messages.Count
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AiConversationMessageDto>> GetSessionMessagesAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId ?? throw new InvalidOperationException("User is required.");

        var messages = await _context.AIConversations
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId && m.UserId == userId)
            .OrderBy(m => m.CreatedAt)
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
