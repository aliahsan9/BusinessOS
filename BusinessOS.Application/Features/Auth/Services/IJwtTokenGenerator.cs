namespace BusinessOS.Application.Features.Auth.Services;

public interface IJwtTokenGenerator
{
    string GenerateToken(string userId, string email, IList<string> roles);
}
