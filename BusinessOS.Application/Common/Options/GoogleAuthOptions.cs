namespace BusinessOS.Application.Common.Options;

/// <summary>
/// Public Google OAuth client settings. The ID-token flow does not use a client secret.
/// </summary>
public sealed class GoogleAuthOptions
{
    public const string SectionName = "Google";

    /// <summary>
    /// OAuth 2.0 client ID from Google Cloud Console. This value is public and is also
    /// embedded in the Angular app. It is not a secret, but it must match the SPA client.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;
}
