namespace BusinessOS.Application.Features.Agents.Services;

/// <summary>
/// Neural text-to-speech for Sophia English spoken replies.
/// </summary>
public interface ISophiaTtsService
{
    /// <summary>
    /// Synthesizes speech audio in MP3 format using a neural English voice.
    /// </summary>
    /// <param name="text">Plain text to speak.</param>
    /// <param name="language">BCP-47 language code (e.g. en-US).</param>
    /// <param name="speechRate">Playback speed multiplier (1.0 = normal).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Audio bytes, content type, and voice name.</returns>
    Task<SophiaTtsResult> SynthesizeAsync(
        string text,
        string language,
        double speechRate = 1.0,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a Sophia TTS synthesis request.
/// </summary>
public sealed class SophiaTtsResult
{
    /// <summary>Raw audio bytes (typically MP3).</summary>
    public required byte[] AudioBytes { get; init; }

    /// <summary>MIME content type of the audio (e.g. audio/mpeg).</summary>
    public required string ContentType { get; init; }

    /// <summary>Neural voice identifier used for synthesis.</summary>
    public required string VoiceName { get; init; }
}
