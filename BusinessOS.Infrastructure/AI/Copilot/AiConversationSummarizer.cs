using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.AI.ConversationMemory;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Services;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.AI.Copilot;

public sealed class AiConversationSummarizer : IAiConversationSummarizer
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILlmChatClient _llm;
    private readonly AiMemoryOptions _options;
    private readonly ILogger<AiConversationSummarizer> _logger;

    public AiConversationSummarizer(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        ILlmChatClient llm,
        IOptions<AiMemoryOptions> options,
        ILogger<AiConversationSummarizer> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _llm = llm;
        _options = options.Value;
        _logger = logger;
    }

    public async Task TrySummarizeAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || !_options.SummarizationEnabled || !_llm.IsConfigured)
            return;

        var userId = _currentUser.UserId;
        var tenantId = _currentUser.TenantId;
        if (userId is null || tenantId is null)
            return;

        var session = await _context.AiConversationSessions
            .FirstOrDefaultAsync(
                s => s.Id == conversationId && s.UserId == userId && s.IsActive,
                cancellationToken);
        if (session is null)
            return;

        var messages = await _context.AiMessages
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

        var toSummarize = ConversationContextBuilder.SelectMessagesToSummarize(
            messages,
            session.LastSummarizedMessageSequence,
            _options);

        if (toSummarize.Count == 0)
            return;

        if (!string.Equals(toSummarize[^1].Role, AiMessageRoles.Assistant, StringComparison.OrdinalIgnoreCase))
            return;

        var prompt = BuildSummarizationPrompt(session.Summary, toSummarize);
        string? summary;
        try
        {
            summary = await _llm.GenerateReplyAsync(
                tenantId.Value,
                userId,
                SummarizerSystemPrompt,
                prompt,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM summarization request failed for conversation {ConversationId}", conversationId);
            return;
        }

        if (string.IsNullOrWhiteSpace(summary))
            return;

        var maxChars = Math.Max(200, _options.MaxSummaryTokens * ConversationTokenEstimator.CharsPerToken);
        var trimmed = summary.Trim();
        if (trimmed.Length > maxChars)
            trimmed = trimmed[..maxChars] + "…";

        session.Summary = trimmed;
        session.LastSummarizedAt = DateTime.UtcNow;
        session.LastSummarizedMessageSequence = toSummarize[^1].SequenceNumber;
        session.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static string BuildSummarizationPrompt(string? existingSummary, IReadOnlyList<AiMemoryTurnDto> turns)
    {
        var prior = string.IsNullOrWhiteSpace(existingSummary)
            ? "(none)"
            : existingSummary.Trim();

        var body = string.Join(
            "\n",
            turns.Select(t => $"{t.Role}: {Truncate(t.Content, 800)}"));

        return $"""
            Existing summary:
            {prior}

            Additional conversation turns to fold in (do not repeat already-summarized content):
            {body}

            Write an updated rolling summary.
            """;
    }

    private const string SummarizerSystemPrompt = """
        You summarize BusinessOS conversations for later context.
        Preserve important entities, product names, unresolved questions, user decisions, and key numbers.
        Distinguish historical discussion from current live business data.
        Do not invent facts. Do not include secrets, credentials, or authorization material.
        Keep the summary concise and in third person.
        """;

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";
}
