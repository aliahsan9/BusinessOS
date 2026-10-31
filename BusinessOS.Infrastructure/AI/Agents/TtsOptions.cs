namespace BusinessOS.Infrastructure.AI.Agents;

/// <summary>
/// Speech synthesis settings. The OpenAI key is Ai:OpenAiApiKey and is never sent to the browser.
/// </summary>
public sealed class TtsOptions
{
    public const string SectionName = "Ai:Tts";

    /// <summary>Primary provider key. <c>openai</c> is the natural voice.</summary>
    public string Provider { get; set; } = "openai";

    /// <summary>Tried when the primary provider is missing or fails. Empty disables fallback.</summary>
    public string FallbackProvider { get; set; } = "edge";

    public bool CacheEnabled { get; set; } = true;

    /// <summary>How long identical audio stays cached. Zero keeps entries until the size cap evicts them.</summary>
    public int CacheMinutes { get; set; } = 120;

    public int CacheMaxEntries { get; set; } = 64;

    /// <summary>Approximate cap on cached audio bytes.</summary>
    public int CacheMaxBytes { get; set; } = 8 * 1024 * 1024;

    /// <summary>OpenAI speech model. gpt-4o-mini-tts is the natural voice.</summary>
    public string OpenAiModel { get; set; } = "gpt-4o-mini-tts";

    /// <summary>Used when the saved preference is empty or <c>default</c>.</summary>
    public string DefaultVoice { get; set; } = "coral";

    /// <summary>Style hint for models that accept instructions. Ignored by tts-1.</summary>
    public string Instructions { get; set; } =
        "Speak in a natural, warm conversational voice. Clear pronunciation, relaxed pace, normal intonation. Do not sound like a public-address system, and do not add words.";
}
