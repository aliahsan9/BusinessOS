namespace BusinessOS.Application.Features.Auth.Services;

public interface IJwtTokenGenerator
{
    string GenerateToken(
        string userId,
        string email,
        Guid tenantId,
        IReadOnlyList<string> roles,
        IReadOnlyList<string> permissions);

    DateTime GetTokenExpiration();
}
