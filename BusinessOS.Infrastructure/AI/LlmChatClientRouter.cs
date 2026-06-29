using BusinessOS.Application.Features.AI.Services;

namespace BusinessOS.Infrastructure.AI;

/// <summary>
/// Prefers OpenAI for RAG; falls back to Cursor Cloud Agents when only Cursor key is set.
/// </summary>
public sealed class LlmChatClientRouter : ILlmChatClient
{
    private readonly OpenAiChatClient _openAi;
    private readonly CursorLlmChatClient _cursor;

    public LlmChatClientRouter(OpenAiChatClient openAi, CursorLlmChatClient cursor)
    {
        _openAi = openAi;
        _cursor = cursor;
    }

    public bool IsConfigured => _openAi.IsConfigured || _cursor.IsConfigured;

    public async Task<string?> GenerateReplyAsync(
        Guid tenantId,
        string userId,
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        if (_openAi.IsConfigured)
        {
            var reply = await _openAi.GenerateReplyAsync(tenantId, userId, systemPrompt, userPrompt, cancellationToken);
            if (!string.IsNullOrWhiteSpace(reply))
                return reply;
        }

        if (_cursor.IsConfigured)
        {
            return await _cursor.GenerateReplyAsync(tenantId, userId, systemPrompt, userPrompt, cancellationToken);
        }

        return null;
    }
}
