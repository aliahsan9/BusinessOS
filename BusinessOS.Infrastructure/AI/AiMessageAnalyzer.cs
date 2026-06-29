using System.Text.RegularExpressions;
using BusinessOS.Application.Features.AI.DTOs;

namespace BusinessOS.Infrastructure.AI;

internal static class AiMessageAnalyzer
{
    private static readonly string[] Greetings =
    [
        "hi", "hello", "hey", "hiya", "howdy", "greetings", "good morning",
        "good afternoon", "good evening", "sup", "yo", "what's up", "whats up"
    ];

    private static readonly string[] Thanks =
    ["thanks", "thank you", "thx", "ty", "appreciate it"];

    private static readonly string[] Farewells =
    ["bye", "goodbye", "see you", "later", "cya"];

    public static AiMessageIntent Classify(string message)
    {
        var text = message.Trim().ToLowerInvariant();
        var normalized = Regex.Replace(text, @"[^\w\s']", " ").Trim();
        normalized = Regex.Replace(normalized, @"\s+", " ");

        if (IsGreeting(normalized))
            return AiMessageIntent.Conversational;

        if (Thanks.Any(t => normalized == t || normalized.StartsWith(t + " ")))
            return AiMessageIntent.Conversational;

        if (Farewells.Any(f => normalized == f || normalized.StartsWith(f + " ")))
            return AiMessageIntent.Conversational;

        if (IsHelpQuestion(text) && !RequiresBusinessData(text))
            return AiMessageIntent.Help;

        return AiMessageIntent.BusinessQuery;
    }

    public static bool RequiresRetrieval(AiMessageIntent intent) =>
        intent is AiMessageIntent.BusinessQuery;

    private static bool IsGreeting(string normalized)
    {
        if (Greetings.Any(g => normalized == g))
            return true;

        if (normalized.Length <= 20 && Greetings.Any(g => normalized.StartsWith(g)))
            return true;

        return false;
    }

    private static bool IsHelpQuestion(string text) =>
        text.Contains("how do i", StringComparison.OrdinalIgnoreCase)
        || text.Contains("how to", StringComparison.OrdinalIgnoreCase)
        || text.Contains("help me", StringComparison.OrdinalIgnoreCase)
        || text.Contains("getting started", StringComparison.OrdinalIgnoreCase)
        || text.Contains("what is businessos", StringComparison.OrdinalIgnoreCase);

    private static bool RequiresBusinessData(string text) =>
        text.Contains("customer", StringComparison.OrdinalIgnoreCase)
        || text.Contains("invoice", StringComparison.OrdinalIgnoreCase)
        || text.Contains("order", StringComparison.OrdinalIgnoreCase)
        || text.Contains("project", StringComparison.OrdinalIgnoreCase)
        || text.Contains("revenue", StringComparison.OrdinalIgnoreCase)
        || text.Contains("overdue", StringComparison.OrdinalIgnoreCase)
        || text.Contains("unpaid", StringComparison.OrdinalIgnoreCase)
        || text.Contains("summarize", StringComparison.OrdinalIgnoreCase)
        || text.Contains("show me", StringComparison.OrdinalIgnoreCase)
        || text.Contains("list", StringComparison.OrdinalIgnoreCase);
}

internal enum AiMessageIntent
{
    Conversational,
    Help,
    BusinessQuery
}
