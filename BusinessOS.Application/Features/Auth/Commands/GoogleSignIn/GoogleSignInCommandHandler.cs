using BusinessOS.Application.Features.Auth.DTOs;
using BusinessOS.Application.Features.Auth.Services;
using MediatR;

namespace BusinessOS.Application.Features.Auth.Commands.GoogleSignIn;

public sealed class GoogleSignInCommandHandler : IRequestHandler<GoogleSignInCommand, GoogleSignInResponse>
{
    private readonly IGoogleAuthService _googleAuthService;

    public GoogleSignInCommandHandler(IGoogleAuthService googleAuthService)
    {
        _googleAuthService = googleAuthService;
    }

    public Task<GoogleSignInResponse> Handle(
        GoogleSignInCommand request,
        CancellationToken cancellationToken) =>
        _googleAuthService.SignInAsync(request.IdToken, cancellationToken);
}
