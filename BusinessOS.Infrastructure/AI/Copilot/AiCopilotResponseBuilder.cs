using System.Text;
using System.Text.Json;
using BusinessOS.Application.Features.AI.DTOs;

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
            question = message
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
}
