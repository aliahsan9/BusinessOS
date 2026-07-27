using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Account.DTOs;

namespace BusinessOS.API.Endpoints;

/// <summary>
/// Authenticated self-service account endpoints for the current user.
/// </summary>
public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/account")
            .WithTags("Account")
            .RequireAuthorization();

        group.MapGet("/me", GetMyProfile)
            .WithName("GetMyProfile")
            .WithSummary("Get the current user's profile")
            .Produces<AccountProfileResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/me", UpdateMyProfile)
            .WithName("UpdateMyProfile")
            .WithSummary("Update the current user's profile")
            .Produces<AccountProfileResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/change-password", ChangePassword)
            .WithName("ChangeMyPassword")
            .WithSummary("Change the current user's password")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> GetMyProfile(
        ICurrentUserService currentUser,
        IIdentityService identityService,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId(currentUser);
        var profile = await identityService.GetAccountProfileAsync(userId, cancellationToken);
        return Results.Ok(profile);
    }

    private static async Task<IResult> UpdateMyProfile(
        UpdateAccountProfileRequest request,
        ICurrentUserService currentUser,
        IIdentityService identityService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName) ||
            string.IsNullOrWhiteSpace(request.Email))
        {
            return Results.BadRequest(new { detail = "First name, last name, and email are required." });
        }

        var userId = RequireUserId(currentUser);
        var profile = await identityService.UpdateAccountProfileAsync(userId, request, cancellationToken);
        return Results.Ok(profile);
    }

    private static async Task<IResult> ChangePassword(
        ChangePasswordRequest request,
        ICurrentUserService currentUser,
        IIdentityService identityService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
            string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return Results.BadRequest(new { detail = "Current password and new password are required." });
        }

        if (request.NewPassword.Length < 8)
        {
            return Results.BadRequest(new { detail = "New password must be at least 8 characters." });
        }

        var userId = RequireUserId(currentUser);
        var result = await identityService.ChangePasswordAsync(
            userId,
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);

        if (!result.Succeeded)
        {
            return Results.BadRequest(new { detail = string.Join("; ", result.Errors), errors = result.Errors });
        }

        return Results.NoContent();
    }

    private static string RequireUserId(ICurrentUserService currentUser) =>
        currentUser.UserId
        ?? throw new UnauthorizedException("You must be signed in to access your account.");
}
