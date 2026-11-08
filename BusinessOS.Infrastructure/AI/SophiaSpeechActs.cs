using System.Text.RegularExpressions;

namespace BusinessOS.Infrastructure.AI;

/// <summary>
/// What the user is doing in this turn. Write tools are allowed only for <see cref="SophiaSpeechAct.Action"/>.
/// Confirmation of a pending action is handled by the stored draft, not by treating the word "yes" as a new command.
/// </summary>
public enum SophiaSpeechAct
{
    Unknown = 0,
    Question = 1,
    InformationRequest = 2,
    Analysis = 3,
    Recommendation = 4,
    Report = 5,
    Action = 6,
    Confirmation = 7,
    Correction = 8,
    Cancellation = 9,
    MissingInformation = 10
}

public static partial class SophiaSpeechActs
{
    public static SophiaSpeechAct Classify(string? message)
    {
        var content = Normalize(message);
        if (content.Length == 0)
            return SophiaSpeechAct.Unknown;

        if (IsCancellation(content))
            return SophiaSpeechAct.Cancellation;
        if (IsShortConfirmation(content))
            return SophiaSpeechAct.Confirmation;
        if (IsReport(content))
            return SophiaSpeechAct.Report;
        if (IsRecommendation(content))
            return SophiaSpeechAct.Recommendation;
        if (IsAnalysis(content))
            return SophiaSpeechAct.Analysis;
        if (IsExplicitAction(content))
            return SophiaSpeechAct.Action;
        if (IsCorrection(content))
            return SophiaSpeechAct.Correction;
        if (IsInformationRequest(content))
            return SophiaSpeechAct.InformationRequest;
        if (IsOpenQuestion(content))
            return SophiaSpeechAct.Question;

        return SophiaSpeechAct.Unknown;
    }

    /// <summary>
    /// True only when the user gave an imperative create, update, or delete.
    /// A question, recommendation, or lookup must not select a write tool.
    /// </summary>
    public static bool AllowsDatabaseWrite(string? message) =>
        Classify(message) == SophiaSpeechAct.Action;

    public static bool IsRecordUpdate(string? message)
    {
        var content = Normalize(message);
        if (content.Length == 0 || IsOpenQuestion(content))
            return false;

        if (Regex.IsMatch(content, @"\b(strategy|advice|idea|ideas|improve|management|process|tip|tips)\b")
            && !Regex.IsMatch(content, @"\b(sku|sale price|cost price|phone|email)\b"))
        {
            return false;
        }

        return Regex.IsMatch(
                content,
                @"\b(?:update|edit|modify)\s+(?:the\s+|this\s+|my\s+|a\s+)?(?:product|customer|client|supplier|company|profile|tax)\b")
            || Regex.IsMatch(
                content,
                @"\b(?:change|set)\s+(?:the\s+|my\s+)?(?:sale\s+price|cost\s+price|price|sku|phone|email|address|tax|currency)\b");
    }

    public static bool IsRecordDelete(string? message)
    {
        var content = Normalize(message);
        if (content.Length == 0 || IsOpenQuestion(content))
            return false;

        // "all" and "every" still select the delete tool so the safety gate can refuse a bulk delete.
        // Treating those phrases as ordinary catalog reads would hide the refusal.
        return Regex.IsMatch(
            content,
            @"\b(?:delete|remove)\s+(?:(?:all|every|entire|the|this|these|those|my|a|an|of)\s+)*(?:product|customer|client|supplier)s?\b");
    }

