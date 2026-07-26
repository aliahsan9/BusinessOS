using System.Text;
using System.Text.Json;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;

namespace BusinessOS.Infrastructure.AI.Copilot;

public static class AiCopilotResponseBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string BuildFromTools(
        string message,
        IReadOnlyList<AiToolResult> toolResults,
        IReadOnlyList<AiCitationDto> citations,
        AiMemoryStateDto memory)
    {
        if (toolResults.Count == 1 && !string.IsNullOrWhiteSpace(toolResults[0].Summary))
            return AppendCitations(toolResults[0].Summary, citations);

        var sb = new StringBuilder();
        foreach (var result in toolResults.Where(r => !string.IsNullOrWhiteSpace(r.Summary)))
        {
            sb.AppendLine(result.Summary);
        }

        if (memory.SelectedCustomerName is not null && message.Contains("customer", StringComparison.OrdinalIgnoreCase))
            sb.AppendLine($"(Context: {memory.SelectedCustomerName})");

        return AppendCitations(sb.ToString().Trim(), citations);
    }

    public static string BuildNoDataReply(
        string message,
        AiCopilotIntent intent,
        IReadOnlyList<AiCitationDto> citations)
    {
        var topic = intent switch
        {
            AiCopilotIntent.DocumentSearch => "matching documents in your knowledge base",
            AiCopilotIntent.Analytics => "sales or revenue figures for that period",
            AiCopilotIntent.ActionRead => "matching catalog or product records",
            _ => "matching business records"
        };

        var reply =
            $"""
            I checked your live BusinessOS data for “{Truncate(message, 120)}” and couldn't find {topic} to answer from.

            I won't invent numbers or details. Try:
            • Asking for a different period (this month / last month / this year)
            • Creating orders, invoices, or products so there is data to analyze
            • Uploading documents if you're looking for policy or handbook answers
            """;

        return AppendCitations(reply.Trim(), citations);
    }

    public static string BuildGroundedAdviceReply(
        string message,
        IReadOnlyList<AiToolResult> toolResults,
        IReadOnlyList<AiCitationDto> citations)
    {
        var dataBlock = string.Join("\n\n", toolResults
            .Where(r => !string.IsNullOrWhiteSpace(r.Summary))
            .Select(r => r.Summary));

        if (string.IsNullOrWhiteSpace(dataBlock))
            return string.Empty;

        var reply =
            $"""
            Here's advice grounded in your live data:

            {dataBlock}

            Practical next steps:
            1. Double down on your current bestsellers — feature them first in quotes and storefront.
            2. Follow up open quotes and overdue invoices the same day — cash and conversion often beat new ads.
            3. Bundle a top seller with an accessory or service to raise average order value.
            4. Re-engage quiet customers who bought bestsellers before with a short personal offer.
            5. Review the trend lines weekly and cut spend on SKUs that aren't moving.

            Ask a follow-up like “Which products are best selling this month?” or “Show overdue invoices” if you want a deeper cut.
            """;

        return AppendCitations(reply.Trim(), citations);
    }

    public static string BuildLlmUserPrompt(
        string message,
        AiPageContextDto page,
        AiMemoryStateDto memory,
        IReadOnlyList<AiToolResult> toolResults,
        IReadOnlyList<AiCitationDto> citations)
    {
        var payload = new
        {
            page,
            memory,
            toolResults = toolResults.Select(r => new { r.ToolName, r.Summary, r.Data }),
            citations,
            question = message,
            groundingRules = new[]
            {
                "Use only toolResults and citations for facts and numbers.",
                "If data is missing, say so — never invent metrics.",
                "Separate live-data findings from general recommendations."
            }
        };

        return $"""
            Business copilot context:
            {JsonSerializer.Serialize(payload, JsonOptions)}

            User question:
            {message}
            """;
    }

    private static string AppendCitations(string reply, IReadOnlyList<AiCitationDto> citations)
    {
        if (citations.Count == 0)
            return reply;

        var sb = new StringBuilder(reply);
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("Sources:");
        foreach (var citation in citations.Take(5))
        {
            sb.AppendLine($"- {citation.Title} ({citation.DocumentType})");
        }

        return sb.ToString().Trim();
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";
}
