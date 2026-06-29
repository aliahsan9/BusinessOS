using BusinessOS.Application.Features.Onboarding.DTOs;

namespace BusinessOS.Application.Features.Onboarding.Services;

public interface IOnboardingService
{
    Task<OnboardingStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<OnboardingStatusDto> SaveProgressAsync(
        SaveOnboardingProgressRequest request,
        CancellationToken cancellationToken = default);

    Task<OnboardingStatusDto> CompleteAsync(CancellationToken cancellationToken = default);

    Task<OnboardingBusinessProfileDto> GetBusinessProfileAsync(
        CancellationToken cancellationToken = default);

    Task SaveBusinessProfileAsync(
        SaveOnboardingBusinessProfileRequest request,
        CancellationToken cancellationToken = default);
}
