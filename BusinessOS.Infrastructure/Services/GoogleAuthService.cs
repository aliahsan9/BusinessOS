using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Auth.DTOs;
using BusinessOS.Application.Features.Auth.Google;
using BusinessOS.Application.Features.Auth.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.Services;

public sealed class GoogleAuthService : IGoogleAuthService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IGoogleIdTokenValidator _validator;
    private readonly IIdentityService _identityService;
    private readonly IAuthService _authService;
    private readonly IDataProtector _protector;
    private readonly IRbacAuditService _auditService;
    private readonly TimeProvider _time;
    private readonly ILogger<GoogleAuthService> _logger;

    public GoogleAuthService(
        IGoogleIdTokenValidator validator,
        IIdentityService identityService,
        IAuthService authService,
        IDataProtectionProvider dataProtection,
        IRbacAuditService auditService,
        TimeProvider time,
        ILogger<GoogleAuthService> logger)
    {
        _validator = validator;
        _identityService = identityService;
        _authService = authService;
        _protector = dataProtection.CreateProtector(GoogleAuthConstants.SignupProtectorPurpose);
        _auditService = auditService;
        _time = time;
        _logger = logger;
    }

    public async Task<GoogleSignInResponse> SignInAsync(string idToken, CancellationToken cancellationToken)
    {
        var identity = await ValidateOrRejectAsync(idToken, cancellationToken);
        var linked = await _identityService.FindByLoginAsync(
            GoogleAuthConstants.Provider,
            identity.Subject,
            cancellationToken);

        if (linked is not null)
        {
            var session = await _authService.CreateSessionForUserAsync(
                linked,
                cancellationToken,
                "GoogleLogin");
            _logger.LogInformation("Google sign-in succeeded for user {UserId}", linked.Id);
            return new GoogleSignInResponse
            {
                Status = GoogleSignInStatus.Authenticated,
                Auth = session
            };
        }

        var existing = await _identityService.FindByEmailAsync(identity.Email, cancellationToken);
        if (existing is not null)
        {
            _logger.LogInformation(
                "Google sign-in refused automatic link for existing account {UserId}",
                existing.Id);
            throw new ConflictException(
                "An account with this email already exists. Sign in with your password, then connect Google from your profile.");
        }

        var (firstName, lastName) = SuggestNames(identity);
        return new GoogleSignInResponse
        {
            Status = GoogleSignInStatus.RegistrationRequired,
            ContinuationToken = ProtectTicket(identity),
            Email = identity.Email,
            FirstName = firstName,
            LastName = lastName,
            Message = "Add your business name to finish creating your BusinessOS workspace."
        };
    }

    public async Task<AuthResponse> CompleteRegistrationAsync(
        string continuationToken,
        string businessName,
        string firstName,
        string lastName,
        CancellationToken cancellationToken)
    {
        var ticket = ReadTicket(continuationToken);
        var linked = await _identityService.FindByLoginAsync(
            GoogleAuthConstants.Provider,
            ticket.Subject,
            cancellationToken);
        if (linked is not null)
        {
            throw new ConflictException(
                "This Google account is already registered. Continue with Google to sign in.");
        }

        var existing = await _identityService.FindByEmailAsync(ticket.Email, cancellationToken);
        if (existing is not null)
        {
            throw new ConflictException(
                "An account with this email already exists. Sign in with your password, then connect Google from your profile.");
        }

        var names = SuggestNames(new GoogleIdentity(
            ticket.Subject,
            ticket.Email,
            true,
            firstName,
            lastName,
            ticket.PictureUrl));

        var session = await _authService.RegisterExternalUserAsync(
            ticket.Email,
            names.FirstName,
            names.LastName,
            businessName.Trim(),
            GoogleAuthConstants.Provider,
            ticket.Subject,
            ticket.PictureUrl,
            cancellationToken);

        _logger.LogInformation("Google registration completed for user {UserId}", session.UserId);
        return session;
    }

    public async Task LinkAsync(string userId, string idToken, CancellationToken cancellationToken)
    {
        var identity = await ValidateOrRejectAsync(idToken, cancellationToken);
        var result = await _identityService.LinkExternalLoginAsync(
            userId,
            GoogleAuthConstants.Provider,
            identity.Subject,
            cancellationToken);

        if (!result.Succeeded)
        {
            var message = result.Errors.FirstOrDefault() ?? "Google could not be connected.";
            if (message.Contains("another", StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException(message);
            }

            throw new BadRequestException(message);
        }

        await _auditService.LogAsync(
            "GoogleAccountLinked",
            "User",
            userId,
            null,
            RbacAuditService.Serialize(new { provider = GoogleAuthConstants.Provider }),
            cancellationToken);
        _logger.LogInformation("Google account linked for user {UserId}", userId);
    }

    public async Task UnlinkAsync(string userId, CancellationToken cancellationToken)
    {
        var result = await _identityService.RemoveExternalLoginAsync(
            userId,
            GoogleAuthConstants.Provider,
            cancellationToken);

        if (!result.Succeeded)
        {
            throw new BadRequestException(result.Errors.FirstOrDefault() ?? "Google could not be disconnected.");
        }

        await _auditService.LogAsync(
            "GoogleAccountUnlinked",
            "User",
            userId,
            null,
            RbacAuditService.Serialize(new { provider = GoogleAuthConstants.Provider }),
            cancellationToken);
        _logger.LogInformation("Google account unlinked for user {UserId}", userId);
    }

    private async Task<GoogleIdentity> ValidateOrRejectAsync(string idToken, CancellationToken cancellationToken)
    {
        try
        {
            return await _validator.ValidateAsync(idToken, cancellationToken);
        }
        catch (GoogleTokenValidationException ex)
        {
            _logger.LogWarning("Google authentication failed. Reason={Reason}", ex.Failure);
            throw new UnauthorizedException("Google sign-in could not be completed.");
        }
    }

    private string ProtectTicket(GoogleIdentity identity)
    {
        var ticket = new GoogleSignupTicket
        {
            Subject = identity.Subject,
            Email = identity.Email,
            GivenName = identity.GivenName,
            FamilyName = identity.FamilyName,
            PictureUrl = SafeHttpsAvatar(identity.PictureUrl),
            ExpiresAtUnix = _time.GetUtcNow().Add(GoogleAuthConstants.SignupTicketLifetime).ToUnixTimeSeconds()
        };
        var json = JsonSerializer.Serialize(ticket, JsonOptions);
        return _protector.Protect(json);
    }

    private GoogleSignupTicket ReadTicket(string continuationToken)
    {
        GoogleSignupTicket? ticket;
        try
        {
            var json = _protector.Unprotect(continuationToken);
            ticket = JsonSerializer.Deserialize<GoogleSignupTicket>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or JsonException)
        {
            _logger.LogWarning("Rejected Google signup continuation ticket. ErrorType={ErrorType}", ex.GetType().Name);
            throw new UnauthorizedException("Google sign-up could not be completed. Start again with Google.");
        }

        if (ticket is null ||
            string.IsNullOrWhiteSpace(ticket.Subject) ||
            string.IsNullOrWhiteSpace(ticket.Email) ||
            ticket.ExpiresAtUnix < _time.GetUtcNow().ToUnixTimeSeconds())
        {
            throw new UnauthorizedException("Google sign-up could not be completed. Start again with Google.");
        }

        ticket.PictureUrl = SafeHttpsAvatar(ticket.PictureUrl);
        return ticket;
    }

    private static (string FirstName, string LastName) SuggestNames(GoogleIdentity identity)
    {
        var localPart = identity.Email.Split('@')[0];
        var first = CleanName(identity.GivenName, string.IsNullOrWhiteSpace(localPart) ? "Google" : localPart);
        var last = CleanName(identity.FamilyName, "User");
        return (first, last);
    }

    private static string CleanName(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return TrimName(fallback);
        }

        var cleaned = new string(value.Trim().Where(character => !char.IsControl(character)).ToArray());
        return cleaned.Length == 0 ? TrimName(fallback) : TrimName(cleaned);
    }

    private static string TrimName(string value) =>
        value.Length <= 100 ? value : value[..100];

    private static string? SafeHttpsAvatar(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var trimmed = url.Trim();
        if (trimmed.Length > 2048 ||
            !Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        return trimmed;
    }

    private sealed class GoogleSignupTicket
    {
        public string Subject { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string? GivenName { get; set; }

        public string? FamilyName { get; set; }

        public string? PictureUrl { get; set; }

        public long ExpiresAtUnix { get; set; }
    }
}
