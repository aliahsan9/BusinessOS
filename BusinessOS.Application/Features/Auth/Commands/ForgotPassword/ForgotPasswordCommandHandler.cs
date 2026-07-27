using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Auth.DTOs;
using MediatR;

namespace BusinessOS.Application.Features.Auth.Commands.ForgotPassword;

public sealed class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, PasswordResetResponse>
{
    private readonly IAuthService _authService;

    public ForgotPasswordCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public Task<PasswordResetResponse> Handle(
        ForgotPasswordCommand request,
        CancellationToken cancellationToken) =>
        _authService.ForgotPasswordAsync(request.Email, cancellationToken);
}
