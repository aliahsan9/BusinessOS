using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Account.DTOs;
using BusinessOS.Application.Features.Auth.Services;

namespace BusinessOS.API.Endpoints;

/// <summary>
/// Authenticated self-service account endpoints for the current user.
/// </summary>
public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/account")
            .WithTags("Account")
            .RequireAuthorization()
            .RequireRateLimiting("api");

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

        group.MapGet("/external-logins", GetExternalLogins)
            .WithName("GetExternalLogins")
            .WithSummary("List connected sign-in methods")
            .Produces<ExternalLoginsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/external-logins/google", LinkGoogle)
            .WithName("LinkGoogle")
            .WithSummary("Connect Google to the current account")
            .Produces<ExternalLoginsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/external-logins/google", UnlinkGoogle)
            .WithName("UnlinkGoogle")
            .WithSummary("Disconnect Google when a local password exists")
            .Produces<ExternalLoginsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/password", SetPassword)
            .WithName("SetMyPassword")
            .WithSummary("Add a local password to an account that does not have one")
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

        if (request.NewPassword.Length < 8 || request.NewPassword.Length > 128)
        {
            return Results.BadRequest(new { detail = "New password must be between 8 and 128 characters." });
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

    private static async Task<IResult> GetExternalLogins(
        ICurrentUserService currentUser,
        IIdentityService identityService,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId(currentUser);
        var status = await identityService.GetExternalLoginsAsync(userId, cancellationToken);
        return Results.Ok(status);
    }

    private static async Task<IResult> LinkGoogle(
        LinkGoogleRequest request,
        ICurrentUserService currentUser,
        IGoogleAuthService googleAuth,
        IIdentityService identityService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken) || request.IdToken.Length > 8192)
        {
            return Results.BadRequest(new { detail = "Google sign-in could not be completed." });
        }

        var userId = RequireUserId(currentUser);
        await googleAuth.LinkAsync(userId, request.IdToken, cancellationToken);
        var status = await identityService.GetExternalLoginsAsync(userId, cancellationToken);
        return Results.Ok(status);
    }

    private static async Task<IResult> UnlinkGoogle(
        ICurrentUserService currentUser,
        IGoogleAuthService googleAuth,
        IIdentityService identityService,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId(currentUser);
        await googleAuth.UnlinkAsync(userId, cancellationToken);
        var status = await identityService.GetExternalLoginsAsync(userId, cancellationToken);
        return Results.Ok(status);
    }

    private static async Task<IResult> SetPassword(
        SetPasswordRequest request,
        ICurrentUserService currentUser,
        IIdentityService identityService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) ||
            request.NewPassword.Length < 8 ||
            request.NewPassword.Length > 128)
        {
            return Results.BadRequest(new { detail = "New password must be between 8 and 128 characters." });
        }

        var userId = RequireUserId(currentUser);
        var result = await identityService.SetPasswordAsync(userId, request.NewPassword, cancellationToken);
        if (!result.Succeeded)
        {
            var safeErrors = result.Errors
                .Where(error => error.Contains("Password", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            return Results.BadRequest(new
            {
                detail = safeErrors.Length > 0
                    ? string.Join("; ", safeErrors)
                    : "A password could not be added to this account."
            });
        }

        return Results.NoContent();
    }

    private static string RequireUserId(ICurrentUserService currentUser) =>
        currentUser.UserId
        ?? throw new UnauthorizedException("You must be signed in to access your account.");
}
