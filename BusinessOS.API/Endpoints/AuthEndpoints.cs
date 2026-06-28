using BusinessOS.Application.Features.Auth.Commands.Login;
using BusinessOS.Application.Features.Auth.Commands.Register;
using BusinessOS.Application.Features.Auth.DTOs;
using MediatR;

namespace BusinessOS.API.Endpoints;

/// <summary>
/// Authentication endpoints for registration and login.
/// </summary>
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
            .WithDescription("Creates a new tenant, user account, and returns a JWT for immediate API access.")
            .Produces<AuthResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/login", Login)
            .WithName("Login")
            .WithSummary("Authenticate and receive a JWT")
            .WithDescription("Validates credentials and returns a bearer token including the tenant claim.")
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> Register(
        RegisterCommand command,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return Results.Created("/api/auth/login", result);
    }

    private static async Task<IResult> Login(
        LoginCommand command,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return Results.Ok(result);
    }
}
