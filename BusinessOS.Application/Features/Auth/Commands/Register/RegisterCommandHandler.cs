using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Auth.DTOs;
using MediatR;

namespace BusinessOS.Application.Features.Auth.Commands.Register;

public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResponse>
{
    private readonly IAuthService _authService;

    public RegisterCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public Task<AuthResponse> Handle(
        RegisterCommand request,
        CancellationToken cancellationToken) =>
        _authService.RegisterAsync(
            request.Email,
            request.Password,
            request.FirstName,
            request.LastName,
            request.BusinessName,
            cancellationToken);
}
