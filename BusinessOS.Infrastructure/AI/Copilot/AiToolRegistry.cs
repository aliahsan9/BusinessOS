using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.AI.Services;

namespace BusinessOS.Infrastructure.AI.Copilot;

public sealed class AiToolRegistry : IAiToolRegistry
{
    private readonly IReadOnlyList<IAiTool> _tools;

    public AiToolRegistry(IEnumerable<IAiTool> tools)
    {
        _tools = tools.ToList();
    }

    public IReadOnlyList<IAiTool> AllTools => _tools;

    public IReadOnlyList<IAiTool> SelectTools(
        AiIntentDetectionResult intent,
        string message,
        AiPageContextDto page,
        AiMemoryStateDto memory)
    {
        var selected = new List<IAiTool>();

        foreach (var suggested in intent.SuggestedTools)
        {
            var tool = _tools.FirstOrDefault(t => t.ToolName == suggested);
            if (tool is not null && !selected.Contains(tool))
                selected.Add(tool);
        }

        foreach (var tool in _tools)
        {
            if (tool.CanHandle(intent.Intent, message, page, memory) && !selected.Contains(tool))
                selected.Add(tool);
        }

        // Analytics / sales questions often need summary + bestsellers + trends together.
        var limit = intent.Intent is AiCopilotIntent.Analytics or AiCopilotIntent.FollowUp ? 4 : 3;
        return selected.Take(limit).ToList();
    }
}
