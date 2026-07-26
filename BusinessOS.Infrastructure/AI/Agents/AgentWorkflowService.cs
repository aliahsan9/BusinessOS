using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.Agents.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.AI.Agents;

public sealed class AgentWorkflowService : IAgentWorkflowService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AgentWorkflowService> _logger;

    public AgentWorkflowService(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        ILogger<AgentWorkflowService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<AgentWorkflowDto> CreateFromPlanAsync(
        AgentWorkflowPlanDto plan,
        string userId,
        Guid? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        var run = new AgentWorkflowRun
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            SessionId = sessionId,
            AgentKey = plan.AgentKey,
            Title = plan.Title,
            Status = AgentWorkflowStatus.Pending,
            CurrentStepIndex = 0,
            StartedAt = DateTime.UtcNow,
            Steps = plan.Steps
                .OrderBy(s => s.SortOrder)
                .Select(s => new AgentWorkflowStep
                {
                    Id = Guid.NewGuid(),
                    StepKey = s.StepKey,
                    Title = s.Title,
                    SortOrder = s.SortOrder,
                    Status = AgentWorkflowStepStatus.Pending
                })
                .ToList()
        };

        _context.AgentWorkflowRuns.Add(run);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created agent workflow {WorkflowId} '{Title}' for user {UserId} with {StepCount} steps",
            run.Id,
            run.Title,
            userId,
            run.Steps.Count);

        return Map(run);
    }

    public async Task<AgentWorkflowDto?> GetAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        var run = await _context.AgentWorkflowRuns
            .AsNoTracking()
            .Include(r => r.Steps)
            .FirstOrDefaultAsync(r => r.Id == workflowId, cancellationToken);

        return run is null ? null : Map(run);
    }

    public async Task<IReadOnlyList<AgentWorkflowSummaryDto>> ListRecentAsync(
        string userId,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(limit, 1, 100);

        var runs = await _context.AgentWorkflowRuns
            .AsNoTracking()
            .Include(r => r.Steps)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.StartedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return runs.Select(r => new AgentWorkflowSummaryDto
        {
            Id = r.Id,
            AgentKey = r.AgentKey,
            Title = r.Title,
            Status = r.Status,
            CurrentStepIndex = r.CurrentStepIndex,
            TotalSteps = r.Steps.Count,
            ResultSummary = r.ResultSummary,
            StartedAt = r.StartedAt,
            CompletedAt = r.CompletedAt
        }).ToList();
    }

    public async Task<AgentWorkflowDto> StartAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        var run = await LoadTrackedAsync(workflowId, cancellationToken);
        run.Status = AgentWorkflowStatus.Running;
        run.StartedAt = DateTime.UtcNow;
        run.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Started agent workflow {WorkflowId}", workflowId);
        return Map(run);
    }

    public async Task<AgentWorkflowStepDto> BeginStepAsync(
        Guid workflowId,
        string stepKey,
        string? message = null,
        CancellationToken cancellationToken = default)
    {
        var run = await LoadTrackedAsync(workflowId, cancellationToken);
        var step = FindStep(run, stepKey);

        step.Status = AgentWorkflowStepStatus.Running;
        step.StartedAt ??= DateTime.UtcNow;
        step.Message = message ?? step.Message;
        run.CurrentStepIndex = step.SortOrder;
        run.Status = AgentWorkflowStatus.Running;
        run.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Began workflow {WorkflowId} step {StepKey}", workflowId, stepKey);
        return MapStep(step);
    }

    public async Task<AgentWorkflowStepDto> CompleteStepAsync(
        Guid workflowId,
        string stepKey,
        string? message = null,
        CancellationToken cancellationToken = default)
    {
        var run = await LoadTrackedAsync(workflowId, cancellationToken);
        var step = FindStep(run, stepKey);

        step.Status = AgentWorkflowStepStatus.Completed;
        step.CompletedAt = DateTime.UtcNow;
        step.Message = message ?? step.Message;
        run.CurrentStepIndex = step.SortOrder;
        run.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Completed workflow {WorkflowId} step {StepKey}", workflowId, stepKey);
        return MapStep(step);
    }

    public async Task<AgentWorkflowStepDto> FailStepAsync(
        Guid workflowId,
        string stepKey,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        var run = await LoadTrackedAsync(workflowId, cancellationToken);
        var step = FindStep(run, stepKey);

        step.Status = AgentWorkflowStepStatus.Failed;
        step.CompletedAt = DateTime.UtcNow;
        step.Message = errorMessage;
        run.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogWarning("Failed workflow {WorkflowId} step {StepKey}: {Error}", workflowId, stepKey, errorMessage);
        return MapStep(step);
    }

    public async Task<AgentWorkflowStepDto> SkipStepAsync(
        Guid workflowId,
        string stepKey,
        string? message = null,
        CancellationToken cancellationToken = default)
    {
        var run = await LoadTrackedAsync(workflowId, cancellationToken);
        var step = FindStep(run, stepKey);

        step.Status = AgentWorkflowStepStatus.Skipped;
        step.CompletedAt = DateTime.UtcNow;
        step.Message = message ?? step.Message;
        run.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return MapStep(step);
    }

    public async Task<AgentWorkflowDto> CompleteAsync(
        Guid workflowId,
        string? resultSummary = null,
        CancellationToken cancellationToken = default)
    {
        var run = await LoadTrackedAsync(workflowId, cancellationToken);
        run.Status = AgentWorkflowStatus.Completed;
        run.CompletedAt = DateTime.UtcNow;
        run.ResultSummary = resultSummary;
        run.UpdatedAt = DateTime.UtcNow;

        foreach (var pending in run.Steps.Where(s => s.Status == AgentWorkflowStepStatus.Pending))
        {
            pending.Status = AgentWorkflowStepStatus.Skipped;
            pending.CompletedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Completed agent workflow {WorkflowId}", workflowId);
        return Map(run);
    }

    public async Task<AgentWorkflowDto> FailAsync(
        Guid workflowId,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        var run = await LoadTrackedAsync(workflowId, cancellationToken);
        run.Status = AgentWorkflowStatus.Failed;
        run.CompletedAt = DateTime.UtcNow;
        run.ErrorMessage = errorMessage;
        run.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogWarning("Failed agent workflow {WorkflowId}: {Error}", workflowId, errorMessage);
        return Map(run);
    }

    public async Task<AgentWorkflowDto> CancelAsync(
        Guid workflowId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var run = await LoadTrackedAsync(workflowId, cancellationToken);
        run.Status = AgentWorkflowStatus.Cancelled;
        run.CompletedAt = DateTime.UtcNow;
        run.ErrorMessage = reason;
        run.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Map(run);
    }

    public async Task UpdateProgressJsonAsync(
        Guid workflowId,
        string progressJson,
        CancellationToken cancellationToken = default)
    {
        var run = await LoadTrackedAsync(workflowId, cancellationToken);
        run.ProgressJson = progressJson;
        run.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetStatusAsync(
        Guid workflowId,
        AgentWorkflowStatus status,
        CancellationToken cancellationToken = default)
    {
        var run = await LoadTrackedAsync(workflowId, cancellationToken);
        run.Status = status;
        run.UpdatedAt = DateTime.UtcNow;
        if (status is AgentWorkflowStatus.Completed or AgentWorkflowStatus.Failed or AgentWorkflowStatus.Cancelled)
            run.CompletedAt ??= DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<AgentWorkflowRun> LoadTrackedAsync(Guid workflowId, CancellationToken cancellationToken)
    {
        var run = await _context.AgentWorkflowRuns
            .Include(r => r.Steps)
            .FirstOrDefaultAsync(r => r.Id == workflowId, cancellationToken);

        return run ?? throw new InvalidOperationException($"Workflow '{workflowId}' was not found.");
    }

    private static AgentWorkflowStep FindStep(AgentWorkflowRun run, string stepKey) =>
        run.Steps.FirstOrDefault(s => string.Equals(s.StepKey, stepKey, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Step '{stepKey}' was not found on workflow '{run.Id}'.");

    private static AgentWorkflowDto Map(AgentWorkflowRun run) => new()
    {
        Id = run.Id,
        AgentKey = run.AgentKey,
        Title = run.Title,
        Status = run.Status,
        CurrentStepIndex = run.CurrentStepIndex,
        ResultSummary = run.ResultSummary,
        ErrorMessage = run.ErrorMessage,
        SessionId = run.SessionId,
        StartedAt = run.StartedAt,
        CompletedAt = run.CompletedAt,
        Steps = run.Steps.OrderBy(s => s.SortOrder).Select(MapStep).ToList()
    };

    private static AgentWorkflowStepDto MapStep(AgentWorkflowStep step) => new()
    {
        Id = step.Id,
        StepKey = step.StepKey,
        Title = step.Title,
        Status = step.Status,
        SortOrder = step.SortOrder,
        Message = step.Message,
        StartedAt = step.StartedAt,
        CompletedAt = step.CompletedAt
    };
}
