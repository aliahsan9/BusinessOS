using BusinessOS.Application.Features.Auth.Commands.Login;
using BusinessOS.Application.Features.Auth.Commands.Register;
using MediatR;

namespace BusinessOS.API.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        group.MapPost(
            "/register",
            async (
                RegisterCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    command,
                    cancellationToken);

                return Results.Ok(result);
            })
            .WithName("Register")
            .WithDescription("Register a new user");

        group.MapPost(
            "/login",
            async (
                LoginCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    command,
                    cancellationToken);

                return Results.Ok(result);
            })
            .WithName("Login")
            .WithDescription("Login user");

        return app;
    }
}
