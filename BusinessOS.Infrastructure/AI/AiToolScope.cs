using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;

namespace BusinessOS.Infrastructure.AI;

/// <summary>
/// Limits a model turn to the tools the intent detector already named.
/// A null set means the full read catalog. An empty set means no tools.
/// </summary>
public static class AiToolScope
{
    public static IReadOnlySet<AiToolName>? ForModel(AiIntentDetectionResult? intent)
    {
        if (intent is null)
            return null;

        if (intent.SuggestedTools.Count > 0 && intent.Confidence >= 0.8)
        {
            return intent.SuggestedTools
                .Where(tool => !AiToolSafetyGate.IsMutating(tool))
                .ToHashSet();
        }

        if (intent.Intent is AiCopilotIntent.Conversational or AiCopilotIntent.Help)
            return new HashSet<AiToolName>();

        if (intent.Confidence < 0.7)
            return new HashSet<AiToolName>();

        return null;
    }

    public static bool StaysOnScope(AiIntentDetectionResult intent, IReadOnlyList<string> toolsUsed)
    {
        var allowed = ForModel(intent);
        if (allowed is null)
            return true;

        return toolsUsed.All(name =>
            Enum.TryParse<AiToolName>(name, ignoreCase: false, out var tool) && allowed.Contains(tool));
    }
}
