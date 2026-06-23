namespace BusinessOS.Application.Features.Auth.DTOs;

public sealed class AuthResponse
{
    public string Token { get; set; } = default!;

    public string UserId { get; set; } = default!;

    public string Email { get; set; } = default!;

    public DateTime ExpiresAt { get; set; }
}
