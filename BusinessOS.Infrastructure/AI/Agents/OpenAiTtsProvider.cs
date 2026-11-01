using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.Agents.Services;
using BusinessOS.Infrastructure.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.AI.Agents;

/// <summary>
/// OpenAI speech synthesis. The API key is read from server configuration and is not returned to the client.
/// </summary>
public sealed class OpenAiTtsProvider : ITtsProvider
{
    public const string HttpClientName = "OpenAiTts";

    private static readonly TtsVoiceOption[] Catalog =
    [
        new() { Id = "alloy", Label = "Alloy", Description = "Neutral" },
        new() { Id = "ash", Label = "Ash", Description = "Clear" },
        new() { Id = "ballad", Label = "Ballad", Description = "Warm" },
        new() { Id = "coral", Label = "Coral", Description = "Friendly" },
        new() { Id = "echo", Label = "Echo", Description = "Steady" },
        new() { Id = "fable", Label = "Fable", Description = "Expressive" },
        new() { Id = "nova", Label = "Nova", Description = "Bright" },
        new() { Id = "onyx", Label = "Onyx", Description = "Deep" },
        new() { Id = "sage", Label = "Sage", Description = "Calm" },
        new() { Id = "shimmer", Label = "Shimmer", Description = "Light" },
        new() { Id = "verse", Label = "Verse", Description = "Narrative" }
    ];

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiOptions _ai;
    private readonly TtsOptions _tts;
    private readonly ILogger<OpenAiTtsProvider> _logger;

    public OpenAiTtsProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<AiOptions> aiOptions,
        IOptions<TtsOptions> ttsOptions,
        ILogger<OpenAiTtsProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _ai = aiOptions.Value;
        _tts = ttsOptions.Value;
        _logger = logger;
    }

    public string Key => "openai";

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_ai.OpenAiApiKey);

    public string ModelId => string.IsNullOrWhiteSpace(_tts.OpenAiModel) ? "gpt-4o-mini-tts" : _tts.OpenAiModel.Trim();

    public string DefaultVoice => ResolveVoice(_tts.DefaultVoice);

    public IReadOnlyList<TtsVoiceOption> Voices => Catalog;

    public async Task<SophiaTtsResult> SynthesizeAsync(
        TtsSynthesisRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            throw new InvalidOperationException("OpenAI speech is not configured.");

        var text = TtsTextNormalizer.Normalize(request.Text);
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Nothing to speak.");

        var voice = ResolveVoice(request.VoiceName);
        var speed = TtsTextNormalizer.ClampRate(request.SpeechRate);
        var instructions = SupportsInstructions && !string.IsNullOrWhiteSpace(_tts.Instructions)
            ? _tts.Instructions.Trim()
            : null;
        if (SupportsInstructions)
        {
            var hint = request.Language switch
            {
                not null when request.Language.StartsWith("ur", StringComparison.OrdinalIgnoreCase) =>
                    " The text is Urdu. Pronounce it as natural spoken Urdu. Do not translate it.",
                not null when request.Language.StartsWith("ar", StringComparison.OrdinalIgnoreCase) =>
                    " The text is Arabic. Pronounce it as natural spoken Arabic. Do not translate it.",
                _ => " Speak the text in the language it is written. Do not translate it."
            };
            instructions = string.IsNullOrWhiteSpace(instructions) ? hint.Trim() : instructions + hint;
        }

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/audio/speech");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _ai.OpenAiApiKey);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/mpeg"));
        httpRequest.Content = JsonContent.Create(new SpeechBody
        {
            Model = ModelId,
            Input = text,
            Voice = voice,
            Speed = speed,
            Instructions = instructions
        });

        using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "OpenAI speech failed ({Status}) for voice {Voice}: {Body}",
                (int)response.StatusCode,
                voice,
                Truncate(body, 300));
            throw new InvalidOperationException($"OpenAI speech synthesis failed ({(int)response.StatusCode}).");
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is not null &&
            mediaType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("OpenAI speech returned JSON instead of audio for voice {Voice}", voice);
            throw new InvalidOperationException("OpenAI speech synthesis failed.");
        }

        var audio = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (audio.Length < 32)
            throw new InvalidOperationException("OpenAI returned empty audio.");

        return new SophiaTtsResult
        {
            AudioBytes = audio,
            ContentType = "audio/mpeg",
            VoiceName = voice,
            Provider = Key
        };
    }

    private bool SupportsInstructions =>
        ModelId.Contains("gpt-4o", StringComparison.OrdinalIgnoreCase) ||
        ModelId.Contains("mini-tts", StringComparison.OrdinalIgnoreCase);

    private string ResolveVoice(string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested) &&
            !requested.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            var match = Catalog.FirstOrDefault(v => v.Id.Equals(requested.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match.Id;
        }

        var configured = _tts.DefaultVoice?.Trim();
        var fallback = Catalog.FirstOrDefault(v => v.Id.Equals(configured, StringComparison.OrdinalIgnoreCase));
        return fallback?.Id ?? "coral";
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private sealed class SpeechBody
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("input")]
        public required string Input { get; init; }

        [JsonPropertyName("voice")]
        public required string Voice { get; init; }

        [JsonPropertyName("response_format")]
        public string ResponseFormat { get; init; } = "mp3";

        [JsonPropertyName("speed")]
        public double Speed { get; init; } = 1;

        [JsonPropertyName("instructions")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Instructions { get; init; }
    }
}
