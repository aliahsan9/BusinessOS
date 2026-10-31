namespace BusinessOS.Application.Features.Auth.Google;

public static class GoogleAuthConstants
{
    public const string Provider = "Google";
    public const string SignupProtectorPurpose = "BusinessOS.GoogleSignup.v1";
    public static readonly TimeSpan SignupTicketLifetime = TimeSpan.FromMinutes(10);
}

public static class GoogleSignInStatus
{
    public const string Authenticated = "authenticated";
    public const string RegistrationRequired = "registration_required";
}

public enum GoogleTokenFailure
{
    Invalid,
    Expired,
    WrongAudience,
    UntrustedIssuer,
    UnverifiedEmail,
    MissingSubject,
    MissingEmail,
    NotConfigured
}

/// <summary>
/// Raised when a Google ID token cannot be trusted. The message is generic so it can be logged
/// without echoing the token or the library's raw validation text.
/// </summary>
public sealed class GoogleTokenValidationException : Exception
{
    public GoogleTokenValidationException(GoogleTokenFailure failure)
        : base("Google ID token was rejected.")
    {
        Failure = failure;
    }

    public GoogleTokenFailure Failure { get; }
}

public static class GoogleTokenFailureClassifier
{
    public static GoogleTokenFailure Classify(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return GoogleTokenFailure.Invalid;
        }

        if (message.Contains("expired", StringComparison.OrdinalIgnoreCase))
        {
            return GoogleTokenFailure.Expired;
        }

        if (message.Contains("audience", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("aud", StringComparison.OrdinalIgnoreCase))
        {
            return GoogleTokenFailure.WrongAudience;
        }

        if (message.Contains("issuer", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("iss", StringComparison.OrdinalIgnoreCase))
        {
            return GoogleTokenFailure.UntrustedIssuer;
        }

        return GoogleTokenFailure.Invalid;
    }
}

/// <summary>
/// Claims taken from a signature-validated Google ID token. Never construct this from client JSON.
/// </summary>
public sealed record GoogleIdentity(
    string Subject,
    string Email,
    bool EmailVerified,
    string? GivenName,
    string? FamilyName,
    string? PictureUrl);

public interface IGoogleIdTokenValidator
{
    Task<GoogleIdentity> ValidateAsync(string idToken, CancellationToken cancellationToken);
}
