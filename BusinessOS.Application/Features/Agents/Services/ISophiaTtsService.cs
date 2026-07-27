namespace BusinessOS.Application.Features.Agents.Services;

/// <summary>
/// Neural text-to-speech for Sophia (English spoken replies).
/// </summary>
public interface ISophiaTtsService
{
    /// <summary>
    /// Synthesize speech audio (MP3) using neural English voice.
    /// </summary>
    Task<SophiaTtsResult> SynthesizeAsync(
        string text,
        string language,
        double speechRate = 1.0,
        CancellationToken cancellationToken = default);
}

public sealed class SophiaTtsResult
{
    public required byte[] AudioBytes { get; init; }
    public required string ContentType { get; init; }
    public required string VoiceName { get; init; }
}
