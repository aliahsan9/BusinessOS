namespace BusinessOS.Application.Features.Onboarding.DTOs;

public sealed class OnboardingStatusDto
{
    public int CurrentStep { get; init; } = 1;
    public bool IsCompleted { get; init; }
    public bool IsSkipped { get; init; }
    public DateTime? CompletedAt { get; init; }
    public bool ShouldShowWizard { get; init; }
    public IReadOnlyList<int> CompletedSteps { get; init; } = [];
    public DateTime? LastResumedAt { get; init; }
    public int TotalSteps { get; init; } = 9;
}

public record SaveOnboardingProgressRequest(
    int CurrentStep,
    bool IsSkipped = false,
    bool ResumeLater = false,
    IReadOnlyList<int>? CompletedSteps = null);

public sealed class OnboardingBusinessProfileDto
{
    public string Name { get; init; } = default!;
    public string? LogoUrl { get; init; }
    public string? Website { get; init; }
    public string Industry { get; init; } = default!;
    public string? Description { get; init; }
    public string? Phone { get; init; }
    public string? Address { get; init; }
    public string Currency { get; init; } = "PKR";
    public string Timezone { get; init; } = "Asia/Karachi";
    public int FinancialYearStartMonth { get; init; } = 7;
}

public record SaveOnboardingBusinessProfileRequest(
    string Name,
    string? LogoUrl,
    string? Website,
    string Industry,
    string? Description,
    string? Phone,
    string? Address,
    string Currency,
    string Timezone,
    int FinancialYearStartMonth = 7);
