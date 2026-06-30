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

    public async IAsyncEnumerable<string> StreamReplyAsync(
        Guid tenantId,
        string userId,
        string systemPrompt,
        string userPrompt,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_openAi.IsConfigured)
        {
            await foreach (var token in _openAi.StreamReplyAsync(tenantId, userId, systemPrompt, userPrompt, cancellationToken))
            {
                yield return token;
            }
            yield break;
        }

        var reply = await GenerateReplyAsync(tenantId, userId, systemPrompt, userPrompt, cancellationToken);
        if (!string.IsNullOrWhiteSpace(reply))
        {
            foreach (var word in reply.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                yield return word + " ";
            }
        }
    }

    public async Task<(string? Reply, int? TokenUsage)> GenerateWithToolsAsync(
        Guid tenantId,
        string userId,
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<object> toolResults,
        CancellationToken cancellationToken = default)
    {
        if (_openAi.IsConfigured)
        {
            return await _openAi.GenerateWithToolsAsync(tenantId, userId, systemPrompt, userPrompt, toolResults, cancellationToken);
        }

        var reply = await GenerateReplyAsync(tenantId, userId, systemPrompt, userPrompt, cancellationToken);
        return (reply, null);
    }
}
