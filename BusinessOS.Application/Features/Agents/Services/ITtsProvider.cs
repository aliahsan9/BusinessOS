using BusinessOS.Application.Features.Agents.DTOs;

namespace BusinessOS.Application.Features.Agents.Services;

/// <summary>
/// One text-to-speech backend. The API key, if any, stays inside the provider.
/// </summary>
public interface ITtsProvider
{
    /// <summary>Stable provider id, such as <c>openai</c> or <c>edge</c>.</summary>
    string Key { get; }

    /// <summary>False when required configuration, such as an API key, is missing.</summary>
    bool IsAvailable { get; }

    /// <summary>Model or engine id included in the audio cache key.</summary>
    string ModelId { get; }

    /// <summary>Voice used when the caller does not choose one.</summary>
    string DefaultVoice { get; }

    /// <summary>Voices this provider accepts.</summary>
    IReadOnlyList<TtsVoiceOption> Voices { get; }

    Task<SophiaTtsResult> SynthesizeAsync(
        TtsSynthesisRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Input for a single synthesis call.</summary>
public sealed class TtsSynthesisRequest
{
    public required string Text { get; init; }

    public string Language { get; init; } = "en-US";

    public string? VoiceName { get; init; }

    public double SpeechRate { get; init; } = 1.0;
}
