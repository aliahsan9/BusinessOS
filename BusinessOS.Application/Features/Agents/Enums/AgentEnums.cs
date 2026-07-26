using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Features.Agents.Enums;

/// <summary>
/// Stable machine keys for built-in AI employee agents.
/// </summary>
public static class AgentKeys
{
    public const string Sophia = "sophia";
    public const string Adam = "adam";
    public const string Emma = "emma";

    public static readonly IReadOnlyList<string> All =
    [
        Sophia,
        Adam,
        Emma
    ];

    public static bool IsKnown(string? key) =>
        !string.IsNullOrWhiteSpace(key)
        && All.Any(k => string.Equals(k, key.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string Normalize(string? key) =>
        string.IsNullOrWhiteSpace(key)
            ? Sophia
            : key.Trim().ToLowerInvariant();
}

/// <summary>
/// ISO-style language codes used by AI employee voice and chat.
/// </summary>
public static class AgentLanguages
{
    public const string English = "en";
    public const string Urdu = "ur";

    public static readonly IReadOnlyList<string> All =
    [
        English,
        Urdu
    ];

    public static bool IsSupported(string? language) =>
        !string.IsNullOrWhiteSpace(language)
        && All.Any(l => string.Equals(l, language.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string Normalize(string? language) =>
        string.Equals(language?.Trim(), Urdu, StringComparison.OrdinalIgnoreCase)
            ? Urdu
            : English;

    public static AgentVoiceLanguage ToVoiceLanguage(string? language) =>
        Normalize(language) == Urdu
            ? AgentVoiceLanguage.Ur
            : AgentVoiceLanguage.En;

    public static string FromVoiceLanguage(AgentVoiceLanguage language) =>
        language == AgentVoiceLanguage.Ur ? Urdu : English;
}
