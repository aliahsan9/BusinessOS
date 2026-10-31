using BusinessOS.Application.Features.Auth.DTOs;

namespace BusinessOS.Application.Common.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(string email, string password, CancellationToken cancellationToken);

    Task<AuthResponse> RegisterAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        string businessName,
        CancellationToken cancellationToken,
        string timezone = "UTC",
        string currency = "PKR",
        string industry = "General");

    Task<PasswordResetResponse> ForgotPasswordAsync(string email, CancellationToken cancellationToken);

    Task<PasswordResetResponse> ResetPasswordAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken);

    Task<AuthResponse> CreateSessionForUserAsync(
        UserAuthResult user,
        CancellationToken cancellationToken,
        string auditAction = "UserLogin");

    /// <summary>
    /// Creates a new tenant and owner for a verified external identity.
    /// The caller supplies the provider subject. Tenant id is generated here and is never taken from the client.
    /// </summary>
    Task<AuthResponse> RegisterExternalUserAsync(
        string email,
        string firstName,
        string lastName,
        string businessName,
        string loginProvider,
        string providerKey,
        string? avatarUrl,
        CancellationToken cancellationToken);
}
