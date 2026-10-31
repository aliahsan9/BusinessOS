using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Domain.Entities;

namespace BusinessOS.Application.Features.AI.ConversationMemory;

/// <summary>
/// Selects a token-budgeted slice of conversation history for the LLM.
/// Summarized messages are omitted from the recent window to avoid duplication.
/// </summary>
public static class ConversationContextBuilder
{
    public static ConversationContext Build(
        string? summary,
        int lastSummarizedSequence,
        IReadOnlyList<AiMemoryTurnDto> chronologicalMessages,
        AiMemoryOptions options)
    {
        if (!options.Enabled)
        {
            return new ConversationContext
            {
                Summary = null,
                RecentTurns = [],
                HistoryTokenEstimate = 0
            };
        }

        var unsummarized = chronologicalMessages
            .Where(m => m.SequenceNumber > lastSummarizedSequence)
            .Where(m => AiMessageRoles.IsHistorySafe(m.Role))
            .ToList();

        var selected = SelectWithinBudget(unsummarized, options);
        var summaryText = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
        var summaryTokens = ConversationTokenEstimator.EstimateTokens(summaryText);
        if (summaryTokens > options.MaxSummaryTokens && summaryText is not null)
        {
            var maxChars = Math.Max(32, options.MaxSummaryTokens * ConversationTokenEstimator.CharsPerToken);
            summaryText = summaryText.Length <= maxChars
                ? summaryText
                : summaryText[..maxChars] + "…";
            summaryTokens = ConversationTokenEstimator.EstimateTokens(summaryText);
        }

        var historyTokens = summaryTokens + selected.Sum(m =>
            ConversationTokenEstimator.EstimateMessageTokens(m.Role, m.Content));

        return new ConversationContext
        {
            Summary = summaryText,
            RecentTurns = selected,
            HistoryTokenEstimate = historyTokens
        };
    }

    /// <summary>
    /// Messages that should be rolled into the next summary (older than the retained recent window).
    /// </summary>
    public static IReadOnlyList<AiMemoryTurnDto> SelectMessagesToSummarize(
        IReadOnlyList<AiMemoryTurnDto> chronologicalMessages,
        int lastSummarizedSequence,
        AiMemoryOptions options)
    {
        var unsummarized = chronologicalMessages
            .Where(m => m.SequenceNumber > lastSummarizedSequence)
            .Where(m => AiMessageRoles.IsHistorySafe(m.Role))
            .ToList();

        if (unsummarized.Count < options.SummarizationThreshold)
            return [];

        var keep = Math.Max(2, options.RecentMessageLimit);
        if (unsummarized.Count <= keep)
            return [];

        var candidate = unsummarized.Take(unsummarized.Count - keep).ToList();
        if (candidate.Count == 0)
            return [];

        // Do not summarize a partial tool interaction (tool without a following assistant).
        while (candidate.Count > 0
               && string.Equals(candidate[^1].Role, AiMessageRoles.Tool, StringComparison.OrdinalIgnoreCase))
        {
            candidate.RemoveAt(candidate.Count - 1);
        }

        return candidate;
    }

    private static IReadOnlyList<AiMemoryTurnDto> SelectWithinBudget(
        IReadOnlyList<AiMemoryTurnDto> unsummarized,
        AiMemoryOptions options)
    {
        var fallbackLimit = Math.Max(1, options.RecentMessageLimit);
        var window = unsummarized.Count <= fallbackLimit
            ? unsummarized
            : unsummarized.Skip(unsummarized.Count - fallbackLimit).ToList();

        var budget = Math.Max(32, options.MaxHistoryTokens);
        var selected = new List<AiMemoryTurnDto>();
        var used = 0;

        for (var i = window.Count - 1; i >= 0; i--)
        {
            var message = TruncateIfNeeded(window[i], budget);
            var tokens = ConversationTokenEstimator.EstimateMessageTokens(message.Role, message.Content);
            if (selected.Count > 0 && used + tokens > budget)
                break;

            selected.Add(message);
            used += tokens;
        }

        selected.Reverse();
        return selected;
    }

    private static AiMemoryTurnDto TruncateIfNeeded(AiMemoryTurnDto message, int budget)
    {
        var maxChars = Math.Max(80, budget * ConversationTokenEstimator.CharsPerToken / 2);
        if (message.Content.Length <= maxChars)
            return message;

        return new AiMemoryTurnDto
        {
            Role = message.Role,
            Content = message.Content[..maxChars] + "…",
            SequenceNumber = message.SequenceNumber
        };
    }
}
