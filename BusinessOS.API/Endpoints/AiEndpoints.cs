using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Services;

namespace BusinessOS.API.Endpoints;

public static class AiEndpoints
{
    public static void MapAiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ai")
            .WithTags("AI Assistant")
            .RequireAuthorization();

        group.MapPost("/chat", Chat)
            .WithName("AiChat")
            .Produces<AiChatResponse>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> Chat(
        AiChatRequest request,
        IAiAssistantService aiService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message) && string.IsNullOrWhiteSpace(request.SearchQuery))
            return Results.BadRequest(new { error = "Message or search query is required." });

        var result = await aiService.ChatAsync(request, cancellationToken);
        return Results.Ok(result);
    }
}
