using System.Text.Json;
using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.Agents.Services;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.API.Endpoints;

/// <summary>
/// HTTP endpoints for AI employee agents (Sophia and future specialists).
/// Extends the existing Copilot surface without replacing <c>/api/ai</c>.
/// </summary>
public static class AgentEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void MapAgentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/agents")
            .WithTags("AI Employees")
            .RequireAuthorization();

        group.MapPost("/chat", Chat)
            .WithName("AgentChat")
            .Produces<AgentChatResponse>(StatusCodes.Status200OK);

        group.MapPost("/chat/stream", ChatStream)
            .WithName("AgentChatStream");

        group.MapGet("/employees", ListEmployees)
            .WithName("AgentListEmployees")
            .Produces<IReadOnlyList<AgentEmployeeDto>>(StatusCodes.Status200OK);

        group.MapGet("/voice-preferences", GetVoicePreferences)
            .WithName("AgentGetVoicePreferences")
            .Produces<VoicePreferenceDto>(StatusCodes.Status200OK);

        group.MapPut("/voice-preferences", SaveVoicePreferences)
            .WithName("AgentSaveVoicePreferences")
            .Produces<VoicePreferenceDto>(StatusCodes.Status200OK);

        group.MapPost("/onboarding/start", StartOnboarding)
            .WithName("AgentStartOnboarding")
            .Produces<AgentOnboardingResponse>(StatusCodes.Status200OK);

        group.MapPost("/onboarding/continue", ContinueOnboarding)
            .WithName("AgentContinueOnboarding")
            .Produces<AgentOnboardingResponse>(StatusCodes.Status200OK);

        group.MapGet("/workflows", ListWorkflows)
            .WithName("AgentListWorkflows")
            .Produces<IReadOnlyList<AgentWorkflowSummaryDto>>(StatusCodes.Status200OK);

        group.MapGet("/workflows/{workflowId:guid}", GetWorkflow)
            .WithName("AgentGetWorkflow")
            .Produces<AgentWorkflowDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/ask-sophia", GetAskSophiaSuggestions)
            .WithName("AgentAskSophiaSuggestions")
            .Produces<AskSophiaSuggestionsDto>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> Chat(
        AgentChatRequest request,
        IAgentEmployeeService agentService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return Results.BadRequest(new { error = "Message is required." });

        var result = await agentService.ChatAsync(request, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task ChatStream(
        AgentChatRequest request,
        IAgentEmployeeService agentService,
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

        await foreach (var chunk in agentService.ChatStreamAsync(request, cancellationToken))
        {
            await httpContext.Response.WriteAsync(
                $"data: {JsonSerializer.Serialize(chunk, JsonOptions)}\n\n",
                cancellationToken);
            await httpContext.Response.Body.FlushAsync(cancellationToken);
        }
    }

    private static async Task<IResult> ListEmployees(
        IAgentEmployeeService agentService,
        CancellationToken cancellationToken)
    {
        var employees = await agentService.ListEmployeesAsync(cancellationToken);
        return Results.Ok(employees);
    }

    private static async Task<IResult> GetVoicePreferences(
        IAgentEmployeeService agentService,
        CancellationToken cancellationToken)
    {
        var prefs = await agentService.GetVoicePreferencesAsync(cancellationToken);
        return Results.Ok(prefs);
    }

    private static async Task<IResult> SaveVoicePreferences(
        SaveVoicePreferenceRequest request,
        IAgentEmployeeService agentService,
        CancellationToken cancellationToken)
    {
        var prefs = await agentService.SaveVoicePreferencesAsync(request, cancellationToken);
        return Results.Ok(prefs);
    }

    private static async Task<IResult> StartOnboarding(
        AgentOnboardingStartRequest request,
        IAgentEmployeeService agentService,
        CancellationToken cancellationToken)
    {
        var result = await agentService.StartOnboardingWithAgentAsync(request, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> ContinueOnboarding(
        AgentOnboardingContinueRequest request,
        IAgentEmployeeService agentService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return Results.BadRequest(new { error = "Message is required." });

        var result = await agentService.ContinueOnboardingAsync(request, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> ListWorkflows(
        IAgentEmployeeService agentService,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var workflows = await agentService.ListRecentWorkflowsAsync(limit, cancellationToken);
        return Results.Ok(workflows);
    }

    private static async Task<IResult> GetWorkflow(
        Guid workflowId,
        IAgentEmployeeService agentService,
        CancellationToken cancellationToken)
    {
        var workflow = await agentService.GetWorkflowAsync(workflowId, cancellationToken);
        return workflow is null
            ? Results.NotFound(new { error = "Workflow not found." })
            : Results.Ok(workflow);
    }

    private static async Task<IResult> GetAskSophiaSuggestions(
        IAgentEmployeeService agentService,
        CancellationToken cancellationToken)
    {
        var suggestions = await agentService.GetAskSophiaSuggestionsAsync(cancellationToken);
        return Results.Ok(suggestions);
    }
}
