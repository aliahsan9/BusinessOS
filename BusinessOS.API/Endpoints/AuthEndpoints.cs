using System.Security.Claims;
using BusinessOS.API.OpenApi;
using BusinessOS.API.Security;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Auth.Commands.CompleteGoogleRegistration;
using BusinessOS.Application.Features.Auth.Commands.ForgotPassword;
using BusinessOS.Application.Features.Auth.Commands.GoogleSignIn;
using BusinessOS.Application.Features.Auth.Commands.Login;
using BusinessOS.Application.Features.Auth.Commands.Register;
using BusinessOS.Application.Features.Auth.Commands.ResetPassword;
using BusinessOS.Application.Features.Auth.DTOs;
using BusinessOS.Application.Features.Auth.Google;
using BusinessOS.Application.Features.Auth.Services;
using BusinessOS.Application.Features.Team.DTOs;
using BusinessOS.Application.Features.Team.Services;
using MediatR;

namespace BusinessOS.API.Endpoints;

/// <summary>
/// Authentication endpoints for registration, login, password reset, and team invitation acceptance.
/// </summary>
/// <remarks>
/// Public endpoints under <c>/api/v1/auth</c>. No JWT is required except where noted.
/// Registration creates a new tenant and owner user; the response includes a JWT for immediate API access.
/// Team invitation preview and acceptance are unauthenticated flows using emailed tokens.
/// </remarks>
public static class AuthEndpoints
{
    /// <summary>
    /// Maps authentication endpoints under <c>/api/v1/auth</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth")
            .WithTags("Authentication")
            .RequireRateLimiting("auth");

