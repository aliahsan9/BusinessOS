using BusinessOS.Application.Features.Onboarding.DTOs;
using BusinessOS.Application.Features.Onboarding.Services;
using BusinessOS.API.Authorization;

namespace BusinessOS.API.Endpoints;

public static class OnboardingEndpoints
{
    public static void MapOnboardingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/onboarding")
            .WithTags("Onboarding")
            .RequireAuthorization();

        group.MapGet("/status", GetStatus)
            .WithName("GetOnboardingStatus")
            .Produces<OnboardingStatusDto>(StatusCodes.Status200OK);

        group.MapPost("/save-progress", SaveProgress)
            .WithName("SaveOnboardingProgress")
            .Produces<OnboardingStatusDto>(StatusCodes.Status200OK);

        group.MapPost("/complete", Complete)
            .WithName("CompleteOnboarding")
            .Produces<OnboardingStatusDto>(StatusCodes.Status200OK);

        group.MapGet("/business-profile", GetBusinessProfile)
            .WithName("GetOnboardingBusinessProfile")
            .Produces<OnboardingBusinessProfileDto>(StatusCodes.Status200OK);

        group.MapPut("/business-profile", SaveBusinessProfile)
            .WithName("SaveOnboardingBusinessProfile")
            .Produces(StatusCodes.Status204NoContent);
    }

    private static async Task<IResult> GetStatus(
        IOnboardingService onboardingService,
        CancellationToken cancellationToken)
    {
        var result = await onboardingService.GetStatusAsync(cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> SaveProgress(
        SaveOnboardingProgressRequest request,
        IOnboardingService onboardingService,
        CancellationToken cancellationToken)
    {
        var result = await onboardingService.SaveProgressAsync(request, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> Complete(
        IOnboardingService onboardingService,
        CancellationToken cancellationToken)
    {
        var result = await onboardingService.CompleteAsync(cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetBusinessProfile(
        IOnboardingService onboardingService,
        CancellationToken cancellationToken)
    {
        var result = await onboardingService.GetBusinessProfileAsync(cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> SaveBusinessProfile(
        SaveOnboardingBusinessProfileRequest request,
        IOnboardingService onboardingService,
        CancellationToken cancellationToken)
    {
        await onboardingService.SaveBusinessProfileAsync(request, cancellationToken);
        return Results.NoContent();
    }
}
