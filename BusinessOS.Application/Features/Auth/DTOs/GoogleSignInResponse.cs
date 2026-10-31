namespace BusinessOS.Application.Features.Auth.DTOs;

public sealed class GoogleSignInResponse
{
    public required string Status { get; init; }

    public AuthResponse? Auth { get; init; }

    public string? ContinuationToken { get; init; }

    public string? Email { get; init; }

    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    public string? Message { get; init; }
}