    private static bool IsExplicitAction(string content)
    {
        if (IsOpenQuestion(content))
            return false;

        if (Regex.IsMatch(content, @"\b(employee|employees|staff member|staff members|hire|hiring|headcount)\b")
            && !Regex.IsMatch(content, @"\b(product|customer|client|supplier|invoice|order|task)\b"))
        {
            return false;
        }

        if (Regex.IsMatch(
                content,
                @"\b(?:create|add|register|make)\s+(?:(?:a|an|the|my|new)\s+){0,2}(?:product|item|customer|client|supplier|vendor|invoice|task|project|sale|order)s?\b"))
        {
            return true;
        }

        if (Regex.IsMatch(
                content,
                @"^(?:please\s+)?new\s+(?:product|customer|client|supplier|invoice|task|project|sale|order)\b"))
        {
            return true;
        }

        return Regex.IsMatch(
            content,
            @"\b(?:want|need)\s+(?:a\s+|an\s+)?new\s+(?:product|customer|client|supplier|invoice|order)\b");
    }

    private static bool IsShortConfirmation(string content)
    {
        if (content.Length > 48)
            return false;

        return Regex.IsMatch(
            content,
            @"^(?:please\s+)?(?:yes|yeah|yep|ok|okay|sure|confirm|confirmed|proceed|go ahead|do it|create it|haan|han|ji|theek hai|kar do)(?:\s+(?:please|it|now|create it|create the product|create the order|create the customer|delete it|update it|save it))?$");
    }

    private static bool IsCancellation(string content)
    {
        if (content.Length > 40)
            return false;
        if (Regex.IsMatch(content, @"\b(invoice|order|product|customer|supplier)\b"))
            return false;

        return Regex.IsMatch(
            content,
            @"^(?:please\s+)?(?:cancel|stop|never mind|nevermind|abort|dont|don't|do not|nahi|nai|mat karo|rehne do)\b");
    }

    private static bool IsReport(string content) =>
        Regex.IsMatch(content, @"\b(?:sales report|inventory report|stock report|financial report|weekly report|monthly report|daily report|business report)\b")
        || (Regex.IsMatch(content, @"\b(?:report|pdf)\b")
            && Regex.IsMatch(content, @"\b(?:sales|inventory|stock|finance|financial|weekly|monthly|daily|customer)\b"));

    private static bool IsRecommendation(string content)
    {
        if (IsExplicitAction(content))
            return false;

        if (Regex.IsMatch(content, @"\b(?:how can i|how do i|how should i|how to|ways to|tips|advice|strateg)\b")
            && Regex.IsMatch(content, @"\b(?:improve|increase|boost|grow|manage|management|recommend)\b"))
        {
            return true;
        }

        return Regex.IsMatch(content, @"\b(?:what should i|should i reorder|recommend|suggestion|suggestions)\b")
            && !Regex.IsMatch(content, @"\b(?:create|add|delete|remove)\s+");
    }

    private static bool IsAnalysis(string content)
    {
        if (IsExplicitAction(content) || IsReport(content))
            return false;

        return Regex.IsMatch(content, @"\b(?:why|compare|comparison|compared|versus|vs|declin|decreas|trend|forecast|falling|dropped)\b");
    }

    private static bool IsInformationRequest(string content)
    {
        if (IsExplicitAction(content) || IsOpenQuestion(content) && Regex.IsMatch(content, @"\b(?:how can i|how do i|how to|why)\b"))
            return false;

        return Regex.IsMatch(
            content,
            @"\b(?:show|list|display|find|search|who are|which|what are|how many|how much|my products|my customers|outstanding|restock)\b");
    }

    private static bool IsCorrection(string content) =>
        !IsExplicitAction(content)
        && Regex.IsMatch(content, @"\b(?:actually|correction|instead|change the|not that|make that)\b");

    private static bool IsOpenQuestion(string content) =>
        Regex.IsMatch(content, @"^(?:how|why|what|which|when|where|who|explain|tell me)\b");

    private static string Normalize(string? message)
    {
        var content = (message ?? "").Trim().ToLowerInvariant();
        content = content.Replace('’', '\'');
        content = Regex.Replace(content, @"[^\p{L}\p{N}\s']", " ");
        return Regex.Replace(content, @"\s+", " ").Trim();
    }
}
