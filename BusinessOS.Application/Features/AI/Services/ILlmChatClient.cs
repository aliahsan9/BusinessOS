namespace BusinessOS.Application.Features.AI.Services;

public interface ILlmChatClient
{
    bool IsConfigured { get; }

    Task<string?> GenerateReplyAsync(
        Guid tenantId,
        string userId,
        string message,
        string currentPage,
        CancellationToken cancellationToken = default);
}
