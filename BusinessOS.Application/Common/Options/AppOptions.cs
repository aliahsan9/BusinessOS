namespace BusinessOS.Application.Common.Options;

public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>
    /// Angular frontend base URL used for password-reset and invitation links.
    /// </summary>
    public string FrontendBaseUrl { get; set; } = "http://localhost:4200";
}
