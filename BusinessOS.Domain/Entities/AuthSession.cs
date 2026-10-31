namespace BusinessOS.Domain.Entities;

/// <summary>
/// One browser or device session. Only a hash of the refresh token is stored.
/// </summary>
public class AuthSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = default!;

    public Guid TenantId { get; set; }

    public Guid FamilyId { get; set; }

    public string TokenHash { get; set; } = default!;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? RevokedAt { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public Guid? ReplacedBySessionId { get; set; }

    public string? DeviceLabel { get; set; }

    public bool ReuseDetected { get; set; }
}
