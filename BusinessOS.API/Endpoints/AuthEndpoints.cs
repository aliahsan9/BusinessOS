using BusinessOS.API.OpenApi;
using BusinessOS.Application.Features.Auth.Commands.ForgotPassword;
using BusinessOS.Application.Features.Auth.Commands.Login;
using BusinessOS.Application.Features.Auth.Commands.Register;
using BusinessOS.Application.Features.Auth.Commands.ResetPassword;
using BusinessOS.Application.Features.Auth.DTOs;
using BusinessOS.Application.Features.Team.DTOs;
using BusinessOS.Application.Features.Team.Services;
using MediatR;

namespace BusinessOS.API.Endpoints;

/// <summary>
/// Authentication endpoints for registration, login, password reset, and team invitation acceptance.
/// </summary>
/// <remarks>
/// Public endpoints under <c>/api/auth</c>. No JWT is required except where noted.
/// Registration creates a new tenant and owner user; the response includes a JWT for immediate API access.
/// Team invitation preview and acceptance are unauthenticated flows using emailed tokens.
/// </remarks>
public static class AuthEndpoints
{
    /// <summary>
    /// Maps authentication endpoints under <c>/api/auth</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication");

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
                        "POST", "/api/auth/register",
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
                        "POST", "/api/auth/login",
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
                    "POST", "/api/auth/forgot-password",
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
                    "POST", "/api/auth/reset-password",
                    """
                    {
                      "email": "owner@acme.com",
                      "token": "CfDJ8N...",
                      "newPassword": "NewSecurePass456!"
                    }
                    """))
            .Produces<PasswordResetResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

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
                    "POST", "/api/auth/accept-invitation",
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

        return app;
    }

    /// <summary>Creates a new tenant and user; returns JWT on success.</summary>
    /// <param name="command">Registration payload with email, password, name, and business name.</param>
    /// <returns>201 Created with <see cref="AuthResponse"/> at login URL.</returns>
    private static async Task<IResult> Register(
        RegisterCommand command,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return Results.Created("/api/auth/login", result);
    }

    /// <summary>Authenticates user credentials and returns a JWT.</summary>
    /// <param name="command">Login payload with email and password.</param>
    /// <returns>200 OK with <see cref="AuthResponse"/>.</returns>
    private static async Task<IResult> Login(
        LoginCommand command,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return Results.Ok(result);
    }

    /// <summary>Initiates password reset flow via email.</summary>
    /// <param name="command">Forgot-password payload with email address.</param>
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
    /// <returns>200 OK on successful password change.</returns>
    private static async Task<IResult> ResetPassword(
        ResetPasswordCommand command,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return Results.Ok(result);
    }

    /// <summary>Previews a team invitation by token.</summary>
    /// <param name="token">Invitation token from the email link.</param>
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
    /// <returns>204 No Content on success.</returns>
    private static async Task<IResult> AcceptInvitation(
        AcceptInvitationRequest request,
        ITeamService teamService,
        CancellationToken cancellationToken)
    {
        await teamService.AcceptInvitationAsync(request, cancellationToken);
        return Results.NoContent();
    }
}
