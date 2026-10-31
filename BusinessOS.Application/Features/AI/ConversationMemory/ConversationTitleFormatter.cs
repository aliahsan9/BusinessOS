using System.Globalization;
using System.Text.RegularExpressions;

namespace BusinessOS.Application.Features.AI.ConversationMemory;

/// <summary>
/// Deterministic conversation titles from the first user message. Avoids an extra LLM call.
/// </summary>
public static partial class ConversationTitleFormatter
{
    private static readonly string[] Prefixes =
    [
        "can you please", "could you please", "would you please",
        "can you", "could you", "would you", "please", "hey sophia",
        "hi sophia", "hello sophia", "sophia", "show me", "tell me",
        "i want you to", "i need you to", "help me"
    ];

    public static string FromFirstUserMessage(string? message, int maxLength = 60)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "New conversation";

        var text = WhitespaceRegex().Replace(message.Trim(), " ");
        var lower = text.ToLowerInvariant();

        foreach (var prefix in Prefixes.OrderByDescending(p => p.Length))
        {
            if (lower.StartsWith(prefix + " ", StringComparison.Ordinal)
                || lower.Equals(prefix, StringComparison.Ordinal))
            {
                text = text[prefix.Length..].TrimStart(' ', ',', ':', '-');
                break;
            }
        }

        text = text.TrimEnd('?', '.', '!');
        if (string.IsNullOrWhiteSpace(text))
            text = message.Trim();

        var titled = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text.ToLowerInvariant());
        if (titled.Length <= maxLength)
            return titled;

        var cut = titled[..maxLength].TrimEnd();
        var lastSpace = cut.LastIndexOf(' ');
        if (lastSpace >= 24)
            cut = cut[..lastSpace];

        return cut + "…";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