        group.MapPost("/register", Register)
            .WithName("Register")
            .WithSummary("Register a new user and tenant")
            .WithDescription(
                EndpointDocumentation.WithExampleResponse(
                    EndpointDocumentation.WithExampleRequest(
                        EndpointDocumentation.WithBusinessRules(
                            EndpointDocumentation.WithStatusCodes(
                                "Creates a new tenant, owner user account, and returns a JWT for immediate API access.\n\n" +
                                "**Permission:** Not required (public).\n\n" +
                                EndpointDocumentation.PublicNote,
                                (400, "Validation failed — invalid email, weak password, or missing required fields."),
                                (409, "Conflict — email is already registered."),
                                (500, "Internal server error.")),
                            "Email must be unique across the platform.",
                            "Password must meet Identity password policy requirements.",
                            "Business name becomes the new tenant display name."),
                        "POST", "/api/v1/auth/register",
                        """
                        {
                          "email": "owner@acme.com",
                          "password": "SecurePass123!",
                          "firstName": "Jane",
                          "lastName": "Owner",
                          "businessName": "Acme Trading Co."
                        }
                        """),
                    """
                    {
                      "token": "eyJhbGciOiJIUzI1NiIs...",
                      "expiresAt": "2026-08-07T12:00:00Z",
                      "userId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
                      "tenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                      "email": "owner@acme.com"
                    }
                    """))
            .Produces<AuthResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/login", Login)
            .WithName("Login")
            .WithSummary("Authenticate and receive a JWT")
            .WithDescription(
                EndpointDocumentation.WithExampleResponse(
                    EndpointDocumentation.WithExampleRequest(
                        EndpointDocumentation.WithBusinessRules(
                            EndpointDocumentation.WithStatusCodes(
                                "Validates credentials and returns a bearer token including the tenant claim.\n\n" +
                                "**Permission:** Not required (public).\n\n" +
                                EndpointDocumentation.PublicNote, 
                                (400, "Validation failed — missing email or password."),
                                (401, "Unauthorized — invalid credentials or inactive account."),
                                (500, "Internal server error.")),
                            "Use the returned token in the Authorization header as Bearer {token}.",
                            "Include X-Tenant-ID header on subsequent tenant-scoped requests."),
                        "POST", "/api/v1/auth/login",
                        """
                        {
                          "email": "owner@acme.com",
                          "password": "SecurePass123!"
                        }
                        """),
                    """
                    {
                      "token": "eyJhbGciOiJIUzI1NiIs...",
                      "expiresAt": "2026-08-07T12:00:00Z",
                      "userId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
                      "tenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                      "email": "owner@acme.com"
                    }
                    """))
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/forgot-password", ForgotPassword)
            .WithName("ForgotPassword")
            .WithSummary("Request a password reset email")
            .WithDescription(
                EndpointDocumentation.WithExampleRequest(
                    EndpointDocumentation.WithBusinessRules(
                        EndpointDocumentation.WithStatusCodes(
                            "Always returns a generic success message to avoid email enumeration. Sends a reset link when the account exists and is active.\n\n" +
                            "**Permission:** Not required (public).\n\n" +
                            EndpointDocumentation.PublicNote,
                            (400, "Validation failed — invalid email format."),
                            (500, "Internal server error.")),
                        "Response is identical whether or not the email exists (anti-enumeration).",
                        "Reset link expires per Identity token lifetime settings."),
                    "POST", "/api/v1/auth/forgot-password",
                    """{ "email": "owner@acme.com" }"""))
            .Produces<PasswordResetResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/reset-password", ResetPassword)
            .WithName("ResetPassword")
            .WithSummary("Reset password with emailed token")
            .WithDescription(
                EndpointDocumentation.WithExampleRequest(
                    EndpointDocumentation.WithBusinessRules(
                        EndpointDocumentation.WithStatusCodes(
                            "Validates the Identity password-reset token and sets a new password.\n\n" +
                            "**Permission:** Not required (public).\n\n" +
                            EndpointDocumentation.PublicNote,
                            (400, "Validation failed — invalid or expired token, or weak password."),
                            (500, "Internal server error.")),
                        "Token is obtained from the password reset email link.",
                        "New password must meet Identity password policy."),
                    "POST", "/api/v1/auth/reset-password",
                    """
                    {
                      "email": "owner@acme.com",
                      "token": "CfDJ8N...",
                      "newPassword": "NewSecurePass456!"
                    }
                    """))
            .Produces<PasswordResetResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/google", GoogleSignIn)
            .WithName("GoogleSignIn")
            .WithSummary("Sign in or start sign-up with a Google ID token")
            .WithDescription(
                "Validates a Google ID token and either issues a BusinessOS session or returns a short-lived continuation token so a new workspace can be created. Does not link an existing password account automatically.")
            .Produces<GoogleSignInResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/google/complete", CompleteGoogleRegistration)
            .WithName("CompleteGoogleRegistration")
            .WithSummary("Finish Google sign-up by creating a tenant")
            .WithDescription(
                "Creates a new tenant and owner from a server-issued Google continuation token. The Google subject and email come from that token, not from client-supplied identity fields.")
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/invitation/{token}", GetInvitationPreview)
            .WithName("GetInvitationPreview")
            .WithSummary("Preview a team invitation")
            .WithDescription(
                EndpointDocumentation.WithExampleResponse(
                    EndpointDocumentation.WithStatusCodes(
                        "Returns invitation details (team name, role, inviter) for display before acceptance.\n\n" +
                        "**Permission:** Not required (public).\n\n" +
                        EndpointDocumentation.PublicNote,
                        (404, "Not found — invitation token is invalid or expired."),
                        (500, "Internal server error.")),
                    """
                    {
                      "email": "newmember@acme.com",
                      "roleName": "Sales Manager",
                      "tenantName": "Acme Trading Co.",
                      "invitedBy": "Jane Owner",
                      "expiresAt": "2026-08-13T00:00:00Z"
                    }
                    """))
            .Produces<InvitationPreviewDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/accept-invitation", AcceptInvitation)
            .WithName("AcceptInvitation")
            .WithSummary("Accept a team invitation")
            .WithDescription(
                EndpointDocumentation.WithExampleRequest(
                    EndpointDocumentation.WithBusinessRules(
                        EndpointDocumentation.WithStatusCodes(
                            "Accepts a pending team invitation and creates or links the user account to the tenant.\n\n" +
                            "**Permission:** Not required (public).\n\n" +
                            EndpointDocumentation.PublicNote,
                            (400, "Validation failed — invalid token or missing required profile fields."),
                            (404, "Not found — invitation token is invalid or expired."),
                            (500, "Internal server error.")),
                        "Invitation must not be expired or already accepted.",
                        "New users must provide password and name; existing users link by token only."),
                    "POST", "/api/v1/auth/accept-invitation",
                    """
                    {
                      "token": "inv_abc123...",
                      "firstName": "John",
                      "lastName": "Member",
                      "password": "SecurePass123!"
                    }
                    """))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/refresh", Refresh)
            .WithName("RefreshSession")
            .WithSummary("Rotate the refresh cookie and issue a new access token")
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/logout", Logout)
            .WithName("Logout")
            .WithSummary("Revoke the current refresh session")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/logout-all", LogoutAll)
            .WithName("LogoutAll")
            .WithSummary("Revoke every session for the authenticated user")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }

    /// <summary>Creates a new tenant and user; returns JWT on success.</summary>
    /// <param name="command">Registration payload with email, password, name, and business name.</param>
    /// <param name="sender">Mediator sender for handling the command.</param>
    /// <param name="sessions">Session service that issues the refresh cookie.</param>
    /// <param name="httpContext">Current HTTP context that receives the refresh cookie.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>201 Created with <see cref="AuthResponse"/> at login URL.</returns>
    private static async Task<IResult> Register(
        RegisterCommand command,
        ISender sender,
        IAuthSessionService sessions,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        await AuthCookieWriter.IssueAsync(httpContext, sessions, result.UserId, result.TenantId, cancellationToken);
        return Results.Created("/api/v1/auth/login", result);
    }

    /// <summary>Authenticates user credentials and returns a JWT.</summary>
    /// <param name="command">Login payload with email and password.</param>
    /// <param name="sender">Mediator sender for handling the command.</param>
    /// <param name="sessions">Session service that issues the refresh cookie.</param>
    /// <param name="httpContext">Current HTTP context that receives the refresh cookie.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with <see cref="AuthResponse"/>.</returns>
    private static async Task<IResult> Login(
        LoginCommand command,
        ISender sender,
        IAuthSessionService sessions,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        await AuthCookieWriter.IssueAsync(httpContext, sessions, result.UserId, result.TenantId, cancellationToken);
        return Results.Ok(result);
    }

    /// <summary>Initiates password reset flow via email.</summary>
    /// <param name="command">Forgot-password payload with email address.</param>
    /// <param name="sender">Mediator sender for handling the command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with generic success message.</returns>
    private static async Task<IResult> ForgotPassword(
        ForgotPasswordCommand command,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return Results.Ok(result);
    }

    /// <summary>Completes password reset using emailed token.</summary>
    /// <param name="command">Reset payload with email, token, and new password.</param>
    /// <param name="sender">Mediator sender for handling the command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK on successful password change.</returns>
    private static async Task<IResult> ResetPassword(
        ResetPasswordCommand command,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GoogleSignIn(
        GoogleSignInCommand command,
        ISender sender,
        IAuthSessionService sessions,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        if (result.Status == GoogleSignInStatus.Authenticated && result.Auth is not null)
        {
            await AuthCookieWriter.IssueAsync(
                httpContext,
                sessions,
                result.Auth.UserId,
                result.Auth.TenantId,
                cancellationToken);
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> CompleteGoogleRegistration(
        CompleteGoogleRegistrationCommand command,
        ISender sender,
        IAuthSessionService sessions,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        await AuthCookieWriter.IssueAsync(
            httpContext,
            sessions,
            result.UserId,
            result.TenantId,
            cancellationToken);
        return Results.Ok(result);
    }

    /// <summary>Previews a team invitation by token.</summary>
    /// <param name="token">Invitation token from the email link.</param>
    /// <param name="teamService">Team service for handling invitation preview.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with <see cref="InvitationPreviewDto"/>.</returns>
    private static async Task<IResult> GetInvitationPreview(
        string token,
        ITeamService teamService,
        CancellationToken cancellationToken)
    {
        var result = await teamService.GetInvitationPreviewAsync(token, cancellationToken);
        return Results.Ok(result);
    }

    /// <summary>Accepts a team invitation and joins the tenant.</summary>
    /// <param name="request">Acceptance payload with token and optional profile fields.</param>
    /// <param name="teamService">Team service for handling invitation acceptance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    private static async Task<IResult> AcceptInvitation(
        AcceptInvitationRequest request,
        ITeamService teamService,
        CancellationToken cancellationToken)
    {
        await teamService.AcceptInvitationAsync(request, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> Refresh(
        HttpContext httpContext,
        IAuthSessionService sessions,
        CancellationToken cancellationToken)
    {
        if (!AuthCookieWriter.CsrfMatches(httpContext.Request))
        {
            return Results.Problem(
                title: "CSRF validation failed.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (!httpContext.Request.Cookies.TryGetValue(AuthCookieWriter.RefreshCookieName, out var refreshToken))
        {
            return Results.Unauthorized();
        }

        var rotation = await sessions.RotateAsync(refreshToken, cancellationToken);
        if (!rotation.Succeeded || rotation.Access is null || rotation.Session is null)
        {
            AuthCookieWriter.Clear(httpContext);
            return Results.Unauthorized();
        }

        AuthCookieWriter.Write(httpContext, rotation.Session);
        return Results.Ok(rotation.Access);
    }

    private static async Task<IResult> Logout(
        HttpContext httpContext,
        IAuthSessionService sessions,
        CancellationToken cancellationToken)
    {
        if (!AuthCookieWriter.CsrfMatches(httpContext.Request))
        {
            return Results.Problem(
                title: "CSRF validation failed.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (httpContext.Request.Cookies.TryGetValue(AuthCookieWriter.RefreshCookieName, out var refreshToken))
        {
            await sessions.RevokeCurrentAsync(refreshToken, cancellationToken);
        }

        AuthCookieWriter.Clear(httpContext);
        return Results.NoContent();
    }

    private static async Task<IResult> LogoutAll(
        HttpContext httpContext,
        ClaimsPrincipal user,
        IAuthSessionService sessions,
        IAuthorizationVersionService versions,
        ITenantMembershipLookup membershipLookup,
        CancellationToken cancellationToken)
    {
        if (!AuthCookieWriter.CsrfMatches(httpContext.Request))
        {
            return Results.Problem(
                title: "CSRF validation failed.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.Unauthorized();
        }

        var membership = await membershipLookup.FindByUserIdAsync(userId, cancellationToken);
        var versionClaim = user.FindFirst(ClaimTypesConstants.SecurityVersion)?.Value;
        if (membership is null ||
            !membership.IsActive ||
            !int.TryParse(versionClaim, out var tokenVersion) ||
            tokenVersion != membership.SecurityVersion)
        {
            return Results.Unauthorized();
        }

        await sessions.RevokeAllAsync(userId, cancellationToken);
        await versions.BumpUserAsync(userId, cancellationToken);
        AuthCookieWriter.Clear(httpContext);
        return Results.NoContent();
    }
}
