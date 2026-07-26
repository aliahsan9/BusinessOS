using BusinessOS.Application.Features.Agents.DTOs;

namespace BusinessOS.Application.Features.Agents.Services;

/// <summary>
/// Loads and persists per-user voice preferences for AI employees.
/// </summary>
public interface IVoicePreferenceService
{
    Task<VoicePreferenceDto> GetAsync(
        CancellationToken cancellationToken = default);

    Task<VoicePreferenceDto> GetForUserAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<VoicePreferenceDto> SaveAsync(
        SaveVoicePreferenceRequest request,
        CancellationToken cancellationToken = default);

    Task<VoicePreferenceDto> SaveForUserAsync(
        string userId,
        SaveVoicePreferenceRequest request,
        CancellationToken cancellationToken = default);
}
