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
/// ISO-style language codes used by AI employee voice and chat (English only).
/// </summary>
public static class AgentLanguages
{
    public const string English = "en";

    public static readonly IReadOnlyList<string> All =
    [
        English
    ];

    public static bool IsSupported(string? language) =>
        string.Equals(language?.Trim(), English, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string? language) => English;

    public static AgentVoiceLanguage ToVoiceLanguage(string? language) => AgentVoiceLanguage.En;

    public static string FromVoiceLanguage(AgentVoiceLanguage language) => English;
}
