using System.Text.Json;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Services;

namespace BusinessOS.Infrastructure.AI;

public sealed class AiPromptBuilder : IAiPromptBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string BuildSystemPrompt() =>
        """
        You are BusinessOS AI, a professional business assistant embedded in BusinessOS.
        Answer using ONLY the business data provided in the user message context.
        Never invent customers, amounts, dates, or statuses.
        If the data is empty or missing, say you don't have that information yet.

        Style rules:
        - For greetings or small talk, reply warmly and briefly — do NOT dump raw data or JSON.
        - For business questions, give clear, confident summaries in plain language with bullet points when helpful.
        - Use currency formatting for money values.
        - Never output raw JSON unless the user explicitly asks for JSON.
        """;

    public string BuildUserPrompt(string message, AiContextDto context)
    {
        var intent = AiMessageAnalyzer.Classify(message);
        var hasData = context.Customer is not null
            || context.Orders.Count > 0
            || context.Invoices.Count > 0
            || context.Projects.Count > 0
            || context.Analytics is not null;

        if (intent is AiMessageIntent.Conversational && !hasData)
        {
            return $"""
                Current page: {context.Page.Module} ({context.Page.Url})
                User message: {message}

                Reply conversationally. Do not retrieve or mention database statistics unless asked.
                """;
        }

        var contextJson = JsonSerializer.Serialize(new
        {
            user = context.User,
            page = context.Page,
            customer = context.Customer,
            invoices = context.Invoices,
            orders = context.Orders,
            projects = context.Projects,
            analytics = context.Analytics
        }, JsonOptions);

        return $"""
            Business data context (JSON):
            {contextJson}

            User question:
            {message}
            """;
    }
}
