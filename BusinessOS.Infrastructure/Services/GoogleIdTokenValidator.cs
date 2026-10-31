using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.Auth.Google;
using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.Services;

/// <summary>
/// Validates Google ID tokens with Google.Apis.Auth, which checks the signature against Google's
/// published keys plus issuer, audience, and expiry. The raw token is never logged.
/// </summary>
public sealed class GoogleIdTokenValidator : IGoogleIdTokenValidator
{
    private readonly GoogleAuthOptions _options;
    private readonly ILogger<GoogleIdTokenValidator> _logger;

    public GoogleIdTokenValidator(
        IOptions<GoogleAuthOptions> options,
        ILogger<GoogleIdTokenValidator> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<GoogleIdentity> ValidateAsync(string idToken, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            _logger.LogError("Google sign-in was attempted but Google:ClientId is not configured.");
            throw new GoogleTokenValidationException(GoogleTokenFailure.NotConfigured);
        }

        if (string.IsNullOrWhiteSpace(idToken) || idToken.Length > 8192)
        {
            throw new GoogleTokenValidationException(GoogleTokenFailure.Invalid);
        }

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [_options.ClientId.Trim()]
                }).WaitAsync(cancellationToken);
        }
        catch (InvalidJwtException ex)
        {
            var failure = GoogleTokenFailureClassifier.Classify(ex.Message);
            _logger.LogWarning("Rejected Google ID token. Reason={Reason}", failure);
            throw new GoogleTokenValidationException(failure);
        }

        if (string.IsNullOrWhiteSpace(payload.Subject))
        {
            throw new GoogleTokenValidationException(GoogleTokenFailure.MissingSubject);
        }

        if (string.IsNullOrWhiteSpace(payload.Email))
        {
            throw new GoogleTokenValidationException(GoogleTokenFailure.MissingEmail);
        }

        if (!payload.EmailVerified)
        {
            _logger.LogWarning("Rejected Google ID token because the email is not verified.");
            throw new GoogleTokenValidationException(GoogleTokenFailure.UnverifiedEmail);
        }

        return new GoogleIdentity(
            payload.Subject,
            payload.Email.Trim(),
            true,
            payload.GivenName,
            payload.FamilyName,
            payload.Picture);
    }
}
