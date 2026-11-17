using System.Text.Json;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.Agents.Services;
using BusinessOS.Infrastructure.AI;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Application.Features.Notifications.DTOs;
using BusinessOS.Application.Features.Notifications.Services;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
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

        group.MapGet("/employees/{agentKey}/permissions", GetEmployeeToolPermissions)
            .WithName("AgentGetToolPermissions")
            .Produces<AgentToolPermissionsDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/employees/{agentKey}/permissions", UpdateEmployeeToolPermissions)
            .WithName("AgentUpdateToolPermissions")
            .Produces<AgentToolPermissionsDto>(StatusCodes.Status200OK)
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

        group.MapPost("/actions/proposals", CreateActionProposal)
            .WithName("AgentCreateActionProposal")
            .Produces<AgentActionProposalDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden);

        group.MapGet("/actions/proposals", ListActionProposals)
            .WithName("AgentListActionProposals")
            .Produces<IReadOnlyList<AgentActionProposalDto>>(StatusCodes.Status200OK);

        group.MapGet("/actions/proposals/{proposalId:guid}", GetActionProposal)
            .WithName("AgentGetActionProposal")
            .Produces<AgentActionProposalDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/actions/proposals/{proposalId:guid}/decision", DecideActionProposal)
            .WithName("AgentDecideActionProposal")
            .Produces<AgentActionProposalDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/actions/proposals/{proposalId:guid}/execute", ExecuteActionProposal)
            .WithName("AgentExecuteActionProposal")
            .Produces<AgentActionProposalDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status503ServiceUnavailable);

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

    private static async Task<IResult> GetEmployeeToolPermissions(
        string agentKey,
        IAgentPersonaService personaService,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is null || currentUser.TenantId == Guid.Empty)
            return Results.Forbid();

        if (!CanManageAgents(currentUser))
            return Results.Forbid();

        var permissions = await personaService.GetToolPermissionsAsync(agentKey, cancellationToken);
        return permissions is null
            ? Results.NotFound(new { error = "AI employee was not found." })
            : Results.Ok(permissions);
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

        if (!CanManageAgents(currentUser))
            return Results.Forbid();

        if (string.IsNullOrWhiteSpace(agentKey))
            return Results.BadRequest(new { error = "Agent key is required." });

        try
        {
            var permissions = await personaService.UpdateToolPermissionsAsync(agentKey, request, cancellationToken);
            return permissions is null
                ? Results.NotFound(new { error = "AI employee was not found." })
                : Results.Ok(permissions);
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

    private static bool CanManageAgents(ICurrentUserService currentUser) =>
        currentUser.Roles.Any(role =>
            string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase));

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


    private const string SalesAgentKey = "emma";
    private const string SalesFollowUpCapability = "SendCustomerFollowUp";
    private static readonly HashSet<string> SupportedProposalActions =
        new(StringComparer.Ordinal) { "SendCustomerFollowUp" };

    private sealed class AgentActionProposalPayload
    {
        public AgentActionProposalPayload() { }
        public string ActionType { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
        public string PayloadJson { get; init; } = "{}";
        public DateTime? ExpiresAt { get; init; }
        public string? DecisionComment { get; init; }
        public DateTime? DecidedAt { get; init; }
    }

    private static async Task<IResult> CreateActionProposal(
        CreateAgentActionProposalRequest request,
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not Guid tenantId || tenantId == Guid.Empty
            || string.IsNullOrWhiteSpace(currentUser.UserId))
            return Results.Forbid();

        if (!SupportedProposalActions.Contains(request.ActionType))
            return Results.BadRequest(new { error = "Unsupported proposal action type." });

        if (!await HasSalesFollowUpCapabilityAsync(db, tenantId, cancellationToken))
            return Results.Forbid();

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 120)
            return Results.BadRequest(new { error = "Title is required and must be at most 120 characters." });

        if (string.IsNullOrWhiteSpace(request.Summary) || request.Summary.Trim().Length > 1000)
            return Results.BadRequest(new { error = "Summary is required and must be at most 1000 characters." });

        if (string.IsNullOrWhiteSpace(request.PayloadJson) || request.PayloadJson.Length > 8000)
            return Results.BadRequest(new { error = "Payload must be valid JSON and at most 8000 characters." });

        try
        {
            using var payloadDocument = JsonDocument.Parse(request.PayloadJson);
            if (payloadDocument.RootElement.ValueKind != JsonValueKind.Object)
                return Results.BadRequest(new { error = "Proposal payload must be a JSON object." });
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "Proposal payload must be valid JSON." });
        }

        var now = DateTime.UtcNow;
        if (request.ExpiresAt is DateTime expiresAt
            && (expiresAt <= now || expiresAt > now.AddDays(30)))
            return Results.BadRequest(new { error = "Expiry must be within the next 30 days." });

        var run = new AgentWorkflowRun
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = currentUser.UserId,
            AgentKey = SalesAgentKey,
            Title = $"Approval: {request.Title.Trim()}",
            Status = AgentWorkflowStatus.AwaitingApproval,
            StartedAt = now,
            ProgressJson = JsonSerializer.Serialize(new AgentActionProposalPayload
            {
                ActionType = request.ActionType,
                Summary = request.Summary.Trim(),
                PayloadJson = request.PayloadJson,
                ExpiresAt = request.ExpiresAt
            }, JsonOptions)
        };

        db.AgentWorkflowRuns.Add(run);
        db.RbacAuditLogs.Add(new RbacAuditLog
        {
            TenantId = tenantId,
            ActorUserId = currentUser.UserId,
            Action = "AgentActionProposalCreated",
            EntityType = "AgentWorkflowRun",
            EntityId = run.Id.ToString(),
            NewValue = request.ActionType
        });
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/v1/agents/actions/proposals/{run.Id}",
            MapActionProposal(run));
    }

    private static async Task<IResult> ListActionProposals(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        [FromQuery] bool pendingOnly = true,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.TenantId is null || currentUser.TenantId == Guid.Empty)
            return Results.Forbid();

        var tenantId = currentUser.TenantId.Value;
        var query = db.AgentWorkflowRuns.AsNoTracking()
            .Where(run => run.TenantId == tenantId && run.AgentKey == SalesAgentKey);

        var runs = await query.OrderByDescending(run => run.StartedAt)
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync(cancellationToken);

        var proposals = runs.Select(MapActionProposal);
        if (pendingOnly)
            proposals = proposals.Where(proposal => proposal.Status == AgentWorkflowStatus.AwaitingApproval);

        return Results.Ok(proposals.ToList());
    }

    private static async Task<IResult> GetActionProposal(
        Guid proposalId,
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is null || currentUser.TenantId == Guid.Empty)
            return Results.Forbid();

        var tenantId = currentUser.TenantId.Value;
        var run = await db.AgentWorkflowRuns.AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.TenantId == tenantId
                    && item.Id == proposalId
                    && item.AgentKey == SalesAgentKey,
                cancellationToken);

        return run is null
            ? Results.NotFound(new { error = "Proposal was not found." })
            : Results.Ok(MapActionProposal(run));
    }

    private static async Task<IResult> DecideActionProposal(
        Guid proposalId,
        DecideAgentActionProposalRequest request,
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return Results.Forbid();

        var canApprove = currentUser.Roles.Any(role =>
            string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase));
        if (!canApprove)
            return Results.Forbid();

        var decision = request.Decision?.Trim().ToLowerInvariant();
        if (decision is not ("approved" or "rejected"))
            return Results.BadRequest(new { error = "Decision must be 'approved' or 'rejected'." });

        if (request.Comment?.Length > 500)
            return Results.BadRequest(new { error = "Comment must be at most 500 characters." });

        var run = await db.AgentWorkflowRuns.FirstOrDefaultAsync(
            item => item.Id == proposalId
                && item.TenantId == tenantId
                && item.AgentKey == SalesAgentKey,
            cancellationToken);
        if (run is null)
            return Results.NotFound(new { error = "Proposal was not found." });

        if (run.Status != AgentWorkflowStatus.AwaitingApproval)
            return Results.Conflict(new { error = "This proposal has already been decided or is no longer pending." });

        var payload = DeserializeProposalPayload(run.ProgressJson);
        var now = DateTime.UtcNow;
        if (payload.ExpiresAt is DateTime expiresAt && expiresAt <= now)
            return Results.Conflict(new { error = "This proposal has expired and cannot be approved." });

        var newStatus = decision == "approved"
            ? AgentWorkflowStatus.Approved
            : AgentWorkflowStatus.Rejected;
        var updatedPayload = JsonSerializer.Serialize(new AgentActionProposalPayload
        {
            ActionType = payload.ActionType,
            Summary = payload.Summary,
            PayloadJson = payload.PayloadJson,
            ExpiresAt = payload.ExpiresAt,
            DecisionComment = request.Comment?.Trim(),
            DecidedAt = now
        }, JsonOptions);

        // Compare-and-set the pending state. Only one concurrent reviewer can win.
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        var transitioned = await db.TryTransitionAgentWorkflowStatusAsync(
            proposalId,
            tenantId,
            AgentWorkflowStatus.AwaitingApproval,
            newStatus,
            now,
            now,
            updatedPayload,
            cancellationToken);

        if (!transitioned)
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            return Results.Conflict(new { error = "This proposal has already been decided by another reviewer." });
        }

        // Keep the tracked instance aligned with the successful conditional update.
        run.Status = newStatus;
        run.CompletedAt = now;
        run.UpdatedAt = now;
        run.ProgressJson = updatedPayload;

        db.RbacAuditLogs.Add(new RbacAuditLog
        {
            TenantId = tenantId,
            ActorUserId = currentUser.UserId ?? run.UserId,
            Action = decision == "approved"
                ? "AgentActionProposalApproved"
                : "AgentActionProposalRejected",
            EntityType = "AgentWorkflowRun",
            EntityId = run.Id.ToString(),
            OldValue = AgentWorkflowStatus.AwaitingApproval.ToString(),
            NewValue = newStatus.ToString()
        });

        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        // Approval records consent only. No external message or business mutation is executed here.
        return Results.Ok(MapActionProposal(run));
    }

    private sealed class SendCustomerFollowUpPayload
    {
        public SendCustomerFollowUpPayload() { }
        public Guid CustomerId { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    private static async Task<IResult> ExecuteActionProposal(
        Guid proposalId,
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IEmailNotificationService emailService,
        CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return Results.Forbid();

        var canExecute = currentUser.Roles.Any(role =>
            string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase));
        if (!canExecute)
            return Results.Forbid();

        // Revalidate the tenant's explicit employee capability at execution time.
        if (!await HasSalesFollowUpCapabilityAsync(db, tenantId, cancellationToken))
            return Results.Forbid();

        var run = await db.AgentWorkflowRuns.FirstOrDefaultAsync(
            item => item.Id == proposalId
                && item.TenantId == tenantId
                && item.AgentKey == SalesAgentKey,
            cancellationToken);
        if (run is null)
            return Results.NotFound(new { error = "Proposal was not found." });

        if (run.Status != AgentWorkflowStatus.Approved)
            return Results.Conflict(new { error = "Only an approved proposal can be executed, and each proposal can be attempted once." });

        var proposal = DeserializeProposalPayload(run.ProgressJson);
        if (!string.Equals(proposal.ActionType, "SendCustomerFollowUp", StringComparison.Ordinal))
            return Results.BadRequest(new { error = "Unsupported proposal action type." });

        SendCustomerFollowUpPayload? action;
        try
        {
            action = JsonSerializer.Deserialize<SendCustomerFollowUpPayload>(proposal.PayloadJson, JsonOptions);
        }
        catch (JsonException)
        {
            action = null;
        }

        if (action is null || action.CustomerId == Guid.Empty
            || string.IsNullOrWhiteSpace(action.Message) || action.Message.Length > 5000)
            return Results.BadRequest(new { error = "The approved follow-up payload is invalid." });

        var customer = await db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == action.CustomerId
                    && item.TenantId == tenantId
                    && item.IsActive,
                cancellationToken);

        if (customer is null)
            return Results.BadRequest(new { error = "The approved customer no longer exists or is inactive." });

        if (!MailAddress.TryCreate(customer.Email, out _))
            return Results.BadRequest(new { error = "The customer does not have a valid email address." });

        var now = DateTime.UtcNow;
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        var claimed = await db.TryTransitionAgentWorkflowStatusAsync(
            proposalId,
            tenantId,
            AgentWorkflowStatus.Approved,
            AgentWorkflowStatus.Executing,
            now,
            run.CompletedAt,
            run.ProgressJson,
            cancellationToken);

        if (!claimed)
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            return Results.Conflict(new { error = "This proposal has already been claimed for execution." });
        }

        db.RbacAuditLogs.Add(new RbacAuditLog
        {
            TenantId = tenantId,
            ActorUserId = currentUser.UserId ?? run.UserId,
            Action = "AgentActionProposalExecutionStarted",
            EntityType = "AgentWorkflowRun",
            EntityId = run.Id.ToString(),
            OldValue = AgentWorkflowStatus.Approved.ToString(),
            NewValue = AgentWorkflowStatus.Executing.ToString()
        });

        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        // The proposal ID is the idempotency key. The compare-and-set claim above allows
        // only one delivery attempt; automatic SMTP retries are deliberately disabled.
        db.Detach(run);
        var executingRun = await db.AgentWorkflowRuns.FirstOrDefaultAsync(
            item => item.Id == proposalId && item.TenantId == tenantId,
            cancellationToken);

        if (executingRun is null)
            return Results.Conflict(new { error = "The proposal was claimed but could not be reloaded. Manual reconciliation is required." });

        try
        {
            var delivery = await emailService.SendMessageAsync(
                new EmailMessage(
                    customer.Email,
                    "A follow-up from your business",
                    action.Message.Trim()),
                cancellationToken);

            if (!delivery.Delivered)
            {
                executingRun.Status = AgentWorkflowStatus.ExecutionFailed;
                executingRun.ErrorMessage = "Email provider is not configured; no message was sent.";
                executingRun.ResultSummary = "Delivery was not attempted because email is not configured.";
                executingRun.CompletedAt = DateTime.UtcNow;
                executingRun.UpdatedAt = DateTime.UtcNow;
                db.RbacAuditLogs.Add(new RbacAuditLog
                {
                    TenantId = tenantId,
                    ActorUserId = currentUser.UserId ?? run.UserId,
                    Action = "AgentActionProposalExecutionFailed",
                    EntityType = "AgentWorkflowRun",
                    EntityId = run.Id.ToString(),
                    OldValue = AgentWorkflowStatus.Executing.ToString(),
                    NewValue = AgentWorkflowStatus.ExecutionFailed.ToString()
                });
                await db.SaveChangesAsync(cancellationToken);

                return Results.Json(
                    new { error = "Email delivery is not configured. The proposal was marked as failed and will not be retried automatically." },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            executingRun.Status = AgentWorkflowStatus.Executed;
            executingRun.ResultSummary = "Customer follow-up email was accepted by the configured SMTP provider.";
            executingRun.ErrorMessage = null;
            executingRun.CompletedAt = DateTime.UtcNow;
            executingRun.UpdatedAt = DateTime.UtcNow;
            db.RbacAuditLogs.Add(new RbacAuditLog
            {
                TenantId = tenantId,
                ActorUserId = currentUser.UserId ?? run.UserId,
                Action = "AgentActionProposalExecuted",
                EntityType = "AgentWorkflowRun",
                EntityId = run.Id.ToString(),
                OldValue = AgentWorkflowStatus.Executing.ToString(),
                NewValue = AgentWorkflowStatus.Executed.ToString()
            });
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(MapActionProposal(executingRun));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The provider may have accepted the message before cancellation. Leave the
            // record in Executing so it cannot be retried and accidentally duplicate delivery.
            throw;
        }
        catch (Exception)
        {
            executingRun.Status = AgentWorkflowStatus.ExecutionFailed;
            executingRun.ErrorMessage = "Email delivery failed. Automatic retry is disabled to prevent duplicate messages.";
            executingRun.ResultSummary = "Delivery failed; manual reconciliation is required before another attempt.";
            executingRun.CompletedAt = DateTime.UtcNow;
            executingRun.UpdatedAt = DateTime.UtcNow;
            db.RbacAuditLogs.Add(new RbacAuditLog
            {
                TenantId = tenantId,
                ActorUserId = currentUser.UserId ?? run.UserId,
                Action = "AgentActionProposalExecutionFailed",
                EntityType = "AgentWorkflowRun",
                EntityId = run.Id.ToString(),
                OldValue = AgentWorkflowStatus.Executing.ToString(),
                NewValue = AgentWorkflowStatus.ExecutionFailed.ToString()
            });
            await db.SaveChangesAsync(cancellationToken);
            return Results.Json(
                new { error = "Email delivery failed. Automatic retry is disabled; review the execution history before taking further action." },
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task<bool> HasSalesFollowUpCapabilityAsync(
        IApplicationDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var profile = await db.AgentProfiles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.TenantId == tenantId
                    && item.Key == SalesAgentKey
                    && item.IsActive
                    && !item.IsDeleted,
                cancellationToken);

        if (profile is null)
            return false;

        try
        {
            var allowed = JsonSerializer.Deserialize<string[]>(profile.AllowedToolsJson ?? "[]", JsonOptions) ?? [];
            return allowed.Contains(SalesFollowUpCapability, StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static AgentActionProposalDto MapActionProposal(AgentWorkflowRun run)
    {
        var payload = DeserializeProposalPayload(run.ProgressJson);
        return new AgentActionProposalDto
        {
            Id = run.Id,
            AgentKey = run.AgentKey,
            ActionType = payload.ActionType,
            Title = run.Title.StartsWith("Approval: ", StringComparison.Ordinal)
                ? run.Title["Approval: ".Length..]
                : run.Title,
            Summary = payload.Summary,
            PayloadJson = payload.PayloadJson,
            Status = run.Status == AgentWorkflowStatus.AwaitingApproval
                && payload.ExpiresAt is DateTime expiresAt
                && expiresAt <= DateTime.UtcNow
                    ? AgentWorkflowStatus.Expired
                    : run.Status,
            CreatedAt = run.StartedAt,
            ExpiresAt = payload.ExpiresAt,
            DecisionComment = payload.DecisionComment,
            DecidedAt = payload.DecidedAt,
            ExecutionMessage = run.ResultSummary ?? (run.Status == AgentWorkflowStatus.ExecutionFailed ? run.ErrorMessage : null)
        };
    }

    private static AgentActionProposalPayload DeserializeProposalPayload(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new AgentActionProposalPayload();

        try
        {
            return JsonSerializer.Deserialize<AgentActionProposalPayload>(json, JsonOptions)
                ?? new AgentActionProposalPayload();
        }
        catch (JsonException)
        {
            return new AgentActionProposalPayload();
        }
    }

    private static async Task<IResult> GetAskSophiaSuggestions(
        IAgentEmployeeService agentService,
        CancellationToken cancellationToken)
    {
        var suggestions = await agentService.GetAskSophiaSuggestionsAsync(cancellationToken);
        return Results.Ok(suggestions);
    }
}
