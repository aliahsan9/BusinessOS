using BusinessOS.Application.Features.AI.DTOs;

namespace BusinessOS.Application.Features.AI.Services;

public interface IAiAssistantService
{
    Task<AiChatResponse> ChatAsync(AiChatRequest request, CancellationToken cancellationToken = default);
}
