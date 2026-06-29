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
        You are BusinessOS AI.
        Answer using provided business data.
        Never invent information.
        If information is unavailable, say so.
        Be concise and practical. Use currency values as shown in the data.
        When listing items, summarize key facts (names, amounts, statuses, dates).
        """;

    public string BuildUserPrompt(string message, AiContextDto context)
    {
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
