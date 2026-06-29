using BusinessOS.Application.Features.AI.DTOs;

namespace BusinessOS.Application.Features.AI.Services;

public interface IAiContextService
{
    AiPageContextDto BuildPageContext(AiChatRequest request);

    Task<AiContextDto> BuildContextAsync(
        AiChatRequest request,
        AiRetrievalScope scope,
        CancellationToken cancellationToken = default);
}

public interface IAiRetrievalService
{
    AiRetrievalScope DetermineScope(string message, AiPageContextDto page);

    Task<AiContextDto> RetrieveAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default);

    AiRetrievedSourcesDto BuildSources(AiContextDto context);
}

public interface IAiActionService
{
    Task<AiActionResultDto?> TryExecuteAsync(
        string message,
        AiPageContextDto page,
        CancellationToken cancellationToken = default);
}

public interface IAiPromptBuilder
{
    string BuildSystemPrompt();

    string BuildUserPrompt(string message, AiContextDto context);
}

public interface IAiChatService
{
    Task<AiChatResponse> ChatAsync(AiChatRequest request, CancellationToken cancellationToken = default);
}
