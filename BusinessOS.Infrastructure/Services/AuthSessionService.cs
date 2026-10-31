using System.Security.Cryptography;
using System.Text;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Auth.DTOs;
using BusinessOS.Application.Features.Auth.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.Services;

public sealed class AuthSessionOptions
{
    public const string SectionName = "Auth";

    public int RefreshTokenDays { get; set; } = 14;

    public int RefreshReuseGraceSeconds { get; set; } = 15;
}

public sealed class AuthSessionService : IAuthSessionService
{
    private static readonly SemaphoreSlim[] Stripes = Enumerable.Range(0, 32)
        .Select(_ => new SemaphoreSlim(1, 1))
        .ToArray();

    private readonly IDbContextFactory<BusinessOSDbContext> _dbContextFactory;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRoleRepository _roleRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly AuthSessionOptions _options;

    public AuthSessionService(
        IDbContextFactory<BusinessOSDbContext> dbContextFactory,
        UserManager<ApplicationUser> userManager,
        IRoleRepository roleRepository,
        IJwtTokenGenerator jwtTokenGenerator,
        IOptions<AuthSessionOptions> options)
    {
        _dbContextFactory = dbContextFactory;
        _userManager = userManager;
        _roleRepository = roleRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
        _options = options.Value;
    }

    public async Task<IssuedBrowserSession> IssueAsync(
        string userId,
        Guid tenantId,
        string? deviceLabel,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var raw = CreateSecret();
        var session = new AuthSession
        {
            UserId = userId,
            TenantId = tenantId,
            FamilyId = Guid.NewGuid(),
            TokenHash = Hash(raw),
            CreatedAt = now,
            ExpiresAt = now.AddDays(RefreshDays),
            DeviceLabel = NormalizeDevice(deviceLabel)
        };

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.AuthSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        return new IssuedBrowserSession(raw, CreateSecret(), session.ExpiresAt);
    }

    public async Task<SessionRotationResult> RotateAsync(
        string rawRefreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return Failed();
        }

        var hash = Hash(rawRefreshToken);
        var gate = Stripes[Math.Abs(hash.GetHashCode(StringComparison.Ordinal)) % Stripes.Length];
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await RotateCoreAsync(hash, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task RevokeCurrentAsync(string rawRefreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return;
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var hash = Hash(rawRefreshToken);
        var session = await db.AuthSessions.FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (session is null || session.RevokedAt is not null)
        {
            return;
        }

        session.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var sessions = await db.AuthSessions
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            session.RevokedAt = now;
        }

        if (sessions.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<SessionRotationResult> RotateCoreAsync(string hash, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var session = await db.AuthSessions.FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (session is null)
        {
            return Failed();
        }

        if (session.RevokedAt is not null)
        {
            var reuse = session.ReplacedBySessionId is not null &&
                        session.RevokedAt.Value.AddSeconds(GraceSeconds) < now;
            if (reuse)
            {
                await RevokeFamilyAsync(db, session.FamilyId, now, cancellationToken);
            }

            return Failed(reuse);
        }

        if (session.ExpiresAt <= now)
        {
            session.RevokedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            return Failed();
        }

        var user = await _userManager.FindByIdAsync(session.UserId);
        if (user is null || !user.IsActive)
        {
            await RevokeFamilyAsync(db, session.FamilyId, now, cancellationToken);
            return Failed();
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return Failed();
        }

        if (!await TryClaimAsync(db, session, now, cancellationToken))
        {
            var current = await db.AuthSessions.AsNoTracking()
                .FirstAsync(x => x.Id == session.Id, cancellationToken);
            var reuse = current.ReplacedBySessionId is not null &&
                        current.RevokedAt is not null &&
                        current.RevokedAt.Value.AddSeconds(GraceSeconds) < now;
            if (reuse)
            {
                await RevokeFamilyAsync(db, current.FamilyId, now, cancellationToken);
            }

            return Failed(reuse);
        }

        var raw = CreateSecret();
        var replacement = new AuthSession
        {
            UserId = session.UserId,
            TenantId = session.TenantId,
            FamilyId = session.FamilyId,
            TokenHash = Hash(raw),
            CreatedAt = now,
            ExpiresAt = now.AddDays(RefreshDays),
            DeviceLabel = session.DeviceLabel
        };
        db.AuthSessions.Add(replacement);
        await db.SaveChangesAsync(cancellationToken);

        session.ReplacedBySessionId = replacement.Id;
        await db.SaveChangesAsync(cancellationToken);

        var roles = await _roleRepository.GetUserRoleNamesAsync(user.Id, cancellationToken);
        var permissions = await _roleRepository.GetUserPermissionCodesAsync(user.Id, cancellationToken);
        var access = _jwtTokenGenerator.GenerateToken(
            user.Id,
            user.Email ?? string.Empty,
            user.TenantId,
            roles,
            permissions,
            user.SecurityVersion);

        return new SessionRotationResult(
            true,
            false,
            new AuthResponse
            {
                Token = access,
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                TenantId = user.TenantId,
                Roles = roles,
                Permissions = permissions,
                ExpiresAt = _jwtTokenGenerator.GetTokenExpiration()
            },
            new IssuedBrowserSession(raw, CreateSecret(), replacement.ExpiresAt));
    }

    private static async Task<bool> TryClaimAsync(
        BusinessOSDbContext db,
        AuthSession session,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (db.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true)
        {
            if (session.RevokedAt is not null)
            {
                return false;
            }

            session.RevokedAt = now;
            session.LastUsedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        var claimed = await db.AuthSessions
            .Where(x => x.Id == session.Id && x.RevokedAt == null && x.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.RevokedAt, now)
                    .SetProperty(x => x.LastUsedAt, now),
                cancellationToken);

        return claimed == 1;
    }

    private static async Task RevokeFamilyAsync(
        BusinessOSDbContext db,
        Guid familyId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var family = await db.AuthSessions
            .Where(x => x.FamilyId == familyId && x.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var member in family)
        {
            member.RevokedAt = now;
            member.ReuseDetected = true;
        }

        var presented = await db.AuthSessions
            .Where(x => x.FamilyId == familyId && x.ReplacedBySessionId != null)
            .ToListAsync(cancellationToken);
        foreach (var member in presented)
        {
            member.ReuseDetected = true;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private int RefreshDays => _options.RefreshTokenDays is >= 1 and <= 90
        ? _options.RefreshTokenDays
        : 14;

    private int GraceSeconds => _options.RefreshReuseGraceSeconds is >= 0 and <= 120
        ? _options.RefreshReuseGraceSeconds
        : 15;

    private static SessionRotationResult Failed(bool reuse = false) =>
        new(false, reuse, null, null);

    internal static string Hash(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string CreateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(bytes);
    }

    private static string? NormalizeDevice(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        var cleaned = new string(userAgent.Where(ch => !char.IsControl(ch)).ToArray()).Trim();
        if (cleaned.Length == 0)
        {
            return null;
        }

        return cleaned.Length <= 160 ? cleaned : cleaned[..160];
    }
}
