namespace BusinessOS.Application.Features.Onboarding.DTOs;

public sealed class OnboardingStatusDto
{
    public int CurrentStep { get; init; } = 1;
    public bool IsCompleted { get; init; }
    public bool IsSkipped { get; init; }
    public DateTime? CompletedAt { get; init; }
    public bool ShouldShowWizard { get; init; }
}

public record SaveOnboardingProgressRequest(int CurrentStep, bool IsSkipped = false);

public sealed class OnboardingBusinessProfileDto
{
    public string Name { get; init; } = default!;
    public string? LogoUrl { get; init; }
    public string? Website { get; init; }
    public string Industry { get; init; } = default!;
    public string? Description { get; init; }
    public string Currency { get; init; } = "USD";
    public string Timezone { get; init; } = "UTC";
}

public record SaveOnboardingBusinessProfileRequest(
    string Name,
    string? LogoUrl,
    string? Website,
    string Industry,
    string? Description,
    string Currency,
    string Timezone);
