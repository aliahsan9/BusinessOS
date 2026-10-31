using BusinessOS.Application.Features.Auth.DTOs;

namespace BusinessOS.Application.Features.Auth.Services;

public interface IAuthSessionService
{
    Task<IssuedBrowserSession> IssueAsync(
        string userId,
        Guid tenantId,
        string? deviceLabel,
        CancellationToken cancellationToken = default);

    Task<SessionRotationResult> RotateAsync(
        string rawRefreshToken,
        CancellationToken cancellationToken = default);

    Task RevokeCurrentAsync(string rawRefreshToken, CancellationToken cancellationToken = default);

    Task RevokeAllAsync(string userId, CancellationToken cancellationToken = default);
}

public sealed record IssuedBrowserSession(string RefreshToken, string CsrfToken, DateTime RefreshExpiresAt);

public sealed record SessionRotationResult(
    bool Succeeded,
    bool ReuseDetected,
    AuthResponse? Access,
    IssuedBrowserSession? Session);
