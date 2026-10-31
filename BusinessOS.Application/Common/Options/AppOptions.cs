namespace BusinessOS.Application.Common.Options;

public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>
    /// Angular frontend base URL used for password-reset and invitation links.
    /// Reset links are built only from this value, never from the request Host header.
    /// </summary>
    public string FrontendBaseUrl { get; set; } = "http://localhost:4200";

    /// <summary>
    /// Lifetime of ASP.NET Core Identity password-reset tokens. Clamped to 1–168 hours.
    /// </summary>
    public int PasswordResetTokenHours { get; set; } = 24;
}
