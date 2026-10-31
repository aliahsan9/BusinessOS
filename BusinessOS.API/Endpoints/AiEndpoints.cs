using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Services;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.API.Endpoints;

public static class AiEndpoints
{
    public static void MapAiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ai")
            .WithTags("AI Copilot")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        group.MapPost("/chat", Chat)
            .WithName("AiChat")
            .Produces<AiCopilotChatResponse>(StatusCodes.Status200OK);

        group.MapPost("/chat/stream", ChatStream)
            .WithName("AiChatStream");

        group.MapPost("/conversations", CreateConversation)
            .WithName("AiCreateConversation")
            .Produces<AiConversationSessionDto>(StatusCodes.Status201Created);

        group.MapGet("/conversations", ListConversations)
            .WithName("AiListConversations")
            .Produces<IReadOnlyList<AiConversationSessionDto>>(StatusCodes.Status200OK);

        group.MapGet("/conversations/{sessionId:guid}", GetConversation)
            .WithName("AiGetConversation")
            .Produces<IReadOnlyList<AiConversationMessageDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/conversations/{sessionId:guid}/messages", GetConversationMessages)
            .WithName("AiGetConversationMessages")
            .Produces<IReadOnlyList<AiConversationMessageDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/conversations/{sessionId:guid}/messages", SendConversationMessage)
            .WithName("AiSendConversationMessage")
            .Produces<AiCopilotChatResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/conversations/{sessionId:guid}", DeleteConversation)
            .WithName("AiDeleteConversation")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/insights", GetInsights)
            .WithName("AiInsights")
            .Produces<IReadOnlyList<AiProactiveInsightDto>>(StatusCodes.Status200OK);

        group.MapGet("/dashboard-copilot", GetDashboardCopilot)
            .WithName("AiDashboardCopilot")
            .Produces<AiDashboardCopilotDto>(StatusCodes.Status200OK);

        group.MapGet("/diagnostics", GetDiagnostics)
            .WithName("AiDiagnostics")
            .Produces<AiDiagnosticsSummaryDto>(StatusCodes.Status200OK);

        group.MapPost("/analytics/query", RunAnalyticsQuery)
            .WithName("AiAnalyticsQuery")
            .Produces<AiAnalyticsQueryResponse>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> Chat(
        AiCopilotChatRequest request,
        IAiAssistantService aiService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message) && string.IsNullOrWhiteSpace(request.SearchQuery))
            return Results.BadRequest(new { error = "Message or search query is required." });

        var result = await aiService.CopilotChatAsync(request, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task ChatStream(
        AiCopilotChatRequest request,
        IAiAssistantService aiService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(new { error = "Message is required." }, cancellationToken);
            return;
        }

        httpContext.Response.Headers.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Connection = "keep-alive";

        await foreach (var chunk in aiService.CopilotStreamAsync(request, cancellationToken))
        {
            await httpContext.Response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(chunk)}\n\n", cancellationToken);
            await httpContext.Response.Body.FlushAsync(cancellationToken);
        }
    }

    private static async Task<IResult> CreateConversation(
        IAiAssistantService aiService,
        CancellationToken cancellationToken)
    {
        var session = await aiService.CreateConversationAsync(cancellationToken);
        return Results.Created($"/api/ai/conversations/{session.Id}", session);
    }

    private static async Task<IResult> ListConversations(
        IAiAssistantService aiService,
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var sessions = await aiService.ListConversationsAsync(skip, limit, cancellationToken);
        return Results.Ok(sessions);
    }

    private static async Task<IResult> GetConversation(
        Guid sessionId,
        IAiAssistantService aiService,
        CancellationToken cancellationToken)
    {
        var session = await aiService.GetConversationSessionAsync(sessionId, cancellationToken);
        if (session is null)
            return Results.NotFound(new { error = "Conversation not found." });

        var messages = await aiService.GetConversationAsync(sessionId, cancellationToken);
        return Results.Ok(messages);
    }

    private static async Task<IResult> GetConversationMessages(
        Guid sessionId,
        IAiAssistantService aiService,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var session = await aiService.GetConversationSessionAsync(sessionId, cancellationToken);
        if (session is null)
            return Results.NotFound(new { error = "Conversation not found." });

        var messages = await aiService.GetConversationAsync(sessionId, skip, take, cancellationToken);
        return Results.Ok(messages);
    }

    private static async Task<IResult> SendConversationMessage(
        Guid sessionId,
        AiCopilotChatRequest request,
        IAiAssistantService aiService,
        CancellationToken cancellationToken)
    {
        var session = await aiService.GetConversationSessionAsync(sessionId, cancellationToken);
        if (session is null)
            return Results.NotFound(new { error = "Conversation not found." });

        if (string.IsNullOrWhiteSpace(request.Message))
            return Results.BadRequest(new { error = "Message is required." });

        var result = await aiService.CopilotChatAsync(
            request with { SessionId = sessionId, ConversationId = sessionId },
            cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> DeleteConversation(
        Guid sessionId,
        IAiAssistantService aiService,
        CancellationToken cancellationToken)
    {
        var deleted = await aiService.DeleteConversationAsync(sessionId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound(new { error = "Conversation not found." });
    }

    private static async Task<IResult> GetInsights(
        IAiAssistantService aiService,
        CancellationToken cancellationToken)
    {
        var insights = await aiService.GetInsightsAsync(cancellationToken);
        return Results.Ok(insights);
    }

    private static async Task<IResult> GetDashboardCopilot(
        IAiAssistantService aiService,
        CancellationToken cancellationToken)
    {
        var dashboard = await aiService.GetDashboardCopilotAsync(cancellationToken);
        return Results.Ok(dashboard);
    }

    private static async Task<IResult> GetDiagnostics(
        IAiAssistantService aiService,
        [FromQuery] DateTime? since,
        CancellationToken cancellationToken)
    {
        var diagnostics = await aiService.GetDiagnosticsAsync(since, cancellationToken);
        return Results.Ok(diagnostics);
    }

    private static async Task<IResult> RunAnalyticsQuery(
        AiAnalyticsQueryRequest request,
        IAiAssistantService aiService,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await aiService.RunAnalyticsQueryAsync(request, cancellationToken);
            return Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}
