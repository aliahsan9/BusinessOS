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
}
