using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Auth.DTOs;
using MediatR;

namespace BusinessOS.Application.Features.Auth.Commands.Login;

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponse>
{
    private readonly IAuthService _authService;

    public LoginCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public Task<AuthResponse> Handle(
        LoginCommand request,
        CancellationToken cancellationToken) =>
        _authService.LoginAsync(request.Email, request.Password, cancellationToken);
}
