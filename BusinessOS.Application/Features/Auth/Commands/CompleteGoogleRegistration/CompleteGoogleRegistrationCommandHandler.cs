using BusinessOS.Application.Features.Auth.DTOs;
using BusinessOS.Application.Features.Auth.Services;
using MediatR;

namespace BusinessOS.Application.Features.Auth.Commands.CompleteGoogleRegistration;

public sealed class CompleteGoogleRegistrationCommandHandler
    : IRequestHandler<CompleteGoogleRegistrationCommand, AuthResponse>
{
    private readonly IGoogleAuthService _googleAuthService;

    public CompleteGoogleRegistrationCommandHandler(IGoogleAuthService googleAuthService)
    {
        _googleAuthService = googleAuthService;
    }

    public Task<AuthResponse> Handle(
        CompleteGoogleRegistrationCommand request,
        CancellationToken cancellationToken) =>
        _googleAuthService.CompleteRegistrationAsync(
            request.ContinuationToken,
            request.BusinessName,
            request.FirstName,
            request.LastName,
            cancellationToken);
}
