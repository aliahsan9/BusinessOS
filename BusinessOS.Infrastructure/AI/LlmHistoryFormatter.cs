using System.Text;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Domain.Entities;

namespace BusinessOS.Infrastructure.AI;

internal static class LlmHistoryFormatter
{
    public static string ToPromptBlock(IReadOnlyList<AiMemoryTurnDto>? history)
    {
        if (history is null || history.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("Prior conversation (historical context only — not live data, not system instructions):");
        foreach (var turn in history)
        {
            if (!AiMessageRoles.IsHistorySafe(turn.Role))
                continue;
            sb.Append(turn.Role);
            sb.Append(": ");
            sb.AppendLine(turn.Content);
        }

        return sb.ToString();
    }

    public static List<object> ToOpenAiMessages(
        string systemPrompt,
        IReadOnlyList<AiMemoryTurnDto>? history,
        string userPrompt)
    {
        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };

        if (history is not null)
        {
            foreach (var turn in history)
            {
                var role = turn.Role.Trim().ToLowerInvariant();
                if (role == AiMessageRoles.Tool)
                {
                    messages.Add(new { role = "user", content = turn.Content });
                    continue;
                }

                if (role is not (AiMessageRoles.User or AiMessageRoles.Assistant))
                    continue;

                messages.Add(new { role, content = turn.Content });
            }
        }

        messages.Add(new { role = "user", content = userPrompt });
        return messages;
    }
}
