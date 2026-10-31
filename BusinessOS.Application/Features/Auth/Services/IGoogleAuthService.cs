using BusinessOS.Application.Features.Auth.DTOs;

namespace BusinessOS.Application.Features.Auth.Services;

public interface IGoogleAuthService
{
    Task<GoogleSignInResponse> SignInAsync(string idToken, CancellationToken cancellationToken);

    Task<AuthResponse> CompleteRegistrationAsync(
        string continuationToken,
        string businessName,
        string firstName,
        string lastName,
        CancellationToken cancellationToken);

    Task LinkAsync(string userId, string idToken, CancellationToken cancellationToken);

    Task UnlinkAsync(string userId, CancellationToken cancellationToken);
}
