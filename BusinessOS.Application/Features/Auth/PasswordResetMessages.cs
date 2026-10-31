namespace BusinessOS.Application.Features.Auth;

public static class PasswordResetMessages
{
    public const string PublicSuccessMessage =
        "If an account exists for this email address, we'll send instructions to reset your password.";

    public const string ResetSucceededMessage =
        "Your password has been reset. Sign in with your new password.";

    public const string InvalidLinkMessage =
        "Unable to reset password. The link may be invalid or expired.";

    public static IReadOnlyList<string> PublicErrors(IReadOnlyList<string> errors)
    {
        if (errors.Count == 0)
        {
            return [InvalidLinkMessage];
        }

        var revealsTokenFailure = errors.Any(error =>
            !error.Contains("Password", StringComparison.OrdinalIgnoreCase));
        if (revealsTokenFailure)
        {
            return [InvalidLinkMessage];
        }

        return errors;
    }
}

public static class PasswordResetLink
{
    public static string Build(string frontendBaseUrl, string email, string token)
    {
        if (string.IsNullOrWhiteSpace(frontendBaseUrl))
        {
            throw new InvalidOperationException("App:FrontendBaseUrl is not configured.");
        }

        var baseUrl = frontendBaseUrl.Trim().TrimEnd('/');
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException("App:FrontendBaseUrl must be an absolute http or https URL.");
        }

        return $"{baseUrl}/auth/reset-password" +
               $"?email={Uri.EscapeDataString(email.Trim())}" +
               $"&token={Uri.EscapeDataString(token)}";
    }
}
