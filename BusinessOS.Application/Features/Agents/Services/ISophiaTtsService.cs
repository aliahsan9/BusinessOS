using BusinessOS.Application.Features.Agents.DTOs;

namespace BusinessOS.Application.Features.Agents.Services;

/// <summary>
/// Selects a configured speech provider, caches identical audio, and falls back when a provider fails.
/// </summary>
public interface ISophiaTtsService
{
    /// <summary>Provider that will be tried first.</summary>
    string ActiveProvider { get; }

    /// <summary>Default voice of the active provider.</summary>
    string DefaultVoice { get; }

    /// <summary>Voices the client may choose for the active provider.</summary>
    IReadOnlyList<TtsVoiceOption> ListVoices();

    /// <summary>
    /// Synthesizes speech audio, usually MP3.
    /// </summary>
    Task<SophiaTtsResult> SynthesizeAsync(
        string text,
        string language,
        double speechRate = 1.0,
        string? voiceName = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a speech synthesis request.
/// </summary>
public sealed class SophiaTtsResult
{
    /// <summary>Raw audio bytes (typically MP3).</summary>
    public required byte[] AudioBytes { get; init; }

    /// <summary>MIME content type of the audio (e.g. audio/mpeg).</summary>
    public required string ContentType { get; init; }

    /// <summary>Voice identifier used for synthesis.</summary>
    public required string VoiceName { get; init; }

    /// <summary>Provider that produced the audio.</summary>
    public string Provider { get; init; } = "";

    /// <summary>True when the audio was served from the response cache.</summary>
    public bool ServedFromCache { get; init; }
}
