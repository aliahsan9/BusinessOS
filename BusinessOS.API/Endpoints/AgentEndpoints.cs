using System.Text.Json;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.Agents.Services;
using BusinessOS.Application.Features.AI.Services;
using BusinessOS.Infrastructure.AI;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.API.Endpoints;

/// <summary>
/// HTTP endpoints for AI employee agents (Navla and future specialists).
/// Extends the existing Copilot surface without replacing <c>/api/v1/ai</c>.
/// </summary>
public static class AgentEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void MapAgentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/agents")
            .WithTags("AI Employees")
            .RequireAuthorization()
            .RequireRateLimiting("ai");

        group.MapPost("/chat", Chat)
            .WithName("AgentChat")
            .Produces<AgentChatResponse>(StatusCodes.Status200OK);

        group.MapPost("/chat/stream", ChatStream)
            .WithName("AgentChatStream");

        group.MapGet("/employees", ListEmployees)
            .WithName("AgentListEmployees")
            .Produces<IReadOnlyList<AgentEmployeeDto>>(StatusCodes.Status200OK);

        group.MapGet("/tools", ListTools)
            .WithName("AgentListTools")
            .Produces<IReadOnlyList<AgentToolCatalogItemDto>>(StatusCodes.Status200OK);

        group.MapPut("/employees/{agentKey}/activation", SetEmployeeActivation)
            .WithName("AgentSetEmployeeActivation")
            .Produces<AgentEmployeeDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPut("/employees/{agentKey}/configuration", UpdateEmployeeConfiguration)
            .WithName("AgentUpdateEmployeeConfiguration")
            .Produces<AgentEmployeeDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/employees/{agentKey}/permissions", UpdateEmployeeToolPermissions)
            .WithName("AgentUpdateEmployeeToolPermissions")
            .Produces<AgentEmployeeDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/voice-preferences", GetVoicePreferences)
            .WithName("AgentGetVoicePreferences")
            .Produces<VoicePreferenceDto>(StatusCodes.Status200OK);

        group.MapPut("/voice-preferences", SaveVoicePreferences)
            .WithName("AgentSaveVoicePreferences")
            .Produces<VoicePreferenceDto>(StatusCodes.Status200OK);

        group.MapGet("/tts/voices", ListVoices)
            .WithName("AgentListTtsVoices")
            .Produces<TtsVoiceCatalogDto>(StatusCodes.Status200OK);

        group.MapPost("/tts", SynthesizeSpeech)
            .WithName("AgentSynthesizeSpeech")
            .Produces(StatusCodes.Status200OK, contentType: "audio/mpeg")
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status503ServiceUnavailable);

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

    private static IResult ListTools(IAiToolRegistry toolRegistry)
    {
        var tools = toolRegistry.AllTools
            .OrderBy(tool => tool.ToolName.ToString(), StringComparer.Ordinal)
            .Select(tool => new AgentToolCatalogItemDto
            {
                Name = tool.ToolName.ToString(),
                Description = tool.Description
            })
            .ToArray();

        return Results.Ok(tools);
    }

    private static async Task<IResult> ListEmployees(
        IAgentEmployeeService agentService,
        CancellationToken cancellationToken)
    {
        var employees = await agentService.ListEmployeesAsync(cancellationToken);
        return Results.Ok(employees);
    }

    private static async Task<IResult> SetEmployeeActivation(
        string agentKey,
        SetAgentActivationRequest request,
        IAgentPersonaService personaService,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is null || currentUser.TenantId == Guid.Empty)
            return Results.Forbid();

        var canManageAgents = currentUser.Roles.Any(role =>
            string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase));
        if (!canManageAgents)
            return Results.Forbid();

        if (string.IsNullOrWhiteSpace(agentKey))
            return Results.BadRequest(new { error = "Agent key is required." });

        try
        {
            var employee = await personaService.SetEmployeeActivationAsync(
                agentKey,
                request.IsActive,
                cancellationToken);
            return employee is null
                ? Results.NotFound(new { error = "AI employee was not found in the system catalog." })
                : Results.Ok(employee);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }
    }

    private static async Task<IResult> UpdateEmployeeConfiguration(
        string agentKey,
        UpdateAgentConfigurationRequest request,
        IAgentPersonaService personaService,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is null || currentUser.TenantId == Guid.Empty)
            return Results.Forbid();

        var canManageAgents = currentUser.Roles.Any(role =>
            string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase));
        if (!canManageAgents)
            return Results.Forbid();

        if (string.IsNullOrWhiteSpace(agentKey))
            return Results.BadRequest(new { error = "Agent key is required." });

        try
        {
            var employee = await personaService.UpdateEmployeeConfigurationAsync(
                agentKey,
                request,
                cancellationToken);
            return employee is null
                ? Results.NotFound(new { error = "AI employee was not found in the system catalog." })
                : Results.Ok(employee);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }
    }

    private static async Task<IResult> UpdateEmployeeToolPermissions(
        string agentKey,
        UpdateAgentToolPermissionsRequest request,
        IAgentPersonaService personaService,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is null || currentUser.TenantId == Guid.Empty)
            return Results.Forbid();

        var canManageAgents = currentUser.Roles.Any(role =>
            string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase));
        if (!canManageAgents)
            return Results.Forbid();

        if (string.IsNullOrWhiteSpace(agentKey))
            return Results.BadRequest(new { error = "Agent key is required." });

        try
        {
            var employee = await personaService.UpdateEmployeeToolPermissionsAsync(
                agentKey,
                request,
                cancellationToken);
            return employee is null
                ? Results.NotFound(new { error = "AI employee was not found in the system catalog." })
                : Results.Ok(employee);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }
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

    private static IResult ListVoices(ISophiaTtsService tts) =>
        Results.Ok(new TtsVoiceCatalogDto
        {
            Provider = tts.ActiveProvider,
            DefaultVoice = tts.DefaultVoice,
            Voices = tts.ListVoices()
        });

    private static async Task<IResult> SynthesizeSpeech(
        SophiaTtsRequest request,
        ISophiaTtsService tts,
        IVoicePreferenceService voicePrefs,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return Results.BadRequest(new { error = "Text is required." });

        var prefs = await voicePrefs.GetAsync(cancellationToken);
        var language = string.IsNullOrWhiteSpace(request.Language)
            ? prefs.Language
            : request.Language;
        var voice = string.IsNullOrWhiteSpace(request.VoiceName)
            ? prefs.VoiceName
            : request.VoiceName;
        var rate = request.SpeechRate ?? prefs.SpeechRate;

        try
        {
            var result = await tts.SynthesizeAsync(request.Text, language, rate, voice, cancellationToken);
            httpContext.Response.Headers["X-Tts-Provider"] = result.Provider;
            httpContext.Response.Headers["X-Tts-Voice"] = result.VoiceName;
            httpContext.Response.Headers["X-Tts-Requested-Voice"] = voice;
            httpContext.Response.Headers["X-Tts-Cache"] = result.ServedFromCache ? "hit" : "miss";
            return Results.File(result.AudioBytes, result.ContentType, fileDownloadName: null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OpenAiProviderException ex)
        {
            return SpeechFailure(ex.Failure);
        }
        catch (Exception ex) when (ex.InnerException is OpenAiProviderException provider)
        {
            return SpeechFailure(provider.Failure);
        }
        catch (Exception)
        {
            return Results.Json(
                new { error = "Speech synthesis is unavailable." },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static IResult SpeechFailure(OpenAiFailure failure) =>
        Results.Json(
            new { error = failure.ClientMessage },
            statusCode: failure.ClientStatusCode);

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
