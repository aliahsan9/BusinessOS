namespace BusinessOS.Domain.Enums;

/// <summary>
/// Preferred spoken language for AI employee voice interactions.
/// </summary>
public enum AgentVoiceLanguage
{
    /// <summary>English (en).</summary>
    En = 0,

    /// <summary>Urdu (ur), including Roman Urdu speech.</summary>
    Ur = 1,

    /// <summary>Arabic (ar).</summary>
    Ar = 2,

    /// <summary>Detect the language from each message.</summary>
    Auto = 3
}
