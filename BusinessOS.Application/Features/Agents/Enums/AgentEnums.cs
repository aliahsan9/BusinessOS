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
/// Language codes for Sophia chat and voice. Concrete codes are en, ur, ur-Latn, and ar.
/// "auto" means detect from the message. Add a code here before teaching the resolver a new language.
/// </summary>
public static class AgentLanguages
{
    public const string English = "en";
    public const string Urdu = "ur";
    public const string RomanUrdu = "ur-Latn";
    public const string Arabic = "ar";
    public const string Auto = "auto";

    public static readonly IReadOnlyList<string> All =
    [
        English,
        Urdu,
        RomanUrdu,
        Arabic
    ];

    public static bool IsSupported(string? language) => TryGetConcrete(language, out _);

    /// <summary>A speakable language. Auto and blank become English.</summary>
    public static string Normalize(string? language) =>
        TryGetConcrete(language, out var code) ? code : English;

    public static bool TryGetConcrete(string? language, out string code)
    {
        code = English;
        if (string.IsNullOrWhiteSpace(language))
            return false;

        var value = language.Trim().Replace('_', '-');
        if (value.Equals(Auto, StringComparison.OrdinalIgnoreCase)
            || value.Equals("detect", StringComparison.OrdinalIgnoreCase))
            return false;

        if (value.Equals(RomanUrdu, StringComparison.OrdinalIgnoreCase)
            || value.Equals("roman", StringComparison.OrdinalIgnoreCase)
            || value.Equals("roman-urdu", StringComparison.OrdinalIgnoreCase))
        {
            code = RomanUrdu;
            return true;
        }

        if (value.StartsWith("ur", StringComparison.OrdinalIgnoreCase)
            || value.Equals("urdu", StringComparison.OrdinalIgnoreCase))
        {
            code = Urdu;
            return true;
        }

        if (value.StartsWith("ar", StringComparison.OrdinalIgnoreCase)
            || value.Equals("arabic", StringComparison.OrdinalIgnoreCase))
        {
            code = Arabic;
            return true;
        }

        if (value.StartsWith("en", StringComparison.OrdinalIgnoreCase)
            || value.Equals("english", StringComparison.OrdinalIgnoreCase))
        {
            code = English;
            return true;
        }

        return false;
    }

    /// <summary>First concrete language, otherwise English.</summary>
    public static string Coalesce(params string?[] values)
    {
        foreach (var value in values)
        {
            if (TryGetConcrete(value, out var code))
                return code;
        }

        return English;
    }

    public static AgentVoiceLanguage ToVoiceLanguage(string? language)
    {
        if (!TryGetConcrete(language, out var code))
            return AgentVoiceLanguage.Auto;

        if (code.StartsWith("ur", StringComparison.OrdinalIgnoreCase))
            return AgentVoiceLanguage.Ur;
        if (code.Equals(Arabic, StringComparison.OrdinalIgnoreCase))
            return AgentVoiceLanguage.Ar;
        return AgentVoiceLanguage.En;
    }

    public static string FromVoiceLanguage(AgentVoiceLanguage language) => language switch
    {
        AgentVoiceLanguage.Ur => Urdu,
        AgentVoiceLanguage.Ar => Arabic,
        AgentVoiceLanguage.Auto => Auto,
        _ => English
    };

    public static string SpeechLocale(string? language)
    {
        if (!TryGetConcrete(language, out var code))
            return "en-US";
        if (code.StartsWith("ur", StringComparison.OrdinalIgnoreCase))
            return "ur-PK";
        if (code.Equals(Arabic, StringComparison.OrdinalIgnoreCase))
            return "ar-SA";
        return "en-US";
    }

    public static bool IsRtl(string? language) =>
        TryGetConcrete(language, out var code)
        && code is Urdu or Arabic;
}
