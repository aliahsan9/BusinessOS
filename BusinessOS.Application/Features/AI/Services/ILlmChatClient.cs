using BusinessOS.Application.Features.AI.DTOs;

namespace BusinessOS.Application.Features.AI.Services;

public interface ILlmChatClient
{
    bool IsConfigured { get; }

    Task<string?> GenerateReplyAsync(
        Guid tenantId,
        string userId,
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamReplyAsync(
        Guid tenantId,
        string userId,
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default);

    Task<(string? Reply, int? TokenUsage)> GenerateWithToolsAsync(
        Guid tenantId,
        string userId,
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<object> toolResults,
        CancellationToken cancellationToken = default);
}
