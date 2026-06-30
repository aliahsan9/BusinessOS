using System.Diagnostics;
using System.Text.Json;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.AI.Services;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.AI.Copilot;

public sealed class AiObservabilityService : IAiObservabilityService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AiObservabilityService(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task LogAsync(
        Guid? sessionId,
        AiCopilotIntent intent,
        string? userMessage,
        IReadOnlyList<string> toolsUsed,
        IReadOnlyList<AiCitationDto> citations,
        int executionTimeMs,
        int? tokenUsage,
        bool success,
        string? errorMessage,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var userId = _currentUser.UserId;
        if (tenantId is null || userId is null)
            return;

        _context.AiCopilotAuditLogs.Add(new AiCopilotAuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            UserId = userId,
            SessionId = sessionId,
            Intent = intent.ToString(),
            UserMessage = userMessage?.Length > 2000 ? userMessage[..2000] : userMessage,
            ToolsUsedJson = JsonSerializer.Serialize(toolsUsed, JsonOptions),
            RetrievedDocumentsJson = JsonSerializer.Serialize(citations, JsonOptions),
            ExecutionTimeMs = executionTimeMs,
            TokenUsage = tokenUsage,
            Success = success,
            ErrorMessage = errorMessage
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<AiDiagnosticsSummaryDto> GetDiagnosticsAsync(
        DateTime? since,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId ?? throw new InvalidOperationException("User is required.");
        var from = since ?? DateTime.UtcNow.AddDays(-7);

        var logs = await _context.AiCopilotAuditLogs
            .AsNoTracking()
            .Where(l => l.UserId == userId && l.CreatedAt >= from)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(cancellationToken);

        var intentBreakdown = logs
            .GroupBy(l => l.Intent)
            .Select(g => new AiDiagnosticsIntentBreakdownDto { Intent = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        var toolBreakdown = logs
            .SelectMany(l => ParseJsonList(l.ToolsUsedJson))
            .GroupBy(t => t)
            .Select(g => new AiDiagnosticsToolBreakdownDto { Tool = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        return new AiDiagnosticsSummaryDto
        {
            TotalRequests = logs.Count,
            SuccessfulRequests = logs.Count(l => l.Success),
            FailedRequests = logs.Count(l => !l.Success),
            AverageExecutionTimeMs = logs.Count == 0 ? 0 : logs.Average(l => l.ExecutionTimeMs),
            TotalTokenUsage = logs.Sum(l => l.TokenUsage ?? 0),
            IntentBreakdown = intentBreakdown,
            ToolBreakdown = toolBreakdown,
            RecentLogs = logs.Take(25).Select(l => new AiCopilotAuditEntryDto
            {
                Id = l.Id,
                Intent = l.Intent,
                UserMessage = l.UserMessage,
                ToolsUsed = ParseJsonList(l.ToolsUsedJson),
                ExecutionTimeMs = l.ExecutionTimeMs,
                TokenUsage = l.TokenUsage,
                Success = l.Success,
                ErrorMessage = l.ErrorMessage,
                CreatedAt = l.CreatedAt
            }).ToList()
        };
    }

    private static IReadOnlyList<string> ParseJsonList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
