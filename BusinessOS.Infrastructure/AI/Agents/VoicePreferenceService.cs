using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.Agents.Enums;
using BusinessOS.Application.Features.Agents.Services;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.AI.Agents;

public sealed class VoicePreferenceService : IVoicePreferenceService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<VoicePreferenceService> _logger;

    public VoicePreferenceService(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        ILogger<VoicePreferenceService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public Task<VoicePreferenceDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId
            ?? throw new InvalidOperationException("User context is required.");
        return GetForUserAsync(userId, cancellationToken);
    }

    public async Task<VoicePreferenceDto> GetForUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        var entity = await _context.VoicePreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.UserId == userId && v.TenantId == tenantId, cancellationToken);

        return entity is null ? DefaultDto() : Map(entity);
    }

    public Task<VoicePreferenceDto> SaveAsync(
        SaveVoicePreferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId
            ?? throw new InvalidOperationException("User context is required.");
        return SaveForUserAsync(userId, request, cancellationToken);
    }

    public async Task<VoicePreferenceDto> SaveForUserAsync(
        string userId,
        SaveVoicePreferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        var language = AgentLanguages.Normalize(request.Language);
        var voiceLanguage = AgentLanguages.ToVoiceLanguage(language);
        var agentKey = string.IsNullOrWhiteSpace(request.PreferredAgentKey)
            ? AgentKeys.Sophia
            : AgentKeys.Normalize(request.PreferredAgentKey);

        var entity = await _context.VoicePreferences
            .FirstOrDefaultAsync(v => v.UserId == userId && v.TenantId == tenantId, cancellationToken);

        if (entity is null)
        {
            entity = new VoicePreference
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = userId
            };
            _context.VoicePreferences.Add(entity);
        }

        entity.Language = voiceLanguage;
        entity.VoiceName = string.IsNullOrWhiteSpace(request.VoiceName) ? "default" : request.VoiceName.Trim();
        entity.SpeechRate = Clamp(request.SpeechRate, 0.5, 2.0, 1.0);
        entity.Pitch = Clamp(request.Pitch, 0.5, 2.0, 1.0);
        entity.ContinuousListening = request.ContinuousListening;
        entity.AutoSpeak = request.AutoSpeak;
        entity.PreferredAgentKey = agentKey;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Saved voice preferences for user {UserId}", userId);

        return Map(entity);
    }

    private static VoicePreferenceDto DefaultDto() => new()
    {
        Id = Guid.Empty,
        Language = AgentLanguages.English,
        VoiceLanguage = AgentLanguages.ToVoiceLanguage(AgentLanguages.English),
        VoiceName = "default",
        SpeechRate = 1.0,
        Pitch = 1.0,
        ContinuousListening = false,
        AutoSpeak = true,
        PreferredAgentKey = AgentKeys.Sophia
    };

    private static VoicePreferenceDto Map(VoicePreference entity) => new()
    {
        Id = entity.Id,
        Language = AgentLanguages.FromVoiceLanguage(entity.Language),
        VoiceLanguage = entity.Language,
        VoiceName = entity.VoiceName,
        SpeechRate = entity.SpeechRate,
        Pitch = entity.Pitch,
        ContinuousListening = entity.ContinuousListening,
        AutoSpeak = entity.AutoSpeak,
        PreferredAgentKey = entity.PreferredAgentKey ?? AgentKeys.Sophia
    };

    private static double Clamp(double value, double min, double max, double fallback) =>
        value is double.NaN or double.PositiveInfinity or double.NegativeInfinity
            ? fallback
            : Math.Clamp(value, min, max);
}
