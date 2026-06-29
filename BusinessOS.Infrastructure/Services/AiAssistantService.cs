using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Services;

namespace BusinessOS.Infrastructure.Services;

public sealed class AiAssistantService : IAiAssistantService
{
    private readonly IAiChatService _chatService;

    public AiAssistantService(IAiChatService chatService)
    {
        _chatService = chatService;
    }

    public Task<AiChatResponse> ChatAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default) =>
        _chatService.ChatAsync(request, cancellationToken);
}
